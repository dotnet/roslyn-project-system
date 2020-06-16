// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    internal abstract class InstallerComponentMissingTargetDescriptionBase : TargetDescriptionBase
    {
        private readonly string _component;

        protected InstallerComponentMissingTargetDescriptionBase(string component)
        {
            _component = component;
        }

        public override bool Supported => false;

        public override string CommandTitle => VSResources.InstallTargetingPackCommandTitle;

        /// <summary>
        /// A brief description of why the project can't be loaded, displayed in parentheses after the project name
        /// </summary>
        public abstract string UnloadReason { get; }

        /// <summary>
        /// A longer description of why the project can't be loaded, displayed as a child node of the project
        /// </summary>
        public abstract string UnloadDescription { get; }

        public override object? GetProperty(uint prop) => ((__VSPTDPROPID)prop) switch
        {
            __VSPTDPROPID.VSPTDPROPID_MissingPrerequisites => true,
            __VSPTDPROPID.VSPTDPROPID_AcquisitionComponents => _component,
            __VSPTDPROPID.VSPTDPROPID_UnloadInfoLine => UnloadDescription,
            __VSPTDPROPID.VSPTDPROPID_UnloadTitle => UnloadReason,
            _ => base.GetProperty(prop),
        };
    }
}
