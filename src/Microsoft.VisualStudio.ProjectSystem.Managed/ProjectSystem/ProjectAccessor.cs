// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

#pragma warning disable RS0030 // symbol IProjectLockService is banned

namespace Microsoft.VisualStudio.ProjectSystem
{
    /// <summary>
    ///     Provides an implementation of <see cref="IProjectAccessor"/> that delegates onto
    ///     the <see cref="IProjectLockService"/>.
    /// </summary>
    [Export(typeof(IProjectAccessor))]
    internal class ProjectAccessor : IProjectAccessor
    {
        private readonly IProjectLockService _projectLockService;

        [ImportingConstructor]
        public ProjectAccessor(IProjectLockService projectLockService)
        {
            _projectLockService = projectLockService;
        }

        public Task<TResult> OpenProjectForReadAsync<TResult>(ConfiguredProject project, Func<Project, TResult> action, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(project, nameof(project));
            Requires.NotNull(project, nameof(action));

            return _projectLockService.ReadLockAsync(async access =>
            {
                Project evaluatedProject = await access.GetProjectAsync(project, cancellationToken);

                // Deliberately not async to reduce the type of
                // code you can run while holding the lock.
                return action(evaluatedProject);
            }, cancellationToken);
        }

        public Task<TResult> OpenProjectForUpgradeableReadAsync<TResult>(ConfiguredProject project, Func<Project, TResult> action, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(project, nameof(project));
            Requires.NotNull(project, nameof(action));

            return _projectLockService.UpgradeableReadLockAsync(async access =>
            {
                Project evaluatedProject = await access.GetProjectAsync(project, cancellationToken);

                // Deliberately not async to reduce the type of
                // code you can run while holding the lock.
                return action(evaluatedProject);
            }, cancellationToken);
        }

        public Task<TResult> OpenProjectXmlForReadAsync<TResult>(UnconfiguredProject project, Func<ProjectRootElement, TResult> action, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(project, nameof(project));
            Requires.NotNull(project, nameof(action));

            return _projectLockService.ReadLockAsync(async access =>
            {
                ProjectRootElement rootElement = await access.GetProjectXmlAsync(project.FullPath, cancellationToken);

                // Deliberately not async to reduce the type of
                // code you can run while holding the lock.
                return action(rootElement);
            }, cancellationToken);
        }

        public async Task OpenProjectXmlForUpgradeableReadAsync(UnconfiguredProject project, Func<ProjectRootElement, CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(project, nameof(project));
            Requires.NotNull(project, nameof(action));

            await _projectLockService.UpgradeableReadLockAsync(async access =>
            {
                ProjectRootElement rootElement = await access.GetProjectXmlAsync(project.FullPath, cancellationToken);

                // Only async to let the caller upgrade to a 
                // write lock via OpenProjectXmlForWriteAsync
                await action(rootElement, cancellationToken);
            }, cancellationToken);
        }

        public async Task OpenProjectXmlForWriteAsync(UnconfiguredProject project, Action<ProjectRootElement> action, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(project, nameof(project));
            Requires.NotNull(project, nameof(action));

            await _projectLockService.WriteLockAsync(async access =>
            {
                await access.CheckoutAsync(project.FullPath);

                ProjectRootElement rootElement = await access.GetProjectXmlAsync(project.FullPath, cancellationToken);

                // Deliberately not async to reduce the type of
                // code you can run while holding the lock.
                action(rootElement);

                // Avoid blocking thread on Dispose
                await access.ReleaseAsync();
            }, cancellationToken);
        }

        public async Task OpenProjectForWriteAsync(ConfiguredProject project, Action<Project> action, ProjectCheckoutOption option = ProjectCheckoutOption.Checkout, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(project, nameof(project));
            Requires.NotNull(project, nameof(action));

            await _projectLockService.WriteLockAsync(async access =>
            {
                if (option == ProjectCheckoutOption.Checkout)
                {
                    await access.CheckoutAsync(project.UnconfiguredProject.FullPath);
                }

                Project evaluatedProject = await access.GetProjectAsync(project, cancellationToken);

                // Deliberately not async to reduce the type of
                // code you can run while holding the lock.
                action(evaluatedProject);

                // Avoid blocking thread on Dispose
                await access.ReleaseAsync();
            }, cancellationToken);
        }
    }
}
