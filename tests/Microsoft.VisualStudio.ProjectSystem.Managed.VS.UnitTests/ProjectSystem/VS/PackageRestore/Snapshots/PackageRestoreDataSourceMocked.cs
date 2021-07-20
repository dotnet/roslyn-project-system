// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using Microsoft.VisualStudio.IO;
using NuGet.SolutionRestoreManager;

namespace Microsoft.VisualStudio.ProjectSystem.VS.PackageRestore.Snapshots
{
    internal class PackageRestoreDataSourceMocked : PackageRestoreDataSource
    {
        public PackageRestoreDataSourceMocked(UnconfiguredProject project, 
            IPackageRestoreUnconfiguredInputDataSource dataSource, 
            IProjectAsynchronousTasksService projectAsynchronousTasksService, 
            IVsSolutionRestoreService3 solutionRestoreService, 
            IFileSystem fileSystem, 
            IProjectDiagnosticOutputService logger, 
            IProjectDependentFileChangeNotificationService projectDependentFileChangeNotificationService, 
            IVsSolutionRestoreService4 solutionRestoreService4, 
            PackageRestoreSharedJoinableTaskCollection sharedJoinableTaskCollection) 
            : base(project, dataSource, projectAsynchronousTasksService, solutionRestoreService, fileSystem, logger, projectDependentFileChangeNotificationService, solutionRestoreService4, sharedJoinableTaskCollection)
        {
        }

        protected override bool IsProjectConfigurationVersionOutOfDate(System.Collections.Generic.IReadOnlyCollection<PackageRestoreConfiguredInput>? configuredInputs)
        {
            return false;
        }

        protected override bool IsSavedNominationEmptyOrOlder(ConfiguredProject activeConfiguredProject)
        {
            return false;
        }
    }
}
