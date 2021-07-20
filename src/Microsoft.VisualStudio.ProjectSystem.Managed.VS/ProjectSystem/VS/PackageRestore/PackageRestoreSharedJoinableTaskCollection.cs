// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.ProjectSystem.VS.PackageRestore
{
    [Export(typeof(PackageRestoreSharedJoinableTaskCollection))]
    [ProjectSystemContract(ProjectSystemContractScope.UnconfiguredProject, ProjectSystemContractProvider.Private, Cardinality = ImportCardinality.ExactlyOne)]
    internal class PackageRestoreSharedJoinableTaskCollection : IJoinableTaskScope
    {
        private readonly JoinableTaskCollection _joinableTaskCollection;

        private readonly JoinableTaskFactory _joinableTaskFactory;

        [ImportingConstructor]
        public PackageRestoreSharedJoinableTaskCollection(IProjectThreadingService threadingService) 
        {
            _joinableTaskCollection = threadingService.JoinableTaskContext.CreateCollection();
            _joinableTaskFactory = threadingService.JoinableTaskContext.CreateFactory(_joinableTaskCollection);
        }

        public JoinableTaskCollection JoinableTaskCollection => _joinableTaskCollection;

        public JoinableTaskFactory JoinableTaskFactory => _joinableTaskFactory;
    }
}
