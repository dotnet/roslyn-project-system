// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using Moq;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    internal static class IProjectRetargetCheckProviderFactory
    {
        internal static IProjectRetargetCheckProvider Create()
        {
            var mock = new Mock<IProjectRetargetCheckProvider>();

            return mock.Object;
        }
    }
}
