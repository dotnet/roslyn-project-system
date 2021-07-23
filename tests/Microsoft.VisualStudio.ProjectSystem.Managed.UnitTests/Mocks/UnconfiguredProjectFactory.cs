// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;

namespace Microsoft.VisualStudio.ProjectSystem
{
    internal static class UnconfiguredProjectFactory
    {
        public static UnconfiguredProject ImplementFullPath(string? fullPath)
        {
            return Create(fullPath: fullPath);
        }

        public static UnconfiguredProject Create(IProjectThreadingService threadingService)
        {
            var project = CreateDefault(threadingService);

            return project.Object;
        }

        public static UnconfiguredProject CreateWithActiveConfiguredProjectProvider(IProjectThreadingService threadingService)
        {
            var project = CreateDefault(threadingService);

            var activeConfiguredProject = new Mock<IActiveConfiguredProjectProvider>().Object;
            project.Setup(s => s.Services.ActiveConfiguredProjectProvider).Returns(activeConfiguredProject);

            var configuredProject = new Mock<ConfiguredProject>();

            project.Setup(s => s.Services.ActiveConfiguredProjectProvider!.ActiveConfiguredProject).Returns(configuredProject.Object);
            project.Setup(s => s.Services.ActiveConfiguredProjectProvider!.ActiveConfiguredProject!.ProjectVersion).Returns(configuredProject.Object.ProjectVersion);
            return project.Object;
        }

        public static UnconfiguredProject Create(object? hostObject = null,
                                                 string? fullPath = null,
                                                 IProjectConfigurationsService? projectConfigurationsService = null,
                                                 ConfiguredProject? configuredProject = null,
                                                 IEnumerable<ConfiguredProject>? configuredProjects = null,
                                                 Encoding? projectEncoding = null,
                                                 IProjectAsynchronousTasksService? projectAsynchronousTasksService = null,
                                                 IProjectCapabilitiesScope? scope = null,
                                                 UnconfiguredProjectServices? unconfiguredProjectServices = null)
        {
            if (configuredProject is not null
                && configuredProjects is null)
            {
                configuredProjects = new[] { configuredProject };
            }
            else if (configuredProjects is not null
                     && configuredProject is null)
            {
                configuredProject = configuredProjects.First();
            }

            var service = IProjectServiceFactory.Create();

            if (unconfiguredProjectServices == null)
            {
                var unconfiguredProjectServicesMock = new Mock<UnconfiguredProjectServices>();

                unconfiguredProjectServicesMock.SetupGet<object?>(u => u.FaultHandler)
                                           .Returns(IProjectFaultHandlerServiceFactory.Create());

                unconfiguredProjectServicesMock.SetupGet(u => u.HostObject)
                                           .Returns(hostObject);

                unconfiguredProjectServicesMock.SetupGet(u => u.ProjectConfigurationsService)
                                           .Returns(projectConfigurationsService);

                var activeConfiguredProjectProvider = IActiveConfiguredProjectProviderFactory.Create(getActiveConfiguredProject: () => configuredProject);
                unconfiguredProjectServicesMock.Setup(u => u.ActiveConfiguredProjectProvider)
                                           .Returns(activeConfiguredProjectProvider);

                unconfiguredProjectServicesMock.Setup(u => u.ProjectAsynchronousTasks)
                                           .Returns(projectAsynchronousTasksService!);

                unconfiguredProjectServices = unconfiguredProjectServicesMock.Object;
            }

            var project = CreateDefault();
            project.Setup(u => u.ProjectService)
                               .Returns(service);

            project.Setup(u => u.Services)
                               .Returns(unconfiguredProjectServices);

            project.SetupGet<string?>(u => u.FullPath)
                                .Returns(fullPath);

            project.Setup(u => u.Capabilities)
                               .Returns(scope!);

            project.Setup(u => u.GetSuggestedConfiguredProjectAsync()).ReturnsAsync(configuredProject);

            if (projectEncoding != null)
            {
                project.Setup(u => u.GetFileEncodingAsync()).ReturnsAsync(projectEncoding);
            }

            if (configuredProjects is not null)
            {
                project.Setup(p => p.LoadConfiguredProjectAsync(It.IsAny<ProjectConfiguration>()))
                    .ReturnsAsync((ProjectConfiguration desiredConfig) => configuredProjects.First(configuredProject => configuredProject.ProjectConfiguration == desiredConfig));
            }

            return project.Object;
        }

        public static UnconfiguredProject CreateWithUnconfiguredProjectAdvanced()
        {
            var mock = CreateDefault();
            mock.As<UnconfiguredProjectAdvanced>();
            return mock.Object;
        }

        public static UnconfiguredProject ImplementGetEncodingAsync(Func<Task<Encoding>> encoding)
        {
            var mock = CreateDefault();
            mock.Setup(u => u.GetFileEncodingAsync()).Returns(encoding);
            return mock.Object;
        }

        public static UnconfiguredProject ImplementLoadConfiguredProjectAsync(Func<ProjectConfiguration, Task<ConfiguredProject>> action)
        {
            var mock = CreateDefault();
            mock.Setup(p => p.LoadConfiguredProjectAsync(It.IsAny<ProjectConfiguration>()))
                .Returns(action);

            return mock.Object;
        }

        private static Mock<UnconfiguredProject> CreateDefault(IProjectThreadingService? threadingService = null)
        {
            var unconfiguredProjectServices = UnconfiguredProjectServicesFactory.Create(threadingService);
            var project = new Mock<UnconfiguredProject>();
            project.Setup(u => u.Services)
                   .Returns(unconfiguredProjectServices);

            return project;
        }
    }
}
