// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Properties
{
    /// <summary>
    /// Interface for components that provide post-configuration cloning propertyName fix ups.
    /// </summary>
    internal class CloneConfigurationFixup : IClonePlatformFixup
    {
        private IEnumerable<string> _propertyNames = null;
        private List<string> _configurationProperties;
        private readonly IUnconfiguredProjectServices _unconfiguredProjectServices;
        private readonly IProjectThreadingService _projectThreadingService;
        private readonly ConfiguredProject? _sourceConfiguredProject;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloneConfigurationFixup"/> class.
        /// </summary>
        public CloneConfigurationFixup(IUnconfiguredProjectServices unconfiguredProjectServices,
            IProjectThreadingService projectThreadingService,
            ConfiguredProject? sourceConfiguredProject,
            string newConfigurationName)
        {
            _unconfiguredProjectServices = unconfiguredProjectServices;
            _projectThreadingService = projectThreadingService;
            _sourceConfiguredProject = sourceConfiguredProject;
        }

        /// <summary>
        /// Gets the list of property names that factor on Configuration builds.
        /// </summary>
        private async Task<IEnumerable<string>> GetConfigurationPropertiesNamesToClone()
        {
            if (_propertyNames != null)
            {
                return _propertyNames;
            }

            IProjectProperties? commonProperties =
                _unconfiguredProjectServices.ActiveConfiguredProjectProvider?.ActiveConfiguredProject?.Services.ProjectPropertiesProvider?.GetCommonProperties();
            string value = await commonProperties.GetEvaluatedPropertyValueAsync("CopyingConfigurationProperties");

            _propertyNames = value.Split(';').Distinct().Where(s => !string.IsNullOrEmpty(s));

            return _propertyNames;
        }

        /// <summary>
        /// Gets a value indicating whether this propertyName factors or not on configuration build
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        private async Task<bool> isInConfigurationDefaultsAsync(string propertyName)
        {
            _configurationProperties ??= (await GetConfigurationPropertiesNamesToClone()).ToList();
            return _configurationProperties.Contains(propertyName);
        }

        /// <summary>
        /// Read the value for propertyName via Targets
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns>Default propertyName value</returns>
        private async Task<string> GetDefaultPropertyValueAsync(string propertyName)
        {
            // TODO - How to get the property value from the configured project ?
            IProjectProperties? commonProperties = _sourceConfiguredProject?.Services?.ProjectPropertiesProvider?.GetCommonProperties();
            string? propertyValue = await commonProperties?.GetUnevaluatedPropertyValueAsync(propertyName);

            return propertyValue;
        }


        public bool ShouldElementBeCloned(ProjectPropertyElement propertyElement, ref string? alternativeValue)
        {
            string possibleAlternativeValue = "";
            bool shouldClone = false;

            _projectThreadingService.ExecuteSynchronously(async () =>
            {
                if (await isInConfigurationDefaultsAsync(propertyElement.Name))
                {
                    possibleAlternativeValue = await GetDefaultPropertyValueAsync(propertyElement.Name);
                    shouldClone = true;
                }
            });

            alternativeValue = possibleAlternativeValue;
            return shouldClone;
        }


        public bool ShouldElementBeCloned(ProjectMetadataElement metadataElement, ref string? alternativeValue)
        {
            string possibleAlternativeValue = "";
            bool shouldClone = false;

            _projectThreadingService.ExecuteSynchronously(async () =>
            {
                if (await isInConfigurationDefaultsAsync(metadataElement.Name))
                {
                    possibleAlternativeValue = GetDefaultPropertyValueAsync(metadataElement.Name).GetAwaiter().GetResult();
                    shouldClone = true;
                }
            });

            alternativeValue = possibleAlternativeValue;
            return shouldClone;
        }
    }
}
