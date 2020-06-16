// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    public partial class UnconfiguredProjectRetargetingDataSourceTests
    {
        private class ProjectRetarget : RetargetProjectTargetDescriptionBase
        {
            public override Guid TargetId => Guid.NewGuid();

            public override string RetargetingTitle => throw new NotImplementedException();

            public override string RetargetingDescription => throw new NotImplementedException();

            public override string DisplayName => throw new NotImplementedException();

            public override string CommandTitle => throw new NotImplementedException();
        }
    }
}
