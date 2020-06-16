// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    internal abstract class RetargetProjectTargetDescriptionBase : TargetDescriptionBase
    {
        public abstract string RetargetingTitle { get; }

        public abstract string RetargetingDescription { get; }

        public override Guid SetupDriver => Guid.Empty;

        public override bool Supported => true;

        public override object? GetProperty(uint prop) => ((__VSPTDPROPID)prop) switch
        {
            __VSPTDPROPID.VSPTDPROPID_ProjectRetargetingTitle => RetargetingTitle,
            __VSPTDPROPID.VSPTDPROPID_ProjectRetargetingDescription => RetargetingDescription,
            __VSPTDPROPID.VSPTDPROPID_MissingPrerequisites => false,
            _ => base.GetProperty(prop),
        };
    }
}
