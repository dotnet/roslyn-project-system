// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Telemetry;
using StageId = Microsoft.VisualStudio.ProjectSystem.OperationProgress.OperationProgressStageId;

namespace Microsoft.VisualStudio.ProjectSystem.VS.PackageRestore
{
    internal partial class PackageRestoreProgressTracker
    {
        internal class PackageRestoreProgressTrackerInstance : OnceInitializedOnceDisposedAsync, IMultiLifetimeInstance, IJoinableProjectValueDataSource, IProgressTrackerOutputDataSource
        {
            // The steps leading up to and during restore are the following:
            //
            //      1) Evaluation
            //      2) Design-Time build (call CollectPackageReferences, et all)
            //      3) Push package references & restore data to NuGet ("Nominate")
            //      4) If assets file updated during restore, repeat above
            //
            // It can take two cycles of above (during first open, or when assets file is out of 
            // date) before we have a design-time build that contains all the references that a 
            // project depends on, up to and including mscorlib/System.Runtime.
            // 
            // We want the "IntelliSense" operation progress stage to only be considered completed
            // once we've stopped this cycle, which will let Roslyn, designers and other consumers
            // disable commands, or give indicators that the project is still loading.
            //
            // To figure out when we've finished the cycle, we compare the last write time of the 
            // assets file during the last evaluation against the timestamp of the file on disk just 
            // after restore. If they don't match, we know that we're about to repeat the cycle and 
            // we're still incomplete. Once they match in timestamp, we know that the last design-time 
            // build ran with the latest assets file and we notify operation progress that we're now
            // completed with the results.

            private readonly IProjectFaultHandlerService _projectFaultHandlerService;
            private readonly IDataProgressTrackerService _dataProgressTrackerService;
            private readonly IPackageRestoreDataSource _dataSource;
            private readonly IProjectSubscriptionService _projectSubscriptionService;
            private readonly IConfiguredProjectPackageRestoreTelemetryService _packageReferenceTelemetryService;
            private readonly long _packageRestoreProgressTrackerId;

            private IDataProgressTrackerServiceRegistration? _progressRegistration;
            private IDisposable? _subscription;
            private IDisposable? _joinedDataSources;

            public PackageRestoreProgressTrackerInstance(
                ConfiguredProject project,
                IProjectThreadingService threadingService,
                IProjectFaultHandlerService projectFaultHandlerService,
                IDataProgressTrackerService dataProgressTrackerService,
                IPackageRestoreDataSource dataSource,
                IProjectSubscriptionService projectSubscriptionService,
                IConfiguredProjectPackageRestoreTelemetryService packageReferenceTelemetryService,
                long packageRestoreProgressTrackerId)
                : base(threadingService.JoinableTaskContext)
            {
                ConfiguredProject = project;
                _projectFaultHandlerService = projectFaultHandlerService;
                _dataProgressTrackerService = dataProgressTrackerService;
                _dataSource = dataSource;
                _projectSubscriptionService = projectSubscriptionService;
                _packageReferenceTelemetryService = packageReferenceTelemetryService;
                _packageRestoreProgressTrackerId = packageRestoreProgressTrackerId;
            }

            public ConfiguredProject ConfiguredProject { get; }

            public string? OperationProgressStageId => StageId.IntelliSense;

            public string Name => nameof(PackageRestoreProgressTracker);

            public string DisplayMessage => string.Empty;

            public Task InitializeAsync()
            {
                return InitializeAsync(CancellationToken.None);
            }

            protected override Task InitializeCoreAsync(CancellationToken cancellationToken)
            {
                _joinedDataSources = ProjectDataSources.JoinUpstreamDataSources(JoinableFactory, _projectFaultHandlerService, _projectSubscriptionService.ProjectSource, _dataSource);

                _progressRegistration = _dataProgressTrackerService.RegisterOutputDataSource(this);

                Action<IProjectVersionedValue<ValueTuple<IProjectSnapshot, RestoreData>>> action = OnRestoreCompleted;

                _subscription = ProjectDataSources.SyncLinkTo(
                    _projectSubscriptionService.ProjectSource.SourceBlock.SyncLinkOptions(),
                    _dataSource.SourceBlock.SyncLinkOptions(),
                        DataflowBlockFactory.CreateActionBlock(action, ConfiguredProject.UnconfiguredProject, ProjectFaultSeverity.LimitedFunctionality),
                        linkOptions: DataflowOption.PropagateCompletion,
                        cancellationToken: cancellationToken);

                _packageReferenceTelemetryService.PostPackageRestoreEvent(PackageRestoreOperationNames.PackageRestoreProgressTrackerInstanceInitialized, _packageRestoreProgressTrackerId);

                return Task.CompletedTask;
            }

            internal void OnRestoreCompleted(IProjectVersionedValue<ValueTuple<IProjectSnapshot, RestoreData>> value)
            {
                RestoreData restoreData = value.Value.Item2;
                bool isRestoreUpToDate = IsRestoreUpToDate(value.Value.Item1, restoreData);

                if (isRestoreUpToDate)
                {
                    _progressRegistration!.NotifyOutputDataCalculated(value.DataSourceVersions);
                    _packageReferenceTelemetryService.PostPackageRestoreCompletedEvent(restoreData.Succeeded, _packageRestoreProgressTrackerId);
                }
            }

            private static bool IsRestoreUpToDate(IProjectSnapshot projectSnapshot, RestoreData restoreData)
            {
                // If restore failed, we treat as though it is up-to-date to avoid it forever being stuck out of date.
                if (!restoreData.Succeeded)
                    return true;

                DateTime lastEvaluationWriteTime = GetLastWriteTimeUtc(restoreData.ProjectAssetsFilePath, projectSnapshot);

                return lastEvaluationWriteTime >= restoreData.ProjectAssetsLastWriteTimeUtc;
            }

            private static DateTime GetLastWriteTimeUtc(string filePath, IProjectSnapshot projectSnapshot)
            {
                var projectSnapshot2 = (IProjectSnapshot2)projectSnapshot;

                // If the assets file wasn't included as part of the <AdditionalDesignTimeBuildInput> item,
                // consider it up-to-date by return MaxValue, so that it is not forever stuck out-of-date.
                return projectSnapshot2.AdditionalDependentFileTimes.GetValueOrDefault(filePath, DateTime.MaxValue);
            }

            protected override Task DisposeCoreAsync(bool initialized)
            {
                _subscription?.Dispose();
                _progressRegistration?.Dispose();
                _joinedDataSources?.Dispose();

                return Task.CompletedTask;
            }

            public IDisposable? Join()
            {
                return JoinableCollection.Join();
            }
        }
    }
}
