// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    public partial class UnconfiguredProjectRetargetingDataSourceTests
    {
        private class Prerequisite : InstallerComponentMissingTargetDescriptionBase
        {
            private readonly Guid _setupDriver;

            public Prerequisite(Guid setupDriver)
                : base("")
            {
                _setupDriver = setupDriver;
            }

            public override Guid TargetId => Guid.NewGuid();

            public override string DisplayName => throw new NotImplementedException();

            public override Guid SetupDriver => _setupDriver;

            public override string UnloadReason => throw new NotImplementedException();

            public override string UnloadDescription => throw new NotImplementedException();
        }
    }
}
