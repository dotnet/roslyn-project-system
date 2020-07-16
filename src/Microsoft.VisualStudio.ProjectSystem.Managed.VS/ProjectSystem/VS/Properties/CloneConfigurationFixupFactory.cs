// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem.VS.Properties;

namespace Microsoft.VisualStudio.ProjectSystem.Properties
{
    /// <summary>
    /// Interface for components that provide post-configuration cloning property fix ups.
    /// </summary>
    [Export(typeof(ICloneConfigurationFixupFactory))]
    [AppliesTo(ProjectCapabilities.Managed)]
    internal class CloneConfigurationFixupFactory : ICloneConfigurationFixupFactory
    {
        private readonly IUnconfiguredProjectServices _unconfiguredProjectServices;
        private readonly IProjectThreadingService _projectThreadingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloneConfigurationFixup"/> class.
        /// </summary>>
        [ImportingConstructor]
        public CloneConfigurationFixupFactory(IUnconfiguredProjectServices unconfiguredProjectServices, 
            IProjectThreadingService projectThreadingService)
        {
            _unconfiguredProjectServices = unconfiguredProjectServices;
            _projectThreadingService = projectThreadingService;
        }

        /// <summary>
        /// Prepares for a configuration cloning operation.
        /// </summary>
        /// <param name="sourceConfiguredProject">The template configuration.</param>
        /// <param name="newConfigurationName">The configuration being cloned to.</param>
        /// <returns>
        /// A fixup dedicated to a single cloning operation, or null if
        /// this factory has no special handling that is applicable for the
        /// given configurations.
        /// </returns>
        public IClonePlatformFixup CreateCloneFixup(ConfiguredProject? sourceConfiguredProject, string newConfigurationName)
        {
            return new CloneConfigurationFixup(_unconfiguredProjectServices,
                                                _projectThreadingService,
                                                sourceConfiguredProject,
                                                newConfigurationName);
        }
    }
}
