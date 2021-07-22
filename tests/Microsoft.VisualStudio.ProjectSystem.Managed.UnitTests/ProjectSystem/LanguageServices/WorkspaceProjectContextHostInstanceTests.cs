// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServices.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Build;
using Moq;
using Xunit;
using static Microsoft.VisualStudio.ProjectSystem.LanguageServices.WorkspaceProjectContextHost;

namespace Microsoft.VisualStudio.ProjectSystem.LanguageServices
{
    public class WorkspaceProjectContextHostInstanceTests
    {
        [Fact]
        public async Task Dispose_WhenNotInitialized_DoesNotThrow()
        {
            var instance = CreateInstance();

            await instance.DisposeAsync();

            Assert.True(instance.IsDisposed);
        }

        [Fact]
        public async Task Dispose_WhenInitializedWithNoContext_DoesNotThrow()
        {
            var workspaceProjectContextProvider = IWorkspaceProjectContextProviderFactory.ImplementCreateProjectContextAsync(accessor: null!);

            var instance = await CreateInitializedInstanceAsync(workspaceProjectContextProvider: workspaceProjectContextProvider);

            await instance.DisposeAsync();

            Assert.True(instance.IsDisposed);
        }

        [Fact]
        public async Task OpenContextForWriteAsync_WhenInitializedWithNoContext_ThrowsOperationCanceled()
        {
            var workspaceProjectContextProvider = IWorkspaceProjectContextProviderFactory.ImplementCreateProjectContextAsync(accessor: null!);

            var instance = await CreateInitializedInstanceAsync(workspaceProjectContextProvider: workspaceProjectContextProvider);

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
            {
                return instance.OpenContextForWriteAsync(context => Task.CompletedTask);
            });
        }

        [Fact]
        public async Task OpenContextForWriteAsync_WhenInitializedWithContext_CallsAction()
        {
            var accessor = IWorkspaceProjectContextAccessorFactory.Create();
            var workspaceProjectContextProvider = IWorkspaceProjectContextProviderFactory.ImplementCreateProjectContextAsync(accessor);

            var instance = await CreateInitializedInstanceAsync(workspaceProjectContextProvider: workspaceProjectContextProvider);

            IWorkspaceProjectContextAccessor? result = null;
            await instance.OpenContextForWriteAsync(a => { result = a; return Task.CompletedTask; });

            Assert.Same(accessor, result);
        }

        [Fact]
        public async Task OpenContextForWriteAsync_WhenProjectUnloaded_ThrowsOperationCanceled()
        {
            var tasksService = IUnconfiguredProjectTasksServiceFactory.ImplementUnloadCancellationToken(new CancellationToken(canceled: true));
            var accessor = IWorkspaceProjectContextAccessorFactory.Create();
            var workspaceProjectContextProvider = IWorkspaceProjectContextProviderFactory.ImplementCreateProjectContextAsync(accessor);

            var instance = await CreateInitializedInstanceAsync(workspaceProjectContextProvider: workspaceProjectContextProvider, tasksService: tasksService);

            int callCount = 0;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            {
                return instance.OpenContextForWriteAsync(c => { callCount++; return Task.CompletedTask; });
            });

