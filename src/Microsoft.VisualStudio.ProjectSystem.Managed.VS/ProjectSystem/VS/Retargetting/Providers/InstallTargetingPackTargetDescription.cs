// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    internal class InstallTargetingPackTargetDescription : InstallerComponentMissingTargetDescriptionBase
    {
        private readonly Guid _targetDescriptionId = new Guid("0ABC0204-20D6-4A24-86F7-2F953B929C03");

        public InstallTargetingPackTargetDescription(string component)
            : base(component)
        {
        }

        public override Guid TargetId => _targetDescriptionId;

        public override string DisplayName => "Install Targeting Pack";

        public override string CommandTitle => "Install Targeting Pack...";

        public override string UnloadReason => "Targeting pack missing";

        public override string UnloadDescription => "This project targets a version of .NET Framework for which there is no targeting pack installed.";

        public override Guid SetupDriver => VSConstants.SetupDrivers.SetupDriver_VS;
    }
}
