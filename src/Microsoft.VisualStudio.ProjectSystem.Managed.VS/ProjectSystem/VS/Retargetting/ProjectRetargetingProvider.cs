// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    [Export(typeof(IProjectRetargetHandler))]
    [AppliesTo(ProjectCapability.DotNet)]
    internal class ProjectRetargetingProvider : IProjectRetargetHandler
    {
        private readonly UnconfiguredProject _unconfiguredProject;
        private readonly IUnconfiguredProjectRetargetingDataSource _unconfiguredProjectRetargetingDataSource;

        [ImportingConstructor]
        public ProjectRetargetingProvider(UnconfiguredProject unconfiguredProject,
            IUnconfiguredProjectRetargetingDataSource unconfiguredProjectRetargetingDataSource)
        {
            _unconfiguredProject = unconfiguredProject;
            _unconfiguredProjectRetargetingDataSource = unconfiguredProjectRetargetingDataSource;
        }

        public async Task<IProjectTargetChange?> CheckForRetargetAsync(RetargetCheckOptions options)
        {
            IProjectDataSourceRegistry? dataSourceRegistry = _unconfiguredProject.Services.DataSourceRegistry;
            Assumes.NotNull(dataSourceRegistry);
            IProjectVersionedValue<ProjectTargetChange> changes = await _unconfiguredProjectRetargetingDataSource.GetLatestVersionAsync(dataSourceRegistry);

            ProjectTargetChange? change = changes.Value;
            if (change == ProjectTargetChange.None)
            {
                change = null;
            }
            return change;
        }

        public Task<IImmutableList<string>> GetAffectedFilesAsync(IProjectTargetChange projectTargetChange)
        {
            return Task.FromResult<IImmutableList<string>>(ImmutableList<string>.Empty);
        }

        public Task RetargetAsync(TextWriter outputLogger, RetargetOptions options, IProjectTargetChange projectTargetChange, string backupLocation)
        {
            var change = projectTargetChange as ProjectTargetChange;

            Assumes.NotNull(change);
            Assumes.NotNull(change.RetargetProvider);

            return change.RetargetProvider.FixAsync(projectTargetChange);
        }
    }
}
