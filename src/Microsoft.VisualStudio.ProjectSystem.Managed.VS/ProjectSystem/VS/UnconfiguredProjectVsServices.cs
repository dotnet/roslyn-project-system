// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.ProjectSystem.VS
{
    /// <summary>
    ///     Provides an implementation of <see cref="IUnconfiguredProjectVsServices"/> that delegates onto
    ///     its <see cref="IUnconfiguredProjectServices.HostObject"/> and underlying <see cref="IUnconfiguredProjectCommonServices"/>.
    /// </summary>
    [Export(typeof(IUnconfiguredProjectVsServices))]
    internal class UnconfiguredProjectVsServices : IUnconfiguredProjectVsServices
    {
        private readonly IUnconfiguredProjectCommonServices _commonServices;
        private readonly Lazy<IPhysicalProjectTree> _projectTree;

        [ImportingConstructor]
        public UnconfiguredProjectVsServices(IUnconfiguredProjectCommonServices commonServices, Lazy<IPhysicalProjectTree> projectTree)
        {
            _commonServices = commonServices;
            _projectTree = projectTree;
        }
        public bool CacheApplicable => false;

        public IVsHierarchy VsHierarchy
        {
            get
            {
                Assumes.NotNull(_commonServices.Project.Services.HostObject);
                return (IVsHierarchy)_commonServices.Project.Services.HostObject;
            }
        }

        public IVsProject4 VsProject
        {
            get
            {
                Assumes.NotNull(_commonServices.Project.Services.HostObject);
                return (IVsProject4)_commonServices.Project.Services.HostObject;
            }
        }

        public IProjectThreadingService ThreadingService
        {
            get { return _commonServices.ThreadingService; }
        }

        public UnconfiguredProject Project
        {
            get { return _commonServices.Project; }
        }

        public IPhysicalProjectTree ProjectTree
        {
            get { return _projectTree.Value; }
        }

        public ConfiguredProject ActiveConfiguredProject
        {
            get { return _commonServices.ActiveConfiguredProject; }
        }

        public ProjectProperties ActiveConfiguredProjectProperties
        {
            get { return _commonServices.ActiveConfiguredProjectProperties; }
        }

        public IProjectAccessor ProjectAccessor
        {
            get { return _commonServices.ProjectAccessor; }
        }
    }
}
