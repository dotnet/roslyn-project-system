﻿// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem.Tree.Dependencies.Assets;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Tree.Dependencies.AttachedCollections
{
    internal sealed partial class DependenciesAttachedCollectionSourceProvider
    {
        /// <summary>
        /// Base class for dependency item providers that derive data from the assets file.
        /// Items using this base class must be identifiable by <see cref="ProjectTreeFlags"/>.
        /// </summary>
        /// <remarks>
        /// This is not expected to be a public extension point. Public extenders should implement
        /// <see cref="IDependenciesTreeAttachedCollectionSourceProvider"/> directly.
        /// </remarks>
        private abstract class AssetsFileProviderBase : IDependenciesTreeAttachedCollectionSourceProvider
        {
            private readonly HierarchyItemFlagsDetector _flagsDetector;

            protected AssetsFileProviderBase(ProjectTreeFlags flags) => _flagsDetector = new HierarchyItemFlagsDetector(flags);

            public ref readonly HierarchyItemFlagsDetector FlagsDetector => ref _flagsDetector;

            public IAttachedCollectionSource? TryCreateSource(IVsHierarchyItem hierarchyItem)
            {
                UnconfiguredProject? unconfiguredProject = hierarchyItem.HierarchyIdentity.Hierarchy.AsUnconfiguredProject();

                // Find the data source
                IAssetsFileDependenciesDataSource? dataSource = unconfiguredProject?.Services.ExportProvider.GetExportedValueOrDefault<IAssetsFileDependenciesDataSource>();

                if (dataSource == null)
                {
                    return null;
                }

                // Target will be null if project is not multi-targeting
                hierarchyItem.TryFindTarget(out string? target);

                return TryCreateSource(hierarchyItem, dataSource, target);
            }

            protected abstract IAttachedCollectionSource? TryCreateSource(IVsHierarchyItem hierarchyItem, IAssetsFileDependenciesDataSource dataSource, string? target);
        }

        [Export(typeof(IDependenciesTreeAttachedCollectionSourceProvider))]
        [AppliesTo(ProjectCapability.DependenciesTree)] // TODO condition on PackageReferences?
        private sealed class PackageProvider : AssetsFileProviderBase
        {
            private readonly JoinableTaskContext _joinableTaskContext;

            [ImportingConstructor]
            public PackageProvider(JoinableTaskContext joinableTaskContext)
                : base(DependencyTreeFlags.PackageDependency)
                    => _joinableTaskContext = joinableTaskContext;

            protected override IAttachedCollectionSource? TryCreateSource(IVsHierarchyItem hierarchyItem, IAssetsFileDependenciesDataSource dataSource, string? target)
            {
                return hierarchyItem.TryGetPackageDetails(out string? packageId, out string? packageVersion)
                    ? new PackageReferenceAttachedCollectionSource(hierarchyItem, target, packageId, packageVersion, dataSource, _joinableTaskContext)
                    : null;
            }
        }
    }
}