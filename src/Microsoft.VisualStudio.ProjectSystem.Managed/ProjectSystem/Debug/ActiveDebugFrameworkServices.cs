// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Configuration;

namespace Microsoft.VisualStudio.ProjectSystem.Debug
{
    /// <summary>
    /// Providers a wrapper around the what is considered the active debugging framework.
    /// </summary>
    [Export(typeof(IActiveDebugFrameworkServices))]
    [AppliesTo(ProjectCapability.DotNet)]
    internal class ActiveDebugFrameworkServices : IActiveDebugFrameworkServices
    {
        private readonly IActiveConfiguredProjectsProvider _activeConfiguredProjectsProvider;
        private readonly IUnconfiguredProjectCommonServices _commonProjectServices;

        [ImportingConstructor]
        public ActiveDebugFrameworkServices(IActiveConfiguredProjectsProvider activeConfiguredProjectsProvider, IUnconfiguredProjectCommonServices commonProjectServices)
        {
            _activeConfiguredProjectsProvider = activeConfiguredProjectsProvider;
            _commonProjectServices = commonProjectServices;
        }

        /// <summary>
        /// <see cref="IActiveDebugFrameworkServices.GetProjectFrameworksAsync"/>
        /// </summary>
        public async Task<List<string>?> GetProjectFrameworksAsync()
        {
            // It is important that we return the frameworks in the order they are specified in the project to ensure the default is set
            // correctly. 
            ConfigurationGeneral props = await _commonProjectServices.ActiveConfiguredProjectProperties.GetConfigurationGeneralPropertiesAsync();

            string? targetFrameworks = (string?)await props.TargetFrameworks.GetValueAsync();

            if (Strings.IsNullOrWhiteSpace(targetFrameworks))
            {
                return null;
            }

            return ConfigUtilities.EnumerateDimensionValues(targetFrameworks).ToList();
        }

        /// <summary>
        /// <see cref="IActiveDebugFrameworkServices.SetActiveDebuggingFrameworkPropertyAsync"/>
        /// </summary>
        public async Task SetActiveDebuggingFrameworkPropertyAsync(string activeFramework)
        {
            ProjectDebugger props = await _commonProjectServices.ActiveConfiguredProjectProperties.GetProjectDebuggerPropertiesAsync();
            await props.ActiveDebugFramework.SetValueAsync(activeFramework);
        }

        /// <summary>
        /// <see cref="IActiveDebugFrameworkServices.GetActiveDebuggingFrameworkPropertyAsync"/>
        /// </summary>
        public async Task<string?> GetActiveDebuggingFrameworkPropertyAsync()
        {
            ProjectDebugger props = await _commonProjectServices.ActiveConfiguredProjectProperties.GetProjectDebuggerPropertiesAsync();
            string? activeValue = await props.ActiveDebugFramework.GetValueAsync() as string;
            return activeValue;
        }

        /// <summary>
        /// <see cref="IActiveDebugFrameworkServices.GetConfiguredProjectForActiveFrameworkAsync"/>
        /// </summary>
        public async Task<ConfiguredProject?> GetConfiguredProjectForActiveFrameworkAsync()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            ImmutableDictionary<string, ConfiguredProject>? configProjects = await _activeConfiguredProjectsProvider.GetActiveConfiguredProjectsMapAsync();
#pragma warning restore CS0618 // Type or member is obsolete

            if (configProjects == null)
            {
                return null;
            }

            // If there is only one we are done
            if (configProjects.Count == 1)
            {
                return configProjects.First().Value;
            }

            string? activeFramework = await GetActiveDebuggingFrameworkPropertyAsync();

            if (!Strings.IsNullOrWhiteSpace(activeFramework))
            {
                if (configProjects.TryGetValue(activeFramework, out ConfiguredProject? configuredProject))
                {
                    return configuredProject;
                }
            }

            // We can't just select the first one. If activeFramework is not set we must pick the first one as defined by the 
            // targetFrameworks property. So we need the order as returned by GetProjectFrameworks()
            List<string>? frameworks = await GetProjectFrameworksAsync();

            if (frameworks?.Count > 0)
            {
                if (configProjects.TryGetValue(frameworks[0], out ConfiguredProject? configuredProject))
                {
                    return configuredProject;
                }
            }

            // All that is left is to return the first one.
            return configProjects.First().Value;
        }
    }
}
