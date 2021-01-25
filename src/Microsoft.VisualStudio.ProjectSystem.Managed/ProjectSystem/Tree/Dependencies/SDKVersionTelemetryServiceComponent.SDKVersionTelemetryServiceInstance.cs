// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.VisualStudio.ProjectSystem.Tree.Dependencies
{
    internal partial class SDKVersionTelemetryServiceComponent
    {
        internal class SDKVersionTelemetryServiceInstance : OnceInitializedOnceDisposed, IMultiLifetimeInstance
        {
            private readonly IUnconfiguredProjectCommonServices _projectVsServices;
            private readonly ISafeProjectGuidService _projectGuidService;
            private readonly ITelemetryService _telemetryService;
            private readonly IUnconfiguredProjectTasksService _unconfiguredProjectTasksService;

            [ImportingConstructor]
            public SDKVersionTelemetryServiceInstance(
                IUnconfiguredProjectCommonServices projectVsServices,
                ISafeProjectGuidService projectGuidService,
                ITelemetryService telemetryService,
                IUnconfiguredProjectTasksService unconfiguredProjectTasksService)
                : base(synchronousDisposal: true)
            {
                _projectVsServices = projectVsServices;
                _projectGuidService = projectGuidService;
                _telemetryService = telemetryService;
                _unconfiguredProjectTasksService = unconfiguredProjectTasksService;
            }

            protected override void Initialize()
            {
                // Do not block initialization on reporting the sdk version. It is possible to deadlock.
                _projectVsServices.ThreadingService.RunAndForget(async () =>
                {
                    // Wait for the project to be loaded so that we don't prematurely load the active configuration
                    await _unconfiguredProjectTasksService.ProjectLoadedInHost;

                    // CACHE_PRODUCTIZE
                    if (_projectVsServices.CacheApplicable)
                    {
                        return;
                    }

                    ConfigurationGeneral projectProperties = await _projectVsServices.ActiveConfiguredProjectProperties.GetConfigurationGeneralPropertiesAsync();
                    Task<object?>? task = projectProperties?.NETCoreSdkVersion?.GetValueAsync();
                    string? version = task == null ? string.Empty : (string?)await task;
                    string? projectId = await GetProjectIdAsync();

                    if (Strings.IsNullOrEmpty(version) || Strings.IsNullOrEmpty(projectId))
                    {
                        return;
                    }

                    _telemetryService.PostProperties(TelemetryEventName.SDKVersion, new[]
                    {
                        (TelemetryPropertyName.SDKVersionProject, (object)projectId),
                        (TelemetryPropertyName.SDKVersionNETCoreSdkVersion, version)
                    });
                },
                unconfiguredProject: _projectVsServices.Project);
            }

            protected override void Dispose(bool disposing)
            {
            }

            private async Task<string?> GetProjectIdAsync()
            {
                Guid projectGuid = await _projectGuidService.GetProjectGuidAsync();
                return projectGuid == Guid.Empty ? null : projectGuid.ToString();
            }

            public Task InitializeAsync()
            {
                EnsureInitialized();

                return Task.CompletedTask;
            }
        }
    }
}
