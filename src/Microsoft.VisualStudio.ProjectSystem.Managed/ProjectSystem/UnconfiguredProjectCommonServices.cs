// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.ComponentModel.Composition;

namespace Microsoft.VisualStudio.ProjectSystem
{
    /// <summary>
    ///     Provides a default implementation of <see cref="IUnconfiguredProjectCommonServices"/>.
    /// </summary>
    [Export(typeof(IUnconfiguredProjectCommonServices))]
    internal class UnconfiguredProjectCommonServices : IUnconfiguredProjectCommonServices
    {
        private readonly UnconfiguredProject _project;
        private readonly Lazy<IProjectThreadingService> _threadingService;
        private readonly Lazy<IProjectAccessor> _projectAccessor;
        private readonly IActiveConfiguredValue<ConfiguredProject> _activeConfiguredProject;
        private readonly IActiveConfiguredValue<ProjectProperties> _activeConfiguredProjectProperties;
        private bool _cacheApplicable;

        [ImportingConstructor]
        public UnconfiguredProjectCommonServices(UnconfiguredProject project, Lazy<IProjectThreadingService> threadingService,
                                                 IActiveConfiguredValue<ConfiguredProject> activeConfiguredProject, IActiveConfiguredValue<ProjectProperties> activeConfiguredProjectProperties,
                                                 Lazy<IProjectAccessor> projectAccessor)
        {
            _project = project;
            _threadingService = threadingService;
            _activeConfiguredProject = activeConfiguredProject;
            _activeConfiguredProjectProperties = activeConfiguredProjectProperties;
            _projectAccessor = projectAccessor;

            _threadingService.Value.ExecuteSynchronously(async () =>
            {
                await _threadingService.Value.SwitchToUIThread();
                _cacheApplicable = FeatureFlagHelper.IsFeatureEnabled(FeatureFlagHelper.FeatureFlagCacheModeEnabled, false);
            });
        }

        public bool CacheApplicable => _cacheApplicable;

        public IProjectThreadingService ThreadingService
        {
            get { return _threadingService.Value; }
        }

        public UnconfiguredProject Project
        {
            get { return _project; }
        }

        public ConfiguredProject ActiveConfiguredProject
        {
            get { return _activeConfiguredProject.Value; }
        }

        public ProjectProperties ActiveConfiguredProjectProperties
        {
            get { return _activeConfiguredProjectProperties.Value; }
        }

        public IProjectAccessor ProjectAccessor
        {
            get { return _projectAccessor.Value; }
        }
    }
}
