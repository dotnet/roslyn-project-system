// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.VisualStudio.Composition;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    [Export(typeof(IConfiguredProjectRetargetingDataSource))]
    [AppliesTo(ProjectCapability.DotNet)]
    internal class ConfiguredProjectRetargetingDataSource : ChainedProjectValueDataSourceBase<IImmutableList<ProjectTargetChange>>, IConfiguredProjectRetargetingDataSource
    {
        private readonly ConfiguredProject _project;
        private readonly IProjectSubscriptionService _projectSubscriptionService;

        [ImportingConstructor]
        internal ConfiguredProjectRetargetingDataSource(ConfiguredProject project,
                                                        IProjectSubscriptionService projectSubscriptionService)
            : base(project)
        {
            _project = project;
            _projectSubscriptionService = projectSubscriptionService;

            ProjectRetargetCheckProviders = new OrderPrecedenceImportCollection<IProjectRetargetCheckProvider>(projectCapabilityCheckProvider: project);
            ProjectPrerequisiteCheckProviders = new OrderPrecedenceImportCollection<IProjectPrerequisiteCheckProvider>(projectCapabilityCheckProvider: project);
        }

        protected override UnconfiguredProject? ContainingProject => _project.UnconfiguredProject;

        [ImportMany]
        public OrderPrecedenceImportCollection<IProjectRetargetCheckProvider> ProjectRetargetCheckProviders { get; }

        [ImportMany]
        public OrderPrecedenceImportCollection<IProjectPrerequisiteCheckProvider> ProjectPrerequisiteCheckProviders { get; }

        protected override IDisposable? LinkExternalInput(ITargetBlock<IProjectVersionedValue<IImmutableList<ProjectTargetChange>>> targetBlock)
        {
            IProjectValueDataSource<IProjectSubscriptionUpdate> source = _projectSubscriptionService.ProjectRuleSource;

            var ruleNames = new HashSet<string>();
            foreach (IProjectRetargetCheckProvider provider in ProjectRetargetCheckProviders.ExtensionValues())
            {
                ruleNames.AddRange(provider.GetProjectEvaluationRuleNames());
            }
            foreach (IProjectPrerequisiteCheckProvider provider in ProjectPrerequisiteCheckProviders.ExtensionValues())
            {
                ruleNames.AddRange(provider.GetProjectEvaluationRuleNames());
            }

            // Transform the changes from evaluation/design-time build -> restore data
            DisposableValue<ISourceBlock<IProjectVersionedValue<IImmutableList<ProjectTargetChange>>>> transformBlock = source.SourceBlock.TransformWithNoDelta(CheckForProjectRetarget,
                    suppressVersionOnlyUpdates: false,
                    ruleNames: ruleNames);

            // Set the link up so that we publish changes to target block
            transformBlock.Value.LinkTo(targetBlock, DataflowOption.PropagateCompletion);

            // Join the source blocks, so if they need to switch to UI thread to complete 
            // and someone is blocked on us on the same thread, the call proceeds
            JoinUpstreamDataSources(source);

            return transformBlock;
        }

        private async Task<IProjectVersionedValue<IImmutableList<ProjectTargetChange>>> CheckForProjectRetarget(IProjectVersionedValue<IProjectSubscriptionUpdate> arg)
        {
            ImmutableList<ProjectTargetChange>.Builder changes = ImmutableList.CreateBuilder<ProjectTargetChange>();

            foreach (IProjectRetargetCheckProvider provider in ProjectRetargetCheckProviders.ExtensionValues())
            {
                TargetDescriptionBase? change = await provider.CheckAsync(arg.Value.CurrentState);
                if (change != null)
                {
                    changes.Add(ProjectTargetChange.CreateForRetarget(change, provider));
                }
            }

            foreach (IProjectPrerequisiteCheckProvider provider in ProjectPrerequisiteCheckProviders.ExtensionValues())
            {
                TargetDescriptionBase? change = await provider.CheckAsync(arg.Value.CurrentState);
                if (change != null)
                {
                    changes.Add(ProjectTargetChange.CreateForPrerequisite(change));
                }
            }

            return new ProjectVersionedValue<IImmutableList<ProjectTargetChange>>(changes.ToImmutable(), arg.DataSourceVersions);
        }
    }
}