            Assert.Equal(0, callCount);
        }

        [Fact]
        public async Task InitializedAsync_WhenInitializedWithContext_RegistersContextWithTracker()
        {
            string? contextIdResult = null;
            var activeWorkspaceProjectContextTracker = IActiveEditorContextTrackerFactory.ImplementRegisterContext(id => { contextIdResult = id; });

            var context = IWorkspaceProjectContextMockFactory.Create();
            var accessor = IWorkspaceProjectContextAccessorFactory.ImplementContext(context, "ContextId");
            var provider = new WorkspaceProjectContextProviderMock();
            provider.ImplementCreateProjectContextAsync(project => accessor);

            await CreateInitializedInstanceAsync(workspaceProjectContextProvider: provider.Object, activeWorkspaceProjectContextTracker: activeWorkspaceProjectContextTracker);

            Assert.Equal("ContextId", contextIdResult);
        }

        [Fact]
        public async Task Dispose_WhenInitializedWithContext_ReleasesContext()
        {
            var accessor = IWorkspaceProjectContextAccessorFactory.Create();

            IWorkspaceProjectContextAccessor? result = null;
            var provider = new WorkspaceProjectContextProviderMock();
            provider.ImplementCreateProjectContextAsync(project => accessor);
            provider.ImplementReleaseProjectContextAsync(a => { result = a; });

            var instance = await CreateInitializedInstanceAsync(workspaceProjectContextProvider: provider.Object);

            await instance.DisposeAsync();

            Assert.Same(accessor, result);
        }

        [Fact]
        public async Task Dispose_WhenInitializedWithContext_UnregistersContextWithTracker()
        {
            string? result = null;
            var activeWorkspaceProjectContextTracker = IActiveEditorContextTrackerFactory.ImplementUnregisterContext(c => { result = c; });

            var context = IWorkspaceProjectContextMockFactory.Create();
            var accessor = IWorkspaceProjectContextAccessorFactory.ImplementContext(context, "ContextId");
            var provider = new WorkspaceProjectContextProviderMock();
            provider.ImplementCreateProjectContextAsync(project => accessor);

            var instance = await CreateInitializedInstanceAsync(workspaceProjectContextProvider: provider.Object, activeWorkspaceProjectContextTracker: activeWorkspaceProjectContextTracker);

            await instance.DisposeAsync();

            Assert.Equal("ContextId", result);
        }

        [Theory]
        [InlineData(WorkspaceContextHandlerType.Evaluation)]
        [InlineData(WorkspaceContextHandlerType.ProjectBuild)]
        [InlineData(WorkspaceContextHandlerType.SourceItems)]
        internal async Task OnProjectChangedAsync_WhenProjectUnloaded_TriggersCancellation(WorkspaceContextHandlerType handlerType)
        {
            var unloadSource = new CancellationTokenSource();
            var tasksService = IUnconfiguredProjectTasksServiceFactory.ImplementUnloadCancellationToken(unloadSource.Token);

            void ApplyProjectBuild(IProjectVersionedValue<IProjectSubscriptionUpdate> _, IProjectBuildSnapshot projectBuildSnapshot, ContextState __, CancellationToken cancellationToken)
            {
                // Unload project
                unloadSource.Cancel();

                cancellationToken.ThrowIfCancellationRequested();
            }

            void ApplyProjectEvaluation(IProjectVersionedValue<IProjectSubscriptionUpdate> _, ContextState __, CancellationToken cancellationToken)
            {
                // Unload project
                unloadSource.Cancel();

                cancellationToken.ThrowIfCancellationRequested();
            }

            var applyChangesToWorkspaceContext = handlerType switch
            {
                WorkspaceContextHandlerType.Evaluation => IApplyChangesToWorkspaceContextFactory.ImplementApplyProjectEvaluationAsync(ApplyProjectEvaluation),
                WorkspaceContextHandlerType.ProjectBuild => IApplyChangesToWorkspaceContextFactory.ImplementApplyProjectBuildAsync(ApplyProjectBuild),
                WorkspaceContextHandlerType.SourceItems => IApplyChangesToWorkspaceContextFactory.ImplementApplySourceItemsAsync(ApplyProjectEvaluation), // ApplyProjectEvaluation works for source items as they share a signature
                _ => throw new NotImplementedException()
            };

            var instance = await CreateInitializedInstanceAsync(tasksService: tasksService, applyChangesToWorkspaceContext: applyChangesToWorkspaceContext);

            var update = IProjectVersionedValueFactory.Create<(ConfiguredProject, IProjectSubscriptionUpdate, IProjectBuildSnapshot)>(default);
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
            {
                return instance.OnProjectChangedAsync(update, handlerType);
            });
        }

        [Theory]
        [InlineData(WorkspaceContextHandlerType.Evaluation)]
        [InlineData(WorkspaceContextHandlerType.ProjectBuild)]
        [InlineData(WorkspaceContextHandlerType.SourceItems)]
        internal async Task OnProjectChangedAsync_WhenInstanceDisposed_TriggersCancellation(WorkspaceContextHandlerType handlerType)
        {
            WorkspaceProjectContextHostInstance? instance = null;

            void ApplyProjectBuild(IProjectVersionedValue<IProjectSubscriptionUpdate> _, IProjectBuildSnapshot buildSnapshot, ContextState __, CancellationToken cancellationToken)
            {
                // Dispose the instance underneath us
                instance!.Dispose();

                cancellationToken.ThrowIfCancellationRequested();
            }

            void ApplyProjectEvaluation(IProjectVersionedValue<IProjectSubscriptionUpdate> _, ContextState __, CancellationToken cancellationToken)
            {
                // Dispose the instance underneath us
                instance!.Dispose();

                cancellationToken.ThrowIfCancellationRequested();
            }

            var applyChangesToWorkspaceContext = handlerType switch
            {
                WorkspaceContextHandlerType.Evaluation => IApplyChangesToWorkspaceContextFactory.ImplementApplyProjectEvaluationAsync(ApplyProjectEvaluation),
                WorkspaceContextHandlerType.ProjectBuild => IApplyChangesToWorkspaceContextFactory.ImplementApplyProjectBuildAsync(ApplyProjectBuild),
                WorkspaceContextHandlerType.SourceItems => IApplyChangesToWorkspaceContextFactory.ImplementApplySourceItemsAsync(ApplyProjectEvaluation), // ApplyProjectEvaluation works for source items as they share a signature
                _ => throw new NotImplementedException()
            };

            instance = await CreateInitializedInstanceAsync(applyChangesToWorkspaceContext: applyChangesToWorkspaceContext);

            var update = IProjectVersionedValueFactory.Create<(ConfiguredProject, IProjectSubscriptionUpdate, IProjectBuildSnapshot)>(default);
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
            {
                return instance.OnProjectChangedAsync(update, handlerType);
            });
        }

        [Theory]
        [InlineData(WorkspaceContextHandlerType.Evaluation)]
        [InlineData(WorkspaceContextHandlerType.ProjectBuild)]
        [InlineData(WorkspaceContextHandlerType.SourceItems)]
        internal async Task OnProjectChangedAsync_PassesProjectUpdate(WorkspaceContextHandlerType handlerType)
        {
            IProjectVersionedValue<IProjectSubscriptionUpdate>? subscriptionResult = null;

            void ApplyProjectBuild(IProjectVersionedValue<IProjectSubscriptionUpdate> u, IProjectBuildSnapshot projectBuildSnapshot, ContextState _, CancellationToken __)
            {
                subscriptionResult = u;
            }

            void ApplyProjectEvaluation(IProjectVersionedValue<IProjectSubscriptionUpdate> u, ContextState _, CancellationToken __)
            {
                subscriptionResult = u;
            }

            var applyChangesToWorkspaceContext = handlerType switch
            {
                WorkspaceContextHandlerType.Evaluation => IApplyChangesToWorkspaceContextFactory.ImplementApplyProjectEvaluationAsync(ApplyProjectEvaluation),
                WorkspaceContextHandlerType.ProjectBuild => IApplyChangesToWorkspaceContextFactory.ImplementApplyProjectBuildAsync(ApplyProjectBuild),
                WorkspaceContextHandlerType.SourceItems => IApplyChangesToWorkspaceContextFactory.ImplementApplySourceItemsAsync(ApplyProjectEvaluation), // ApplyProjectEvaluation works for source items as they share a signature
                _ => throw new NotImplementedException()
            };

            var instance = await CreateInitializedInstanceAsync(applyChangesToWorkspaceContext: applyChangesToWorkspaceContext);

            var buildSnapshot = Mock.Of<IProjectBuildSnapshot>();
            var subscription = IProjectSubscriptionUpdateFactory.CreateEmpty();
            var update = IProjectVersionedValueFactory.Create<(ConfiguredProject, IProjectSubscriptionUpdate, IProjectBuildSnapshot)>((null!, subscription, buildSnapshot));
            await instance.OnProjectChangedAsync(update, handlerType);

            Assert.Same(subscriptionResult!.Value, subscription);
        }

        [Theory] // Evaluation/Project Build       IsActiveContext
        [InlineData(WorkspaceContextHandlerType.Evaluation, true)]
        [InlineData(WorkspaceContextHandlerType.Evaluation, false)]
        [InlineData(WorkspaceContextHandlerType.ProjectBuild, true)]
        [InlineData(WorkspaceContextHandlerType.ProjectBuild, false)]
        [InlineData(WorkspaceContextHandlerType.SourceItems, true)]
        [InlineData(WorkspaceContextHandlerType.SourceItems, false)]
        internal async Task OnProjectChangedAsync_RespectsIsActiveContext(WorkspaceContextHandlerType handlerType, bool isActiveContext)
        {
            bool? isActiveContextResult = null;

            void ApplyProjectBuild(IProjectVersionedValue<IProjectSubscriptionUpdate> u, IProjectBuildSnapshot projectBuildSnapshot, ContextState iac, CancellationToken _)
            {
                isActiveContextResult = iac.IsActiveEditorContext;
            }

            void ApplyProjectEvaluation(IProjectVersionedValue<IProjectSubscriptionUpdate> u, ContextState iac, CancellationToken _)
            {
                isActiveContextResult = iac.IsActiveEditorContext;
            }

            var activeWorkspaceProjectContextTracker = IActiveEditorContextTrackerFactory.ImplementIsActiveEditorContext(context => isActiveContext);
            var applyChangesToWorkspaceContext = handlerType switch
            {
                WorkspaceContextHandlerType.Evaluation => IApplyChangesToWorkspaceContextFactory.ImplementApplyProjectEvaluationAsync(ApplyProjectEvaluation),
                WorkspaceContextHandlerType.ProjectBuild => IApplyChangesToWorkspaceContextFactory.ImplementApplyProjectBuildAsync(ApplyProjectBuild),
                WorkspaceContextHandlerType.SourceItems => IApplyChangesToWorkspaceContextFactory.ImplementApplySourceItemsAsync(ApplyProjectEvaluation), // ApplyProjectEvaluation works for source items as they share a signature
                _ => throw new NotImplementedException()
            };

            var instance = await CreateInitializedInstanceAsync(applyChangesToWorkspaceContext: applyChangesToWorkspaceContext, activeWorkspaceProjectContextTracker: activeWorkspaceProjectContextTracker);

            var update = IProjectVersionedValueFactory.Create<(ConfiguredProject, IProjectSubscriptionUpdate, IProjectBuildSnapshot)>(default);
            await instance.OnProjectChangedAsync(update, handlerType);

            Assert.Equal(isActiveContext, isActiveContextResult);
        }

        private static async Task<WorkspaceProjectContextHostInstance> CreateInitializedInstanceAsync(ConfiguredProject? project = null, IProjectThreadingService? threadingService = null, IUnconfiguredProjectTasksService? tasksService = null, IProjectSubscriptionService? projectSubscriptionService = null, IActiveEditorContextTracker? activeWorkspaceProjectContextTracker = null, IWorkspaceProjectContextProvider? workspaceProjectContextProvider = null, IApplyChangesToWorkspaceContext? applyChangesToWorkspaceContext = null)
        {
            var instance = CreateInstance(project, threadingService, tasksService, projectSubscriptionService, activeWorkspaceProjectContextTracker, workspaceProjectContextProvider, applyChangesToWorkspaceContext);

            await instance.InitializeAsync();

            return instance;
        }

        private static WorkspaceProjectContextHostInstance CreateInstance(ConfiguredProject? project = null, IProjectThreadingService? threadingService = null, IUnconfiguredProjectTasksService? tasksService = null, IProjectSubscriptionService? projectSubscriptionService = null, IActiveEditorContextTracker? activeEditorContextTracker = null, IWorkspaceProjectContextProvider? workspaceProjectContextProvider = null, IApplyChangesToWorkspaceContext? applyChangesToWorkspaceContext = null)
        {
            project ??= ConfiguredProjectFactory.Create();
            threadingService ??= IProjectThreadingServiceFactory.Create();
            tasksService ??= IUnconfiguredProjectTasksServiceFactory.Create();
            projectSubscriptionService ??= IProjectSubscriptionServiceFactory.Create();
            activeEditorContextTracker ??= IActiveEditorContextTrackerFactory.Create();
            workspaceProjectContextProvider ??= IWorkspaceProjectContextProviderFactory.ImplementCreateProjectContextAsync(IWorkspaceProjectContextAccessorFactory.Create());
            applyChangesToWorkspaceContext ??= IApplyChangesToWorkspaceContextFactory.Create();
            IActiveConfiguredProjectProvider activeConfiguredProjectProvider = IActiveConfiguredProjectProviderFactory.Create();
            IDataProgressTrackerService dataProgressTrackerService = IDataProgressTrackerServiceFactory.Create();
            IProjectBuildSnapshotService projectBuildSnapshotService = IProjectBuildSnapshotServiceFactory.Create();

            return new WorkspaceProjectContextHostInstance(project,
                                                           threadingService,
                                                           tasksService,
                                                           projectSubscriptionService,
                                                           workspaceProjectContextProvider,
                                                           activeEditorContextTracker,
                                                           activeConfiguredProjectProvider,
                                                           ExportFactoryFactory.ImplementCreateValueWithAutoDispose(() => applyChangesToWorkspaceContext),
                                                           dataProgressTrackerService,
                                                           projectBuildSnapshotService);
        }
    }
}
