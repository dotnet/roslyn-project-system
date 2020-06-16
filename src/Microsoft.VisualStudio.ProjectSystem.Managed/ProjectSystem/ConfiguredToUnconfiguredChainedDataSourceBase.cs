// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using Microsoft.VisualStudio.ProjectSystem.Utilities;

namespace Microsoft.VisualStudio.ProjectSystem
{
    /// <summary>
    /// Base class allowing data to flow from an <see cref="UnconfiguredProject"/> instance by converting or merging
    /// the data of all implicitly active <see cref="ConfiguredProject"/> instances 
    /// </summary>
    /// <typeparam name="TInput">The type of the data that this block receives as input, from the configured project lever</typeparam>
    /// <typeparam name="TOutput">The type of the data that this block outputs, at the unconfigured project level</typeparam>
    public abstract class ConfiguredToUnconfiguredChainedDataSourceBase<TInput, TOutput> : ChainedProjectValueDataSourceBase<TOutput>
                                where TInput : class
                                where TOutput : class
    {
        private readonly UnconfiguredProject _project;
        private readonly IActiveConfigurationGroupService _activeConfigurationGroupService;

        protected ConfiguredToUnconfiguredChainedDataSourceBase(UnconfiguredProject project, IActiveConfigurationGroupService activeConfigurationGroupService)
            : base(project, synchronousDisposal: true, registerDataSource: false)
        {
            _project = project;
            _activeConfigurationGroupService = activeConfigurationGroupService;
        }

        protected override UnconfiguredProject ContainingProject
        {
            get { return _project; }
        }

        protected abstract IProjectValueDataSource<TInput>? GetInputDataSource(ConfiguredProject configuredProject);

        protected sealed override IDisposable LinkExternalInput(ITargetBlock<IProjectVersionedValue<TOutput>> targetBlock)
        {
            // At a high-level, we want to combine all implicitly active configurations (ie the active config of each TFM) data
            // and allow implementors to convert or combine it into another form of data and publish that. When a change is 
            // made to a configuration we should react to it and push a new version of our output. If the 
            // active configuration changes, we should react to it, and publish data from the new set of implicitly active configurations.
            var disposables = new DisposableBag();

            var configuredInputSource = new UnwrapCollectionChainedProjectValueDataSource<IReadOnlyCollection<ConfiguredProject>, TInput>(
                _project,
                projects => projects.Select(GetInputDataSource)
                                    .WhereNotNull()
                                    .Select(DropConfiguredProjectVersions),
                includeSourceVersions: true);

            disposables.Add(configuredInputSource);

            IProjectValueDataSource<IConfigurationGroup<ConfiguredProject>> activeConfiguredProjectsSource = _activeConfigurationGroupService.ActiveConfiguredProjectGroupSource;
            disposables.Add(activeConfiguredProjectsSource.SourceBlock.LinkTo(configuredInputSource, DataflowOption.PropagateCompletion));

            // Dataflow from two configurations can depend on a same unconfigured level data source, and processes it at a different speed.
            // Introduce a forward-only block to prevent regressions in versions.
            IPropagatorBlock<IProjectVersionedValue<IReadOnlyCollection<TInput>>, IProjectVersionedValue<IReadOnlyCollection<TInput>>> forwardOnlyBlock = ProjectDataSources.CreateDataSourceVersionForwardOnlyFilteringBlock<IReadOnlyCollection<TInput>>();
            disposables.Add(configuredInputSource.SourceBlock.LinkTo(forwardOnlyBlock, DataflowOption.PropagateCompletion));

            // Transform all input data -> output data
            DisposableValue<ISourceBlock<IProjectVersionedValue<TOutput>>> mergeBlock = forwardOnlyBlock.TransformWithNoDelta(update => update.Derive(ConvertInputData));
            disposables.Add(mergeBlock);

            // Set the link up so that we publish changes to target block
            mergeBlock.Value.LinkTo(targetBlock, DataflowOption.PropagateCompletion);

            // Join the source blocks, so if they need to switch to UI thread to complete 
            // and someone is blocked on us on the same thread, the call proceeds
            JoinUpstreamDataSources(configuredInputSource, activeConfiguredProjectsSource);

            return disposables;
        }

        private IProjectValueDataSource<TInput> DropConfiguredProjectVersions(IProjectValueDataSource<TInput> dataSource)
        {
            // Wrap it in a data source that will drop project version and identity versions so as they will never agree
            // on these versions as they are unique to each configuration. They'll be consistent by all other versions.
            return new DropConfiguredProjectVersionDataSource<TInput>(_project, dataSource);
        }

        protected abstract TOutput ConvertInputData(IReadOnlyCollection<TInput> inputs);
    }
}
