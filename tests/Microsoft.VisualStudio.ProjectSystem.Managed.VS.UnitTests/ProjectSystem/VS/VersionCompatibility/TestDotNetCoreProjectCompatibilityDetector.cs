// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.IO;
using Microsoft.VisualStudio.ProjectSystem.VS.Interop;
using Microsoft.VisualStudio.ProjectSystem.VS.UI;
using Microsoft.VisualStudio.ProjectSystem.VS.Utilities;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.ProjectSystem.VS.VersionCompatibility
{
    internal class TestDotNetCoreProjectCompatibilityDetector : DotNetCoreProjectCompatibilityDetector
    {
        private readonly bool _hasNewProjects;
        private readonly bool _usingPreviewSDK;

        public TestDotNetCoreProjectCompatibilityDetector(Lazy<IProjectServiceAccessor> projectAccessor,
                                                          Lazy<IDialogServices> dialogServices,
                                                          Lazy<IProjectThreadingService> threadHandling,
                                                          Lazy<IVsShellUtilitiesHelper> vsShellUtilitiesHelper,
                                                          Lazy<IFileSystem> fileSystem,
                                                          Lazy<IHttpClient> httpClient,
                                                          IVsService<SVsUIShell, IVsUIShell> vsUIShellService,
                                                          IVsService<SVsSettingsPersistenceManager, ISettingsManager> settingsManagerService,
                                                          IVsService<SVsSolution, IVsSolution> vsSolutionService,
                                                          IVsService<SVsAppId, IVsAppId> vsAppIdService,
                                                          IVsService<SVsShell, IVsShell> vsShellService,
                                                          IUnconfiguredProjectCommonServices commonServices,
                                                          bool hasNewProjects = false,
                                                          bool usingPreviewSDK = false) :
            base(projectAccessor, dialogServices, threadHandling, vsShellUtilitiesHelper, fileSystem, httpClient, vsUIShellService, settingsManagerService, vsSolutionService, vsAppIdService, vsShellService, commonServices)
        {
            _hasNewProjects = hasNewProjects;
            _usingPreviewSDK = usingPreviewSDK;
        }

        protected override Task<bool> IsPreviewSDKInUseAsync() => Task.FromResult(_usingPreviewSDK);
        protected override bool IsNewlyCreated(UnconfiguredProject project) => _hasNewProjects;
    }
}
