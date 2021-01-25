// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.IO;
using Microsoft.VisualStudio.ProjectSystem.VS.Interop;
using Microsoft.VisualStudio.ProjectSystem.VS.UI;
using Microsoft.VisualStudio.ProjectSystem.VS.Utilities;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.VS.VersionCompatibility
{
    public class DotNetCoreProjectCompatibilityDetectorTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task InitializeAsync_DotNotShowWarning(bool isSolutionOpen)
        {
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, isSolutionOpen: isSolutionOpen);
            await compatibilityDetector.InitializeAsync();
            Assert.Equal(isSolutionOpen, compatibilityDetector.SolutionOpen);
            compatibilityDetector.Dispose();
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task InitializeAsync_VersionSetCorrectly()
        {
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, vsVersion: new Version("16.9"));
            await compatibilityDetector.InitializeAsync();
            Assert.Equal(new Version("16.9"), compatibilityDetector.VisualStudioVersion);
            compatibilityDetector.Dispose();
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task OnAfterOpenProject_CompatibilityNotSet_DoNotShowWarning()
        {
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, isSolutionOpen: true);
            await compatibilityDetector.InitializeAsync();
            var ivsHierarchy = IVsHierarchyFactory.ImplementAsUnconfiguredProject(UnconfiguredProjectFactory.Create());
            compatibilityDetector.OnAfterOpenProject(ivsHierarchy, fAdded: 1);
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task OnAfterOpenProject_DoNotShowWarning()
        {
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, isSolutionOpen: true);
            await compatibilityDetector.InitializeAsync();
            var ivsHierarchy = IVsHierarchyFactory.ImplementAsUnconfiguredProject(UnconfiguredProjectFactory.Create());
            compatibilityDetector.CompatibilityLevelWarnedForCurrentSolution = DotNetCoreProjectCompatibilityDetector.CompatibilityLevel.NotSupported;
            compatibilityDetector.OnAfterOpenProject(ivsHierarchy, fAdded: 1);
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task OnAfterOpenProject_NewProject_DoNotShowWarning()
        {
            const string targetFrameworkMoniker = ".NETCoreApp,Version=v2.0";
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, isSolutionOpen: true, hasNewProjects: true, targetFrameworkMoniker: targetFrameworkMoniker);
            await compatibilityDetector.InitializeAsync();
            var ivsHierarchy = CreateIVSHierarchy(targetFrameworkMoniker);
            compatibilityDetector.CompatibilityLevelWarnedForCurrentSolution = DotNetCoreProjectCompatibilityDetector.CompatibilityLevel.Supported;
            compatibilityDetector.OnAfterOpenProject(ivsHierarchy, fAdded: 1);
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task OnAfterOpenProject_NewProject_ShowWarning()
        {
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, isSolutionOpen: true, hasNewProjects: true);
            await compatibilityDetector.InitializeAsync();
            var ivsHierarchy = CreateIVSHierarchy();
            compatibilityDetector.CompatibilityLevelWarnedForCurrentSolution = DotNetCoreProjectCompatibilityDetector.CompatibilityLevel.Supported;
            compatibilityDetector.OnAfterOpenProject(ivsHierarchy, fAdded: 1);
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task OnAfterOpenProject_NewProject_Unsupported_ShowWarning()
        {
            const string targetFrameworkMoniker = ".NETCoreApp,Version=v3.1";
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, isSolutionOpen: true, hasNewProjects: true, versionDataString: defaultUnSupportedVersionDataString);
            await compatibilityDetector.InitializeAsync();
            var ivsHierarchy = CreateIVSHierarchy(targetFrameworkMoniker);
            compatibilityDetector.CompatibilityLevelWarnedForCurrentSolution = DotNetCoreProjectCompatibilityDetector.CompatibilityLevel.Supported;
            compatibilityDetector.OnAfterOpenProject(ivsHierarchy, fAdded: 1);
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task OnAfterOpenProject_NewProject_PreviewOn_ShowWarning()
        {
            const string targetFrameworkMoniker = ".NETCoreApp,Version=v3.1";
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, isSolutionOpen: true, hasNewProjects: true, usingPreviewSDK: true, versionDataString: defaultsupportedPreviewVersionDataString);
            await compatibilityDetector.InitializeAsync();
            var ivsHierarchy = CreateIVSHierarchy(targetFrameworkMoniker);
            compatibilityDetector.CompatibilityLevelWarnedForCurrentSolution = DotNetCoreProjectCompatibilityDetector.CompatibilityLevel.Supported;
            compatibilityDetector.OnAfterOpenProject(ivsHierarchy, fAdded: 1);
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task OnAfterOpenProject_NewProject_PreviewOn_DoNotShowWarning()
        {
            const string targetFrameworkMoniker = ".NETCoreApp,Version=v3.0";
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, isSolutionOpen: true, hasNewProjects: true, usingPreviewSDK: true, versionDataString: defaultsupportedPreviewVersionDataString);
            await compatibilityDetector.InitializeAsync();
            var ivsHierarchy = CreateIVSHierarchy(targetFrameworkMoniker);
            compatibilityDetector.OnAfterOpenProject(ivsHierarchy, fAdded: 1);
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task OnAfterOpenProject_NewProject_PreviewOff_ShowWarning()
        {
            const string targetFrameworkMoniker = ".NETCoreApp,Version=v3.0";
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, isSolutionOpen: true, hasNewProjects: true, usingPreviewSDK:false, versionDataString: defaultsupportedPreviewVersionDataString);
            await compatibilityDetector.InitializeAsync();
            var ivsHierarchy = CreateIVSHierarchy(targetFrameworkMoniker);
            compatibilityDetector.OnAfterOpenProject(ivsHierarchy, fAdded: 1);
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task OnAfterBackgroundSolutionLoadComplete_DoNotShowWarning()
        {
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, isSolutionOpen: true);
            await compatibilityDetector.InitializeAsync();
            compatibilityDetector.OnAfterBackgroundSolutionLoadComplete();
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task OnAfterBackgroundSolutionLoadComplete_ShowWarning()
        {
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, isSolutionOpen: true, hasNewProjects: true);
            await compatibilityDetector.InitializeAsync();
            compatibilityDetector.OnAfterBackgroundSolutionLoadComplete();
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task OnAfterCloseSolution_SetSolutionClosedToFalse()
        {
            var compatibilityDetector = CreateCompatibilityDetector(out var dialogServices, isSolutionOpen: true);
            await compatibilityDetector.InitializeAsync();
            Assert.True(compatibilityDetector.SolutionOpen);
            compatibilityDetector.OnAfterCloseSolution(null);
            Assert.False(compatibilityDetector.SolutionOpen);
            Mock.Get(dialogServices).Verify(x => x.DontShowAgainMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        private static DotNetCoreProjectCompatibilityDetector CreateCompatibilityDetector(out IDialogServices dialogServices,
                                                                                          string? versionDataString = null,
                                                                                          Version? vsVersion = null,
                                                                                          bool isSolutionOpen = false,
                                                                                          bool hasNewProjects = false,
                                                                                          bool usingPreviewSDK = false,
                                                                                          string targetFrameworkMoniker = ".NETCoreApp,Version=v3.0")
        {
            dialogServices = IDialogServicesFactory.Create();
            var additionalReference = dialogServices;
            var projectProperties = IProjectPropertiesFactory.CreateWithPropertyAndValue("TargetFrameworkMoniker", targetFrameworkMoniker);
            var propertiesProvider = IProjectPropertiesProviderFactory.Create(commonProps: projectProperties);
            var project = ConfiguredProjectFactory.Create(services: ConfiguredProjectServicesFactory.Create(projectPropertiesProvider: propertiesProvider));
            var scope = hasNewProjects ? IProjectCapabilitiesScopeFactory.Create(new[] { ProjectCapability.DotNet, ProjectCapability.PackageReferences }) : null;
            var projectAccessor = new Lazy<IProjectServiceAccessor>(() => IProjectServiceAccessorFactory.Create(scope, project));
            var lazyDialogServices = new Lazy<IDialogServices>(() => additionalReference);
            var threadHandling = new Lazy<IProjectThreadingService>(() => IProjectThreadingServiceFactory.Create(verifyOnUIThread: false));
            var vsShellUtilitiesHelper = new Lazy<IVsShellUtilitiesHelper>(() => IVsShellUtilitiesHelperFactory.Create(string.Empty, vsVersion ?? new Version("16.1")));
            var fileSystem = new Lazy<IFileSystem>(() => IFileSystemFactory.Create(existsFunc: x => true, readAllTextFunc: x => versionDataString ?? defaultVersionDataString));
            var httpClient = new Lazy<IHttpClient>(() => IHttpClientFactory.Create(versionDataString ?? defaultVersionDataString));
            var vsUIShellService = IVsServiceFactory.Create<SVsUIShell, IVsUIShell>(Mock.Of<IVsUIShell>());
            var settingsManagerService = IVsServiceFactory.Create<SVsSettingsPersistenceManager, ISettingsManager>(Mock.Of<ISettingsManager>());
            var vsSolutionService = IVsServiceFactory.Create<SVsSolution, IVsSolution>(IVsSolutionFactory.CreateWithAdviseUnadviseSolutionEvents(1, isFullyLoaded: isSolutionOpen));
            var vsAppIdService = IVsServiceFactory.Create<SVsAppId, IVsAppId>(Mock.Of<IVsAppId>());
            var vsShellService = IVsServiceFactory.Create<SVsShell, IVsShell>(Mock.Of<IVsShell>());
            var hierarchy = IVsHierarchyFactory.Create();
            var unconfiguredProject = UnconfiguredProjectFactory.Create(hostObject: hierarchy);
            var commonServices = IUnconfiguredProjectCommonServicesFactory.Create(project: unconfiguredProject);

            var compatibilityDetector = new TestDotNetCoreProjectCompatibilityDetector(projectAccessor,
                                                                                       lazyDialogServices,
                                                                                       threadHandling,
                                                                                       vsShellUtilitiesHelper,
                                                                                       fileSystem,
                                                                                       httpClient,
                                                                                       vsUIShellService,
                                                                                       settingsManagerService,
                                                                                       vsSolutionService,
                                                                                       vsAppIdService,
                                                                                       vsShellService,
                                                                                       commonServices,
                                                                                       hasNewProjects,
                                                                                       usingPreviewSDK);
            return compatibilityDetector;
        }

        private static IVsHierarchy CreateIVSHierarchy(string targetFrameworkMoniker = ".NETCoreApp,Version=v3.0")
        {
            var projectProperties = IProjectPropertiesFactory.CreateWithPropertyAndValue("TargetFrameworkMoniker", targetFrameworkMoniker);
            var propertiesProvider = IProjectPropertiesProviderFactory.Create(commonProps: projectProperties);
            var project = ConfiguredProjectFactory.Create(services: ConfiguredProjectServicesFactory.Create(projectPropertiesProvider: propertiesProvider));
            var scope = IProjectCapabilitiesScopeFactory.Create(new[] { ProjectCapability.DotNet, ProjectCapability.PackageReferences });
            var ivsHierarchy = IVsHierarchyFactory.ImplementAsUnconfiguredProject(UnconfiguredProjectFactory.Create(scope: scope, configuredProject: project));
            return ivsHierarchy;
        }

        private const string defaultVersionDataString = @" {
  ""vsVersions"": {
    ""16.1"": {
      ""supportedVersion"": ""3.0"",
      ""openSupportedMessage"": """",
      ""unsupportedVersion"": ""3.1"",
      ""unsupportedVersionsInstalledMessage"": """",
      ""openUnsupportedMessage"": """"
    }
  }
}";

        private const string defaultUnSupportedVersionDataString = @" {
  ""vsVersions"": {
    ""16.1"": {
      ""unsupportedVersion"": ""3.1"",
      ""unsupportedVersionsInstalledMessage"": """",
    }
  }
}";

        private const string defaultsupportedPreviewVersionDataString = @" {
  ""vsVersions"": {
    ""16.1"": {
      ""supportedPreviewVersion"": ""3.0"",
      ""openSupportedPreviewMessage"": """",
      ""unsupportedVersion"": ""3.0"",
      ""unsupportedVersionsInstalledMessage"": """",
    }
  }
}";
    }
}
