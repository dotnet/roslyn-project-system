// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.VS.HotReload;
using Moq;

namespace Microsoft.VisualStudio.ProjectSystem.VS
{
    internal static class IProjectHotReloadSessionManagerFactory
    {
        public static IProjectHotReloadSessionManager Create()
        {
            var mock = new Mock<IProjectHotReloadSessionManager>();

            mock.Setup(manager => manager.TryCreatePendingSessionAsync(It.IsAny<IDictionary<string, string>>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.ActivateSessionAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            return mock.Object;
        }
    }

}
