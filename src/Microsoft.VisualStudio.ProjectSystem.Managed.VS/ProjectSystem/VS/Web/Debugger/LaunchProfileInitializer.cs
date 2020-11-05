// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem.Debug;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    [Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectDynamicLoadComponent))]
    [AppliesTo(ProjectCapability.AspNetLaunchProfiles)]
    internal partial class LaunchProfileInitializer : AbstractMultiLifetimeComponent<LaunchProfileInitializer.LaunchProfileInitializerInstance>, IProjectDynamicLoadComponent
    {

        private readonly ILaunchSettingsProvider2 _launchSettingsProvider;
        private readonly IUnconfiguredProjectCommonServices _projectServices;
        private readonly IWebServer _webServer;
        private readonly IProjectTreeService _projectTree;

        [ImportingConstructor]
        public LaunchProfileInitializer(
                ILaunchSettingsProvider2 launchSettingsProvider,
                IUnconfiguredProjectCommonServices projectServices,
                IWebServer webServer,
                [Import(ExportContractNames.ProjectTreeProviders.PhysicalProjectTreeService)] IProjectTreeService projectTree)
                : base(projectServices.ThreadingService.JoinableTaskContext)
        {
            _launchSettingsProvider = launchSettingsProvider;
            _projectServices = projectServices;
            _webServer = webServer;
            _projectTree = projectTree;
        }

        protected override LaunchProfileInitializerInstance CreateInstance()
        {
            return new LaunchProfileInitializerInstance(this);
        }
    }
}
