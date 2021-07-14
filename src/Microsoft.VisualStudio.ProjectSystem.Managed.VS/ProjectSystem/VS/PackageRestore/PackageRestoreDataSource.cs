// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Internal.Performance;
using Microsoft.VisualStudio.IO;
using Microsoft.VisualStudio.Threading;
using NuGet.SolutionRestoreManager;

namespace Microsoft.VisualStudio.ProjectSystem.VS.PackageRestore
{
    /// <summary>
    ///     Responsible for pushing ("nominating") project data such as referenced packages and
    ///     target frameworks to NuGet so that it can perform a package restore and returns the
    ///     result.
    /// </summary>
    [Export(typeof(IPackageRestoreDataSource))]
    [Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectDynamicLoadComponent))]
    [AppliesTo(ProjectCapability.PackageReferences)]
    internal class PackageRestoreDataSource : ChainedProjectValueDataSourceBase<RestoreData>, IPackageRestoreDataSource, IProjectDynamicLoadComponent, IVsProjectRestoreInfoSource
    {
        // This class represents the last data source in the package restore chain, which is made up of the following:
        //  _________________________________________      _________________________________________
        // |                                         |    |                                         |
        // | PackageRestoreConfiguredInputDataSource |    | PackageRestoreConfiguredInputDataSource |  Produces data for each "implicitly active" config
        // |            (Debug|AnyCPU|net45)         |    |       (Debug|AnyCPU|netcoreapp30)       |
        // |_________________________________________|    |_________________________________________|
        //                                 ||                         ||
        //                                 PackageRestoreConfiguredInput
        //                                 ||                         ||
        //                         ________\/_________________________\/______
        //                        |                                           |
        //                        | PackageRestoreUnconfiguredInputDataSource |                        Merges config data into single input
        //                        |___________________________________________|
        //                                             ||
        //                                PackageRestoreUnconfiguredInput
        //                                             || 
        //                         ____________________\/_____________________
        //                        |                                           |
        //                        |         PackageRestoreDataSource          |                        Pushes restore data to NuGet
        //                        |___________________________________________|
        //                                             ||
        //                                        RestoreData
        //                                        //        \\ 
        //                                       //          \\
        //  ____________________________________\/___      ___\/____________________________________
        // |                                         |    |                                         |
        // |        PackageRestoreProgressTracker    |    |       PackageRestoreProgressTracker     |  Publishes restore progress to operation progress
        // |            (Debug|AnyCPU|net45)         |    |       (Debug|AnyCPU|netcoreapp30)       |
        // |_________________________________________|    |_________________________________________|
        //

        private readonly UnconfiguredProject _project;
        private readonly IPackageRestoreUnconfiguredInputDataSource _dataSource;
        private readonly IProjectAsynchronousTasksService _projectAsynchronousTasksService;
        private readonly IVsSolutionRestoreService3 _solutionRestoreService;
        private readonly IFileSystem _fileSystem;
        private readonly IProjectDiagnosticOutputService _logger;
        private readonly IProjectDependentFileChangeNotificationService _projectDependentFileChangeNotificationService;
        private readonly IVsSolutionRestoreService4 _solutionRestoreService4;
        private byte[]? _latestHash;
        private bool _enabled;

        /// <summary>
        /// Project Unique Name used by Nuget Nomination.
        /// </summary>
        public string Name => _project.FullPath;

        /// <summary>
        /// Re-usable task that completes when there is a new nomination
        /// </summary>
        private TaskCompletionSource<bool>? _whenNominatedTask;
        private bool _nugetRestoreStarted;

        /// <summary>
        /// Save the configured project versions that might get nominations.
        /// </summary>
        private readonly Dictionary<ProjectConfiguration, IComparable> _savedNominatedConfiguredVersion = new();

        [ImportingConstructor]
        public PackageRestoreDataSource(
            UnconfiguredProject project,
            IPackageRestoreUnconfiguredInputDataSource dataSource,
            [Import(ExportContractNames.Scopes.UnconfiguredProject)] IProjectAsynchronousTasksService projectAsynchronousTasksService,
            IVsSolutionRestoreService3 solutionRestoreService,
            IFileSystem fileSystem,
            IProjectDiagnosticOutputService logger,
            IProjectDependentFileChangeNotificationService projectDependentFileChangeNotificationService,
            IVsSolutionRestoreService4 solutionRestoreService4)
            : base(project, synchronousDisposal: true, registerDataSource: false)
        {
            _project = project;
            _dataSource = dataSource;
            _projectAsynchronousTasksService = projectAsynchronousTasksService;
            _solutionRestoreService = solutionRestoreService;
            _fileSystem = fileSystem;
            _logger = logger;
            _projectDependentFileChangeNotificationService = projectDependentFileChangeNotificationService;
            _solutionRestoreService4 = solutionRestoreService4;
        }

        protected override IDisposable? LinkExternalInput(ITargetBlock<IProjectVersionedValue<RestoreData>> targetBlock)
        {
            // Register before this project receives any data flows containing possible nominations.
            // This is needed because we need to register before any nuget restore or before the solution load.
            _project.Services.FaultHandler.Forget(
                _solutionRestoreService4.RegisterRestoreInfoSourceAsync(this, _projectAsynchronousTasksService.UnloadCancellationToken), 
                _project, 
                ProjectFaultSeverity.LimitedFunctionality);

            JoinUpstreamDataSources(_dataSource);

            // Take the unconfigured "restore inputs", send them to NuGet, and then return the result of that restore
            // We make use of TransformMany so that we can opt out of returning
            DisposableValue<ISourceBlock<IProjectVersionedValue<RestoreData>>> transformBlock = _dataSource.SourceBlock.TransformManyWithNoDelta(RestoreAsync);

            transformBlock.Value.LinkTo(targetBlock, DataflowOption.PropagateCompletion);

            return transformBlock;
        }

        internal async Task<IEnumerable<IProjectVersionedValue<RestoreData>>> RestoreAsync(IProjectVersionedValue<PackageRestoreUnconfiguredInput> e)
        {
            // No configurations - likely during project close
            if (!_enabled || e.Value.RestoreInfo is null)
                return Enumerable.Empty<IProjectVersionedValue<RestoreData>>();

            bool succeeded = await RestoreCoreAsync(e.Value);

            RestoreData restoreData = CreateRestoreData(e.Value.RestoreInfo, succeeded);

            return new[]
            {
                new ProjectVersionedValue<RestoreData>(restoreData, e.DataSourceVersions)
            };
        }

        private async Task<bool> RestoreCoreAsync(PackageRestoreUnconfiguredInput value)
        {
            ProjectRestoreInfo? restoreInfo = value.RestoreInfo;

            // Prevent extra restore under some conditions.
            if (IsProjectConfigurationVersionOutOfDate(value.ConfiguredInputs))
            {
                return false;
            }

            // Restore service always does work regardless of whether the value we pass 
            // them to actually contains changes, only nominate if there are any.
            byte[] hash = RestoreHasher.CalculateHash(restoreInfo!);

            if (_latestHash != null && Enumerable.SequenceEqual(hash, _latestHash))
            {
                SaveNominatedConfiguredVersions(value.ConfiguredInputs);
                return true;
            }

            _latestHash = hash;

            _nugetRestoreStarted = true;
            JoinableTask<bool> joinableTask = JoinableFactory.RunAsync(() =>
            {
                return NominateForRestoreAsync(restoreInfo!, _projectAsynchronousTasksService.UnloadCancellationToken);
            });

            SaveNominatedConfiguredVersions(value.ConfiguredInputs);

            _projectAsynchronousTasksService.RegisterAsyncTask(joinableTask,
                                                               ProjectCriticalOperation.Build | ProjectCriticalOperation.Unload | ProjectCriticalOperation.Rename,
                                                               registerFaultHandler: true);

            // Prevent overlap until Restore completes
            bool success = await joinableTask;

            _nugetRestoreStarted = false;

            HintProjectDependentFile(restoreInfo!);

            return success;
        }

        private bool IsProjectConfigurationVersionOutOfDate(IReadOnlyCollection<PackageRestoreConfiguredInput> configuredInputs)
        {
            Assumes.Present(_project.Services.ActiveConfiguredProjectProvider);
            Assumes.Present(_project.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject);

            var activeConfiguredProject = _project.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject;

            if (!_savedNominatedConfiguredVersion.TryGetValue(activeConfiguredProject.ProjectConfiguration, out IComparable savedVersionOfActiveConfiguration))
            {
                return false;
            }

            // Nuget only cares about the active configuration
            foreach (var configuredInput in configuredInputs)
            {
                if (configuredInput.ProjectConfiguration.Equals(activeConfiguredProject.ProjectConfiguration) &&
                    configuredInput.ConfiguredProjectVersion.IsLaterThanOrEqualTo(savedVersionOfActiveConfiguration))
                {
                    return true;
                }
            }

            return false;
        }

        private void SaveNominatedConfiguredVersions(IReadOnlyCollection<PackageRestoreConfiguredInput> configuredInputs)
        {
            lock (SyncObject)
            {
                _savedNominatedConfiguredVersion.Clear();

                foreach (var configuredInput in configuredInputs)
                {
                    _savedNominatedConfiguredVersion[configuredInput.ProjectConfiguration] = configuredInput.ConfiguredProjectVersion;
                }

                if (_whenNominatedTask is not null)
                {
                    _whenNominatedTask.SetResult(true);
                }
            }
        }

        private void HintProjectDependentFile(ProjectRestoreInfo restoreInfo)
        {
            if (restoreInfo.ProjectAssetsFilePath.Length != 0)
            {
                // Hint to CPS that the assets file "might" have changed and therefore
                // reevaluate if it has. It already listens to file-changed events for it, 
                // but can miss them during periods where the buffer is overflowed when 
                // there are lots of changes.
                _projectDependentFileChangeNotificationService.OnAfterDependentFilesChanged(
                    fileFullPaths: new[] { restoreInfo.ProjectAssetsFilePath },
                    project: ContainingProject);
            }
        }

        private async Task<bool> NominateForRestoreAsync(ProjectRestoreInfo restoreInfo, CancellationToken cancellationToken)
        {
            RestoreLogger.BeginNominateRestore(_logger, _project.FullPath, restoreInfo);

            try
            {
                return await _solutionRestoreService.NominateProjectAsync(_project.FullPath, restoreInfo, cancellationToken);
            }
            finally
            {
                CodeMarkers.Instance.CodeMarker(CodeMarkerTimerId.PerfPackageRestoreEnd);

                RestoreLogger.EndNominateRestore(_logger, _project.FullPath);
            }
        }

        private RestoreData CreateRestoreData(ProjectRestoreInfo restoreInfo, bool succeeded)
        {
            string projectAssetsFilePath = restoreInfo.ProjectAssetsFilePath;

            // Restore service gives us a guarantee that the assets file
            // will contain *at least* the changes that we pushed to it.

            if (projectAssetsFilePath.Length == 0)
                return new RestoreData(string.Empty, DateTime.MinValue, succeeded: false);

            DateTime lastWriteTime = _fileSystem.GetLastFileWriteTimeOrMinValueUtc(projectAssetsFilePath);

            return new RestoreData(
                projectAssetsFilePath,
                lastWriteTime,
                succeeded: succeeded && lastWriteTime != DateTime.MinValue);
        }

        public Task LoadAsync()
        {
            _enabled = true;

            EnsureInitialized();

            return Task.CompletedTask;
        }

        public Task UnloadAsync()
        {
            _enabled = false;

            return Task.CompletedTask;
        }

        // NuGet calls this method to wait project to nominate restoring.
        // If the project has no pending restore data, it will return a completed task.
        // Otherwise a task which will be completed once the project nominate the next restore
        // the task will be cancelled, if the project system decide it no longer need restore (for example: the restore state has no change)
        // the task will be failed, if the project system runs into a problem, so it cannot get correct data to nominate a restore (DT build failed)
        public Task WhenNominated(CancellationToken cancellationToken)
        {
            lock (SyncObject)
            {
                if (!CheckIfHasPendingNomination() || cancellationToken.IsCancellationRequested)
                {
                    return Task.CompletedTask;
                }

                _whenNominatedTask ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            var continuationTask = _whenNominatedTask.Task.ContinueWith(t =>
            {
                lock (SyncObject)
                {
                    if (t.IsFaulted)
                    {
                        _whenNominatedTask.SetException(t.Exception);
                    }
                    else
                    {
                        _whenNominatedTask.TrySetCanceled();
                    }
                }
            }, cancellationToken,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

            return continuationTask;
        }

        // True means the project system plans to call NominateProjectAsync in the future.
        bool IVsProjectRestoreInfoSource.HasPendingNomination
        {
            get => CheckIfHasPendingNomination();
        }

        private bool CheckIfHasPendingNomination()
        {
            lock (SyncObject)
            {
                Assumes.Present(_project.Services.ActiveConfiguredProjectProvider);
                Assumes.Present(_project.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject);

                if (SourceBlock.Completion.IsFaulted)
                {
                    return false;
                }

                // Avoid possible deadlock.
                // Because RestoreCoreAsync() is called inside a dataflow block it will not be called with new data
                // until the old task finishes. So, if the project gets nominating restore, it will not get updated data.
                if (IsNugetRestoreFinished())
                {
                    return false;
                }

                ConfiguredProject? activeConfiguredProject = _project.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject;

                // Nuget should wait until the project at least nominates once.
                if (!_savedNominatedConfiguredVersion.ContainsKey(activeConfiguredProject.ProjectConfiguration))
                {
                    return true;
                }

                // Nuget should not wait for projects that failed DTB
                if (SourceBlock.Completion.IsCompleted)
                {
                    return false;
                }

                // After the first nomination, we should check the saved nominated version
                return IsSavedNominationEmptyOrOlder(activeConfiguredProject);
            }
        }

        private bool IsNugetRestoreFinished()
        {
            // TODO : if NominateForRestoreAsync() has not finished, return false
            return !_nugetRestoreStarted;
        }

        private bool IsSavedNominationEmptyOrOlder(ConfiguredProject activeConfiguredProject)
        {
            if (!_savedNominatedConfiguredVersion.TryGetValue(activeConfiguredProject.ProjectConfiguration,
                out IComparable latestSavedVersionForActiveConfiguredProject))
            {
                return true;
            }

            if (latestSavedVersionForActiveConfiguredProject.IsLaterThan(activeConfiguredProject.ProjectVersion))
            {
                return true;
            }

            foreach(var loadedProject in activeConfiguredProject.UnconfiguredProject.LoadedConfiguredProjects)
            {
                if (_savedNominatedConfiguredVersion.TryGetValue(loadedProject.ProjectConfiguration, out IComparable savedProjectVersion))
                {
                    if (savedProjectVersion.IsLaterThan(loadedProject.ProjectVersion))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
