// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using NuGet.SolutionRestoreManager;

namespace Microsoft.VisualStudio.ProjectSystem.VS.PackageRestore
{
    [Export(typeof(IPackageRestoreUnconfiguredInputDataSource))]
    [AppliesTo(ProjectCapability.PackageReferences)]
    internal partial class PackageRestoreUnconfiguredInputDataSource : ConfiguredToUnconfiguredChainedDataSourceBase<PackageRestoreConfiguredInput, PackageRestoreUnconfiguredInput>, IPackageRestoreUnconfiguredInputDataSource
    {
        [ImportingConstructor]
        public PackageRestoreUnconfiguredInputDataSource(UnconfiguredProject project, IActiveConfigurationGroupService activeConfigurationGroupService)
            : base(project, activeConfigurationGroupService)
        {
        }

        protected override IProjectValueDataSource<PackageRestoreConfiguredInput>? GetInputDataSource(ConfiguredProject configuredProject)
        {
            return configuredProject.Services.ExportProvider.GetExportedValueOrDefault<IPackageRestoreConfiguredInputDataSource>();
        }

        protected override PackageRestoreUnconfiguredInput ConvertInputData(IReadOnlyCollection<PackageRestoreConfiguredInput> inputs)
        {
            // If there are no updates, we have no active configurations
            ProjectRestoreInfo? restoreInfo = null;
            if (inputs.Count != 0)
            {
                // We need to combine the snapshots from each implicitly active configuration (ie per TFM), 
                // resolving any conflicts, which we'll report to the user.
                string msbuildProjectExtensionsPath = ResolveMSBuildProjectExtensionsPathConflicts(inputs);
                string originalTargetFrameworks = ResolveOriginalTargetFrameworksConflicts(inputs);
                string projectAssetsFilePath = ResolveProjectAssetsFilePathConflicts(inputs);
                IVsReferenceItems toolReferences = ResolveToolReferenceConflicts(inputs);
                IVsTargetFrameworks2 targetFrameworks = GetAllTargetFrameworks(inputs);

                restoreInfo = new ProjectRestoreInfo(
                    msbuildProjectExtensionsPath,
                    projectAssetsFilePath,
                    originalTargetFrameworks,
                    targetFrameworks,
                    toolReferences);
            }

            return new PackageRestoreUnconfiguredInput(restoreInfo, inputs);
        }

        private string ResolveProjectAssetsFilePathConflicts(IEnumerable<PackageRestoreConfiguredInput> updates)
        {
            // All configurations need to agree on where the project-wide asset file is located.
            return ResolvePropertyConflicts(updates, u => u.ProjectAssetsFilePath, NuGetRestore.ProjectAssetsFileProperty);
        }

        private string ResolveMSBuildProjectExtensionsPathConflicts(IEnumerable<PackageRestoreConfiguredInput> updates)
        {
            // All configurations need to agree on where the project-wide extensions path is located
            return ResolvePropertyConflicts(updates, u => u.MSBuildProjectExtensionsPath, NuGetRestore.MSBuildProjectExtensionsPathProperty);
        }

        private string ResolveOriginalTargetFrameworksConflicts(IEnumerable<PackageRestoreConfiguredInput> updates)
        {
            // All configurations need to agree on what the overall "user-written" frameworks for the 
            // project so that conditions in the project-wide 'nuget.g.props' and 'nuget.g.targets' 
            // are written and evaluated correctly.
            return ResolvePropertyConflicts(updates, u => u.OriginalTargetFrameworks, NuGetRestore.TargetFrameworksProperty);
        }

        private string ResolvePropertyConflicts(IEnumerable<PackageRestoreConfiguredInput> updates, Func<ProjectRestoreInfo, string> propertyGetter, string propertyName)
        {
            // Always use the first TFM listed in project to provide consistent behavior
            PackageRestoreConfiguredInput update = updates.First();
            string propertyValue = propertyGetter(update.RestoreInfo);

            // Every config should had same value
            bool hasConflicts = updates.Select(u => propertyGetter(u.RestoreInfo))
                                       .Distinct(StringComparers.PropertyNames)
                                       .Count() > 1;

            if (hasConflicts)
            {
                ReportUserFault(string.Format(
                        CultureInfo.CurrentCulture,
                        VSResources.Restore_PropertyWithInconsistentValues,
                        propertyName,
                        propertyValue,
                        update.ProjectConfiguration));
            }

            return propertyValue;
        }

        private IVsReferenceItems ResolveToolReferenceConflicts(IEnumerable<PackageRestoreConfiguredInput> updates)
        {
            var references = new Dictionary<string, IVsReferenceItem>(StringComparers.ItemNames);

            foreach (PackageRestoreConfiguredInput update in updates)
            {
                foreach (IVsReferenceItem reference in update.RestoreInfo.ToolReferences)
                {
                    if (ValidateToolReference(references, reference))
                    {
                        references.Add(reference.Name, reference);
                    }
                }
            }

            return new ReferenceItems(references.Values);
        }
        private IVsTargetFrameworks2 GetAllTargetFrameworks(IEnumerable<PackageRestoreConfiguredInput> updates)
        {
            var frameworks = new List<IVsTargetFrameworkInfo3>();

            foreach (PackageRestoreConfiguredInput update in updates)
            {
                Assumes.True(update.RestoreInfo.TargetFrameworks.Count == 1);

                var framework = (IVsTargetFrameworkInfo3)update.RestoreInfo.TargetFrameworks.Item(0);

                if (ValidateTargetFramework(update.ProjectConfiguration, framework))
                {
                    frameworks.Add(framework);
                }
            }

            return new TargetFrameworks(frameworks);
        }

        private bool ValidateToolReference(Dictionary<string, IVsReferenceItem> existingReferences, IVsReferenceItem reference)
        {
            if (existingReferences.TryGetValue(reference.Name, out IVsReferenceItem existingReference))
            {
                // CLI tool references are project-wide, so if they have conflicts in names, 
                // they must have the same metadata, which avoids from having to condition 
                // them so that they only appear in one TFM.
                if (!RestoreComparer.ReferenceItems.Equals(existingReference, reference))
                {
                    ReportUserFault(string.Format(
                        CultureInfo.CurrentCulture,
                        VSResources.Restore_DuplicateToolReferenceItems,
                        existingReference.Name));
                }

                return false;
            }

            return true;
        }

        private bool ValidateTargetFramework(ProjectConfiguration projectConfiguration, IVsTargetFrameworkInfo3 framework)
        {
            if (framework.TargetFrameworkMoniker.Length == 0)
            {
                ReportUserFault(string.Format(
                    CultureInfo.CurrentCulture,
                    VSResources.Restore_EmptyTargetFrameworkMoniker,
                    projectConfiguration.Name));

                return false;
            }

            return true;
        }

        private void ReportUserFault(string message)
        {
            try
            {
                throw new Exception(message);
            }
            catch (Exception ex)
            {
                ReportDataSourceUserFault(
                  ex,
                  ProjectFaultSeverity.LimitedFunctionality,
                  ContainingProject);
            }
        }
    }
}
