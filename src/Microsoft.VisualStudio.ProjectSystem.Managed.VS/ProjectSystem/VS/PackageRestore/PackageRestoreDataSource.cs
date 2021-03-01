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
using Microsoft.VisualStudio.ProjectSystem.Logging;
using Microsoft.VisualStudio.Telemetry;
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
    internal class PackageRestoreDataSource : ChainedProjectValueDataSourceBase<RestoreData>, IPackageRestoreDataSource, IProjectDynamicLoadComponent
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
        private readonly Lazy<IProjectChangeHintSubmissionService> _projectChangeHintSubmissionService;
        private readonly IProjectAccessor _projectAccessor;
        private readonly IProjectLogger _logger;
        private readonly IUnconfiguredProjectPackageRestoreTelemetryService _packageReferenceTelemetryService;

        private byte[]? _latestHash;
        private bool _enabled;

        [ImportingConstructor]
        public PackageRestoreDataSource(
            UnconfiguredProject project,
            IPackageRestoreUnconfiguredInputDataSource dataSource,
            [Import(ExportContractNames.Scopes.UnconfiguredProject)]IProjectAsynchronousTasksService projectAsynchronousTasksService,
            IVsSolutionRestoreService3 solutionRestoreService,
            IFileSystem fileSystem,
            Lazy<IProjectChangeHintSubmissionService> projectChangeHintSubmissionService,
            IProjectAccessor projectAccessor,
            IProjectLogger logger,
            IUnconfiguredProjectPackageRestoreTelemetryService packageReferenceTelemetryService)
            : base(project, synchronousDisposal : true, registerDataSource : false)
        {
            _project = project;
            _dataSource = dataSource;
            _projectAsynchronousTasksService = projectAsynchronousTasksService;
            _solutionRestoreService = solutionRestoreService;
            _fileSystem = fileSystem;
            _projectChangeHintSubmissionService = projectChangeHintSubmissionService;
            _projectAccessor = projectAccessor;
            _logger = logger;
            _packageReferenceTelemetryService = packageReferenceTelemetryService;
        }

        protected override IDisposable? LinkExternalInput(ITargetBlock<IProjectVersionedValue<RestoreData>> targetBlock)
        {
            JoinUpstreamDataSources(_dataSource);

            // Take the unconfigured "restore inputs", send them to NuGet, and then return the result of that restore
            // We make use of TransformMany so that we can opt out of returning
            DisposableValue<ISourceBlock<IProjectVersionedValue<RestoreData>>> transformBlock = _dataSource.SourceBlock.TransformManyWithNoDelta(RestoreAsync);

            transformBlock.Value.LinkTo(targetBlock, DataflowOption.PropagateCompletion);

            _packageReferenceTelemetryService.PostPackageRestoreEvent(PackageRestoreOperationNames.PackageRestoreDataSourceLinkedToExternalInput);

            return transformBlock;
        }

        internal async Task<IEnumerable<IProjectVersionedValue<RestoreData>>> RestoreAsync(IProjectVersionedValue<PackageRestoreUnconfiguredInput> e)
        {
            // No configurations - likely during project close
            if (!_enabled || e.Value.RestoreInfo is null)
                return Enumerable.Empty<IProjectVersionedValue<RestoreData>>();

            bool succeeded = await RestoreCoreAsync(e.Value.RestoreInfo);

            RestoreData restoreData = CreateRestoreData(e.Value.RestoreInfo, succeeded);

            return new[]
            {
                new ProjectVersionedValue<RestoreData>(restoreData, e.DataSourceVersions)
            };
        }

        private async Task<bool> RestoreCoreAsync(ProjectRestoreInfo restoreInfo)
        {
            // Restore service always does work regardless of whether the value we pass 
            // them to actually contains changes, only nominate if there are any.
            byte[] hash = RestoreHasher.CalculateHash(restoreInfo);

            if (_latestHash != null && Enumerable.SequenceEqual(hash, _latestHash))
                return true;

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

            await HintProjectDependentFileAsync(restoreInfo);

            return success;
        }

        private Task HintProjectDependentFileAsync(ProjectRestoreInfo restoreInfo)
        {
            // Hint to CPS that the assets file "might" have changed and therefore
            // reevaluate if it has. It already listens to file-changed events for it, 
            // but can miss them during periods where the buffer is overflowed when 
            // there are lots of changes.
            if (restoreInfo.ProjectAssetsFilePath.Length > 0)
            {
                return _projectAccessor.EnterWriteLockAsync((collection, token) =>
                {
                    var hint = new ProjectChangeFileSystemEntityHint(ContainingProject!,
                                                                     ProjectChangeFileSystemEntityHint.UpdateProjectDependentFile,
                                                                     new[] { restoreInfo.ProjectAssetsFilePath });

                    return _projectChangeHintSubmissionService.Value.HintAsync(hint);
                });
            }

            return Task.CompletedTask;
        }

        private async Task<bool> NominateForRestoreAsync(ProjectRestoreInfo restoreInfo, CancellationToken cancellationToken)
        {
            _packageReferenceTelemetryService.PostPackageRestoreEvent(PackageRestoreOperationNames.BeginNominateRestore);
            RestoreLogger.BeginNominateRestore(_logger, _project.FullPath, restoreInfo);

            bool nominationSucceeded = false;
            try
            {
                nominationSucceeded = await _solutionRestoreService.NominateProjectAsync(_project.FullPath, restoreInfo, cancellationToken);
                return nominationSucceeded;
            }
            finally
            {
                CodeMarkers.Instance.CodeMarker(CodeMarkerTimerId.PerfPackageRestoreEnd);

                RestoreLogger.EndNominateRestore(_logger, _project.FullPath);
                _packageReferenceTelemetryService.PostNominationCompleteEvent(nominationSucceeded);
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

            _packageReferenceTelemetryService.PostPackageRestoreEvent(PackageRestoreOperationNames.PackageRestoreDataSourceLoading);

            EnsureInitialized();

            return Task.CompletedTask;
        }

        public Task UnloadAsync()
        {
            _enabled = false;

            _packageReferenceTelemetryService.PostPackageRestoreEvent(PackageRestoreOperationNames.PackageRestoreDataSourceUnloading);

            return Task.CompletedTask;
        }
    }
}
