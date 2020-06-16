// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    [Export(typeof(IProjectPrerequisiteCheckProvider))]
    [AppliesTo(ProjectCapability.DotNet)]
    internal class MissingNetFrameworkTargetingPackRetarget : IProjectPrerequisiteCheckProvider
    {
        private static readonly IEnumerable<string> s_rules = new string[] { ConfigurationGeneral.SchemaName };

        public IEnumerable<string> GetProjectEvaluationRuleNames() => s_rules;

        public Task<TargetDescriptionBase?> CheckAsync(IImmutableDictionary<string, IProjectRuleSnapshot> projectState)
        {
            string? targetFrameworkIdentifier = projectState.GetPropertyOrDefault(ConfigurationGeneral.SchemaName, ConfigurationGeneral.TargetFrameworkIdentifierProperty, string.Empty);
            string? targetFrameworkVersion = projectState.GetPropertyOrDefault(ConfigurationGeneral.SchemaName, ConfigurationGeneral.TargetFrameworkVersionProperty, string.Empty);
            string? targetFrameworkProfile = projectState.GetPropertyOrDefault(ConfigurationGeneral.SchemaName, ConfigurationGeneral.TargetFrameworkProfileProperty, string.Empty);

            if (StringComparers.PropertyLiteralValues.Equals(targetFrameworkIdentifier, ".NETFramework"))   // Only .NET Framework has targeting packs
            {
                string? component = GetInstallerComponent(targetFrameworkVersion);

                if (component != null && !Microsoft.Build.Utilities.ToolLocationHelper.GetPathToReferenceAssemblies(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile).Any())
                {
                    return Task.FromResult((TargetDescriptionBase?)new InstallTargetingPackTargetDescription(component));
                }
            }

            return Task.FromResult((TargetDescriptionBase?)null);
        }

        private static string? GetInstallerComponent(string version) => version switch
        {
            "v3.5" => "Microsoft.Net.Component.3.5.DeveloperTools",
            "v4.0" => "Microsoft.Net.Component.4.TargetingPack",
            "v4.5" => "Microsoft.Net.Component.4.5.TargetingPack",
            "v4.5.1" => "Microsoft.Net.Component.4.5.1.TargetingPack",
            "v4.5.2" => "Microsoft.Net.Component.4.5.2.TargetingPack",
            "v4.6" => "Microsoft.Net.Component.4.6.TargetingPack",
            "v4.6.1" => "Microsoft.Net.Component.4.6.1.TargetingPack",
            "v4.6.2" => "Microsoft.Net.Component.4.6.2.TargetingPack",
            "v4.7" => "Microsoft.Net.Component.4.7.TargetingPack",
            "v4.7.1" => "Microsoft.Net.Component.4.7.1.TargetingPack",
            "v4.7.2" => "Microsoft.Net.Component.4.7.2.TargetingPack",
            "v4.8" => "Microsoft.Net.Component.4.8.TargetingPack",
            _ => null
        };
    }
}
