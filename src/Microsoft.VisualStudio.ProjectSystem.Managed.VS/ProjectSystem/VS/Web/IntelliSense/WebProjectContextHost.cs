// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Web.Interop;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web.IntelliSense
{
    /// <summary>
    ///     Responsible for hosting a <see cref="IVsWebProjectContext"/> instance 
    ///     for legacy ASP.NET and Razor IntelliSense.
    /// </summary>
    [Export(typeof(IWebProject))]
    [Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectDynamicLoadComponent))]
    [AppliesTo("AspNet")]
    internal partial class WebProjectContextHost : IDisposable, IProjectDynamicLoadComponent, IWebProject
    {
        private readonly IProjectThreadingService _threadingService;
        private readonly UnconfiguredProject _project;
        private readonly IUnconfiguredProjectVsServices _projectVsServices;
        private readonly IUnconfiguredProjectTasksService _projectTasksService;
        private readonly IVsUIService<IVsWebContextService> _webContextService;
        private readonly IWebLaunchSettingsProvider _webLaunchSettingsProvider;
        
        [ImportingConstructor]
        public WebProjectContextHost(
            IProjectThreadingService threadingService,
            UnconfiguredProject project,
            IUnconfiguredProjectVsServices projectVsServices,
            IUnconfiguredProjectTasksService projectTasksService,
            IVsUIService<SWebApplicationCtxSvc, IVsWebContextService> webContextService,
            IWebLaunchSettingsProvider webLaunchSettingsProvider)
        {
            _threadingService = threadingService;
            _project = project;
            _projectVsServices = projectVsServices;
            _projectTasksService = projectTasksService;
            _webContextService = webContextService;
            _webLaunchSettingsProvider = webLaunchSettingsProvider;
        }

        public IWebProjectProperties? Properties { get; private set; }

        public IWebProjectServices? Services { get; private set; }

        public Task LoadAsync()
        {
            _threadingService.RunAndForget(async () =>
            {
                await _projectTasksService.ProjectLoadedInHost;

                // Creating the web context requires these properties, so set them before
                WebLaunchSettings launchSettings = await _webLaunchSettingsProvider.GetLaunchSettingsAsync();
                Properties = new WebProjectProperties(
                    Path.GetDirectoryName(_project.FullPath) + "\\",
                    Path.GetDirectoryName(_project.FullPath) + "\\",
                    launchSettings);
                Services = await CreateWebProjectServicesAsync();

            }, _project, ProjectFaultSeverity.LimitedFunctionality);

            return Task.CompletedTask;
        }

        public async Task UnloadAsync()
        {
            await _threadingService.SwitchToUIThread();

            IWebProjectServices? services = Services;
            if (services != null)
            {
                HResult result = services.Context.CloseProject();
                if (result.Failed)
                    throw result.Exception!;

                Services = null;
                Properties = null;
            }
        }

        public void Dispose()
        {
            _threadingService.ExecuteSynchronously(UnloadAsync);
        }

        private async Task<WebProjectServices> CreateWebProjectServicesAsync()
        {
            await _threadingService.SwitchToUIThread();

            HResult result = _webContextService.Value.GetWebProjectContext(_projectVsServices.VsHierarchy, out IVsWebProjectContext context);
            if (result.Failed)
                throw result.Exception!;

            result = context.OwnerClosesProject(true);
            if (result.Failed)
                throw result.Exception!;

            return new WebProjectServices(_threadingService, context);
        }
    }
}
