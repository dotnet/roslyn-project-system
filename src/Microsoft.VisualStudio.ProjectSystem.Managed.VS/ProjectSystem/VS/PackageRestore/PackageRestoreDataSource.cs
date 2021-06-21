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

        private readonly Dictionary<string, IComparable> _savedNominatedVersion = new ();
        private readonly AsyncManualResetEvent _nominateNextRestore = new(false);

        public string Name => _project.FullPath;

        public bool FirstNominationOcurred
        {
            get
            {
                Assumes.Present(_project.Services.ActiveConfiguredProjectProvider);
                Assumes.Present(_project.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject);

                ConfiguredProject? activeConfiguredProject = _project.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject;

                // Nuget should wait until the project at least nominates once.
                return _savedNominatedVersion.ContainsKey(activeConfiguredProject.ProjectConfiguration.Name);
            }
        }

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
            JoinableTask joinableTask = JoinableFactory.RunAsync(() =>
            {
                // We should register before any nuget restore or before the solution load.
                return _solutionRestoreService4.RegisterRestoreInfoSourceAsync(this, CancellationToken.None);
            });

            joinableTask.Join();

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

            // Restore service always does work regardless of whether the value we pass 
            // them to actually contains changes, only nominate if there are any.
            byte[] hash = RestoreHasher.CalculateHash(restoreInfo);

            if (_latestHash != null && Enumerable.SequenceEqual(hash, _latestHash))
            {
                return true;
            }

            SaveNominatedConfiguredVersions(value.ConfiguredInputs);
            _latestHash = hash;

            JoinableTask<bool> joinableTask = JoinableFactory.RunAsync(() =>
            {
                return NominateForRestoreAsync(restoreInfo, _projectAsynchronousTasksService.UnloadCancellationToken);
            });

            _projectAsynchronousTasksService.RegisterAsyncTask(joinableTask,
                                                               ProjectCriticalOperation.Build | ProjectCriticalOperation.Unload | ProjectCriticalOperation.Rename,
                                                               registerFaultHandler: true);

            // Prevent overlap until Restore completes
            bool success = await joinableTask;

            HintProjectDependentFile(restoreInfo);

            return success;
        }

        private void SaveNominatedConfiguredVersions(IReadOnlyCollection<PackageRestoreConfiguredInput> configuredInputs)
        {
            Assumes.Present(_project.Services.ActiveConfiguredProjectProvider);
            Assumes.Present(_project.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject);

            _nominateNextRestore.Set();

            ConfiguredProject activeConfiguredProject = _project.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject;

            // Delete non existing configured projects from _savedNominatedVersion
            foreach (var configuredInput in configuredInputs)
            {
                // Only save the active configured project version that gets nominated
                if (string.Compare(configuredInput.ProjectConfiguration.Name, activeConfiguredProject.ProjectConfiguration.Name, StringComparisons.ConfigurationDimensionNames) == 0)
                {
                    _savedNominatedVersion[configuredInput.ProjectConfiguration.Name] = configuredInput.ConfiguredProjectVersion;
                }
            }

            CleanUpSavedConfiguredProjectVersions(configuredInputs);
        }

        private void CleanUpSavedConfiguredProjectVersions(IReadOnlyCollection<PackageRestoreConfiguredInput> configuredInputs)
        {
            foreach (string configuredProject in _savedNominatedVersion.Keys)
            {
                var output = configuredInputs.FirstOrDefault(a => string.Compare(a.ProjectConfiguration.Name, configuredProject,
                    StringComparisons.ConfigurationDimensionNames) == 0);
                if (output is null)
                {
                    _savedNominatedVersion.Remove(configuredProject);
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
            if (!CheckIfHasPendingNomination() || cancellationToken.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            TaskCompletionSource<Task> taskCompletionSource = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<Task> tcs1 = taskCompletionSource.Task;

            // Start a background task that will complete tcs1.Task
            TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            _ = Task.Factory.StartNew((Action)(async () =>
                    {
                        await _nominateNextRestore.WaitAsync();
                        taskCompletionSource.SetResult(Task.CompletedTask);
                    }),
                cancellationToken,
                TaskCreationOptions.None,
                uiScheduler);

            // Prevent overlap until Restore completes
            return joinableTask.Task;
        }

        // True means the project system plans to call NominateProjectAsync in the future.
        bool IVsProjectRestoreInfoSource.HasPendingNomination
        {
            get
            {
                return CheckIfHasPendingNomination();
            }
        }

        private bool CheckIfHasPendingNomination()
        {
            Assumes.Present(_project.Services.ActiveConfiguredProjectProvider);
            Assumes.Present(_project.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject);

            ConfiguredProject? activeConfiguredProject = _project.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject;

            // Nuget should wait until the project at least nominates once.
            if (!_savedNominatedVersion.ContainsKey(activeConfiguredProject.ProjectConfiguration.Name))
            {
                return true;
            }

            // Nuget should not wait for projects that failed DTB
            if (SourceBlock.Completion.IsFaulted || SourceBlock.Completion.IsCompleted)
            {
                return false;
            }

            // After the first nomination, we should check the saved nominated version
            return CheckIfSavedNominationEmptyOrOlder(activeConfiguredProject);
        }

        private bool CheckIfSavedNominationEmptyOrOlder(ConfiguredProject activeConfiguredProject)
        {
            if (!_savedNominatedVersion.TryGetValue(activeConfiguredProject.ProjectConfiguration.Name,
                out IComparable latestConfiguredProjectVersion))
            {
                return true;
            }

            if (latestConfiguredProjectVersion.IsLaterThan(activeConfiguredProject.ProjectVersion))
            {
                return true;
            }

            return false;
        }
    }
}
