// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Threading.Tasks.Dataflow;
using Microsoft.VisualStudio.ProjectSystem.VS.PackageRestore;

namespace Microsoft.VisualStudio.ProjectSystem
{
    /// <summary>
    ///     A <see cref="IProjectValueDataSource{T}"/> that drops <see cref="ProjectDataSources.ConfiguredProjectIdentity"/> and
    ///     <see cref="ProjectDataSources.ConfiguredProjectVersion"/> versions from each value of the original
    ///     <see cref="IProjectValueDataSource{T}"/>.
    /// </summary>
    internal sealed class DropConfiguredProjectVersionDataSource2<T> : ChainedProjectValueDataSourceBase<T>
        where T : class
    {
        private readonly IProjectValueDataSource<T> _dataSource;

        public DropConfiguredProjectVersionDataSource2(UnconfiguredProject project, IProjectValueDataSource<T> dataSource)
            : base(project, synchronousDisposal: true, registerDataSource: false)
        {
            _dataSource = dataSource;
        }

        protected override IDisposable LinkExternalInput(ITargetBlock<IProjectVersionedValue<T>> targetBlock)
        {
            JoinUpstreamDataSources(_dataSource);

            DisposableValue<ISourceBlock<IProjectVersionedValue<T>>> block = _dataSource.SourceBlock.Transform(DropConfiguredProjectVersion);

            block.Value.LinkTo(targetBlock, DataflowOption.PropagateCompletion);

            return block;
        }

        private IProjectVersionedValue<T> DropConfiguredProjectVersion(IProjectVersionedValue<T> data)
        {
            if (data is IProjectVersionedValue<PackageRestoreConfiguredInput> dataRestoreInput)
            {
                dataRestoreInput.DataSourceVersions.TryGetValue(ProjectDataSources.ConfiguredProjectVersion, out IComparable configuredProjectVersion);

                dataRestoreInput.Value.ConfiguredProjectVersion = (long)configuredProjectVersion;
            }

            return new ProjectVersionedValue<T>(data.Value, data.DataSourceVersions.Remove(ProjectDataSources.ConfiguredProjectIdentity)
                                                                                   .Remove(ProjectDataSources.ConfiguredProjectVersion));
        }
    }
}
