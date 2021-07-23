// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem
{
    [Export]
    [Export(typeof(IUnconfiguredProjectTasksService))]
    [AppliesTo(ProjectCapability.DotNet)]
    internal class UnconfiguredProjectTasksService : IUnconfiguredProjectTasksService
    {
        private readonly IProjectAsynchronousTasksService _tasksService;
        private readonly IProjectThreadingService _threadingService;
        private readonly ILoadedInHostListener? _loadedInHostListener;
        private readonly ISolutionService? _solutionService;
        private readonly TaskCompletionSource _projectLoadedInHost = new();
        private readonly TaskCompletionSource _prioritizedProjectLoadedInHost = new();
        private readonly JoinableTaskCollection _prioritizedTasks;

        [ImportingConstructor]
        public UnconfiguredProjectTasksService(
            [Import(ExportContractNames.Scopes.UnconfiguredProject)]IProjectAsynchronousTasksService tasksService,
            IProjectThreadingService threadingService,
            [Import(AllowDefault = true)] ILoadedInHostListener? loadedInHostListener,
            [Import(AllowDefault = true)] ISolutionService? solutionService)
        {
            _prioritizedTasks = threadingService.JoinableTaskContext.CreateCollection();
            _prioritizedTasks.DisplayName = "PrioritizedProjectLoadedInHostTasks";
            _tasksService = tasksService;
            _threadingService = threadingService;
            _loadedInHostListener = loadedInHostListener;
            _solutionService = solutionService;
        }

        [ProjectAutoLoad(completeBy: ProjectLoadCheckpoint.ProjectFactoryCompleted)]
        [AppliesTo(ProjectCapability.DotNet)]
        public Task OnProjectFactoryCompleted()
        {
            if (_loadedInHostListener != null)
            {
                return _loadedInHostListener.StartListeningAsync();
            }
            else
            {
                _projectLoadedInHost.TrySetResult();
                return Task.CompletedTask;
            }
        }

        public Task ProjectLoadedInHost
        {
            get { return _projectLoadedInHost.Task.WithCancellation(_tasksService.UnloadCancellationToken); }
        }

        public Task PrioritizedProjectLoadedInHost
        {
            get { return _prioritizedProjectLoadedInHost.Task.WithCancellation(_tasksService.UnloadCancellationToken); }
        }

        public Task SolutionLoadedInHost
        {
#pragma warning disable VSTHRD110 // Observe result of async calls
            get { return _solutionService?.LoadedInHost.WithCancellation(_tasksService.UnloadCancellationToken) ?? throw new NotSupportedException(); }
#pragma warning restore VSTHRD110 // Observe result of async calls
        }

        public CancellationToken UnloadCancellationToken
        {
            get { return _tasksService.UnloadCancellationToken; }
        }

#pragma warning disable RS0030 // Do not use LoadedProjectAsync (this is the replacement)
        public Task LoadedProjectAsync(Func<Task> action)
        {
            JoinableTask joinable = _tasksService.LoadedProjectAsync(action);

            return joinable.Task;
        }

        public Task<T> LoadedProjectAsync<T>(Func<Task<T>> action)
        {
            JoinableTask<T> joinable = _tasksService.LoadedProjectAsync(action);

            return joinable.Task;
        }

#pragma warning restore RS0030 // Do not use LoadedProjectAsync

        public Task<T> PrioritizedProjectLoadedInHostAsync<T>(Func<Task<T>> action)
        {
            Requires.NotNull(action, nameof(action));

            _tasksService.UnloadCancellationToken.ThrowIfCancellationRequested();

            JoinableTask<T> task = _threadingService.JoinableTaskFactory.RunAsync(action);

            _prioritizedTasks.Add(task);

            return task.Task;
        }

        public Task PrioritizedProjectLoadedInHostAsync(Func<Task> action)
        {
            Requires.NotNull(action, nameof(action));

            _tasksService.UnloadCancellationToken.ThrowIfCancellationRequested();

            JoinableTask task = _threadingService.JoinableTaskFactory.RunAsync(action);

            _prioritizedTasks.Add(task);

            return task.Task;
        }

        public void OnProjectLoadedInHost()
        {
            _projectLoadedInHost.SetResult();
        }

        public void OnPrioritizedProjectLoadedInHost()
        {
            _prioritizedProjectLoadedInHost.SetResult();

            _threadingService.ExecuteSynchronously(() => _prioritizedTasks.JoinTillEmptyAsync());
        }
    }
}
