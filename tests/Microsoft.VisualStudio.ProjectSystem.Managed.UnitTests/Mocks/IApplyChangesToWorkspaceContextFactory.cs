// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Build;
using Moq;

namespace Microsoft.VisualStudio.ProjectSystem.LanguageServices
{
    internal static class IApplyChangesToWorkspaceContextFactory
    {
        public static IApplyChangesToWorkspaceContext Create()
        {
            return Mock.Of<IApplyChangesToWorkspaceContext>();
        }

        public static IApplyChangesToWorkspaceContext ImplementApplyProjectBuildAsync(Action<IProjectVersionedValue<IProjectSubscriptionUpdate>, IProjectBuildSnapshot, ContextState, CancellationToken> action)
        {
            var mock = new Mock<IApplyChangesToWorkspaceContext>();
            mock.Setup(c => c.ApplyProjectBuildAsync(It.IsAny<IProjectVersionedValue<IProjectSubscriptionUpdate>>(), It.IsAny<IProjectBuildSnapshot>(), It.IsAny<ContextState>(), It.IsAny<CancellationToken>()))
                .Callback(action)
                .Returns(Task.CompletedTask);

            return mock.Object;
        }

        public static IApplyChangesToWorkspaceContext ImplementApplyProjectEvaluationAsync(Action<IProjectVersionedValue<IProjectSubscriptionUpdate>, ContextState, CancellationToken> action)
        {
            var mock = new Mock<IApplyChangesToWorkspaceContext>();
            mock.Setup(c => c.ApplyProjectEvaluationAsync(It.IsAny<IProjectVersionedValue<IProjectSubscriptionUpdate>>(), It.IsAny<ContextState>(), It.IsAny<CancellationToken>()))
                .Callback(action)
                .Returns(Task.CompletedTask);

            return mock.Object;
        }

        public static IApplyChangesToWorkspaceContext ImplementApplySourceItemsAsync(Action<IProjectVersionedValue<IProjectSubscriptionUpdate>, ContextState, CancellationToken> action)
        {
            var mock = new Mock<IApplyChangesToWorkspaceContext>();
            mock.Setup(c => c.ApplySourceItemsAsync(It.IsAny<IProjectVersionedValue<IProjectSubscriptionUpdate>>(), It.IsAny<ContextState>(), It.IsAny<CancellationToken>()))
                .Callback(action)
                .Returns(Task.CompletedTask);

            return mock.Object;
        }
    }
}
