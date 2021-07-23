// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.IO;
using Microsoft.VisualStudio.ProjectSystem.VS.PackageRestore.Snapshots;
using NuGet.SolutionRestoreManager;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.VS.PackageRestore
{
    public class PackageRestoreDataSourceTests
    {
        [Fact]
        public async Task Dispose_WhenNotInitialized_DoesNotThrow()
        {
            var instance = CreateInstance();

            await instance.DisposeAsync();

            Assert.True(instance.IsDisposed);
        }

        [Fact]
        public async Task Dispose_WhenInitialized_DoesNotThrow()
        {
            var instance = CreateInitializedInstance();

            await instance.DisposeAsync();

            Assert.True(instance.IsDisposed);
        }

        [Fact]
        public async Task RestoreAsync_PushesRestoreInfoToRestoreService()
        {
            IVsProjectRestoreInfo2? result = null;
            var solutionRestoreService = IVsSolutionRestoreServiceFactory.ImplementNominateProjectAsync((projectFile, info, cancellationToken) => { result = info; });

            var instance = CreateInitializedInstance(solutionRestoreService: solutionRestoreService);

            var restoreInfo = ProjectRestoreInfoFactory.Create();
            var ConfigureInputs = PackageRestoreConfiguredInputFactory.Create(restoreInfo);
            var value = IProjectVersionedValueFactory.Create(new PackageRestoreUnconfiguredInput(restoreInfo, ConfigureInputs!));

            await instance.RestoreAsync(value);

            Assert.Same(restoreInfo, result);
        }

        [Fact]
        public async Task RestoreAsync_NullAsRestoreInfo_DoesNotPushToRestoreService()
        {
            int callCount = 0;
            var solutionRestoreService = IVsSolutionRestoreServiceFactory.ImplementNominateProjectAsync((projectFile, info, cancellationToken) => { callCount++; });

            var instance = CreateInitializedInstance(solutionRestoreService: solutionRestoreService);

            var value = IProjectVersionedValueFactory.Create(new PackageRestoreUnconfiguredInput(null, new PackageRestoreConfiguredInput[0]));

            await instance.RestoreAsync(value);

            Assert.Equal(0, callCount);
        }

        [Fact]
        public async Task RestoreAsync_UnchangedValueAsValue_DoesNotPushToRestoreService()
        {
            int callCount = 0;
            var solutionRestoreService = IVsSolutionRestoreServiceFactory.ImplementNominateProjectAsync((projectFile, info, cancellationToken) => { callCount++; });

            var instance = CreateInitializedInstance(solutionRestoreService: solutionRestoreService);

            var restoreInfo = ProjectRestoreInfoFactory.Create();
            var value = IProjectVersionedValueFactory.Create(new PackageRestoreUnconfiguredInput(restoreInfo, new PackageRestoreConfiguredInput[0]));

            await instance.RestoreAsync(value);

            Assert.Equal(1, callCount); // Should have only been called once
        }

        private static PackageRestoreDataSource CreateInitializedInstance(UnconfiguredProject? project = null, IPackageRestoreUnconfiguredInputDataSource? dataSource = null, IVsSolutionRestoreService3? solutionRestoreService = null)
        {
            var instance = CreateInstance(project, dataSource, solutionRestoreService);
            instance.LoadAsync();

            return instance;
        }

        private static PackageRestoreDataSource CreateInstance(UnconfiguredProject? project = null, IPackageRestoreUnconfiguredInputDataSource? dataSource = null, IVsSolutionRestoreService3? solutionRestoreService = null)
        {
            project ??= UnconfiguredProjectFactory.CreateWithActiveConfiguredProjectProvider(IProjectThreadingServiceFactory.Create());
            dataSource ??= IPackageRestoreUnconfiguredInputDataSourceFactory.Create();
            IProjectAsynchronousTasksService projectAsynchronousTasksService = IProjectAsynchronousTasksServiceFactory.Create();
            solutionRestoreService ??= IVsSolutionRestoreServiceFactory.Create();
            IProjectDiagnosticOutputService logger = IProjectDiagnosticOutputServiceFactory.Create();
            IFileSystem fileSystem = IFileSystemFactory.Create();
            var projectDependentFileChangeNotificationService = IProjectDependentFileChangeNotificationServiceFactory.Create();
            var vsSolutionRestoreService4 = IVsSolutionRestoreService4Factory.ImplementRegisterRestoreInfoSourceAsync();
            var sharedJoinableTaskCollection = new PackageRestoreSharedJoinableTaskCollection(IProjectThreadingServiceFactory.Create());

            return new PackageRestoreDataSourceMocked(
                project,
                dataSource,
                projectAsynchronousTasksService,
                solutionRestoreService,
                fileSystem,
                logger,
                projectDependentFileChangeNotificationService,
                vsSolutionRestoreService4,
                sharedJoinableTaskCollection);
        }
    }
}
