// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.Properties.Package
{
    [ExportInterceptingPropertyValueProvider(PackageIconPropertyName, ExportInterceptingPropertyValueProviderFile.ProjectFile)]
    internal sealed class PackageIconValueProvider : InterceptingPropertyValueProviderBase
    {
        private const string PackageIconPropertyName = "PackageIcon";
        private const string PackPropertyName = "Pack";
        private const string PackagePathPropertyName = "PackagePath";
        private const string NoneItem = "None";
        private const string TrueValue = "True";

        private readonly IProjectItemProvider _sourceItemsProvider;
        private readonly UnconfiguredProject _unconfiguredProject;

        [ImportingConstructor]
        public PackageIconValueProvider(
            [Import(ExportContractNames.ProjectItemProviders.SourceFiles)] IProjectItemProvider sourceItemsProvider,
            UnconfiguredProject unconfiguredProject)
        {
            _sourceItemsProvider = sourceItemsProvider;
            _unconfiguredProject = unconfiguredProject;
        }

        // https://docs.microsoft.com/en-us/nuget/reference/msbuild-targets#packageicon
        private static string CreatePackageIcon(string filePath, string? packagePath)
        {
            string filename = Path.GetFileName(filePath);
            // Make a slash-only value into empty string, so it won't get prepended onto the path.
            packagePath = ("\\".Equals(packagePath, StringComparison.Ordinal) || "/".Equals(packagePath, StringComparison.Ordinal))
                ? string.Empty : (packagePath ?? string.Empty);
            // The assumption is that PackagePath does not contain a path to a file; only a directory path.
            return Path.Combine(packagePath, filename);
        }

        private async Task<IProjectItem?> GetExistingNoneItemAsync(string existingPackageIcon)
        {
            foreach (IProjectItem noneItem in await _sourceItemsProvider.GetItemsAsync(NoneItem))
            {
                string pack = await noneItem.Metadata.GetEvaluatedPropertyValueAsync(PackPropertyName);
                string itemIconFilename = Path.GetFileName(noneItem.EvaluatedInclude);
                string existingIconFilename = Path.GetFileName(existingPackageIcon);
                // Instead of doing pure equality between itemPackageIcon and existingPackageIcon, a user may update the PackagePath
                // of the item and forget to update the PackageIcon to reflect those changes, or vice versa. Instead, if the filename
                // of this packed None item and the filename of the PackageIcon match, consider those to be related to one another.
                if (TrueValue.Equals(pack, StringComparison.OrdinalIgnoreCase) && itemIconFilename.Equals(existingIconFilename, StringComparison.Ordinal))
                {
                    return noneItem;
                }
            }

            return null;
        }

        public override async Task<string?> OnSetPropertyValueAsync(string propertyName, string unevaluatedPropertyValue, IProjectProperties defaultProperties, IReadOnlyDictionary<string, string>? dimensionalConditions = null)
        {
            string relativePath = PathHelper.MakeRelative(_unconfiguredProject, unevaluatedPropertyValue);
            string existingPackageIcon = await defaultProperties.GetEvaluatedPropertyValueAsync(PackageIconPropertyName);
            IProjectItem? existingItem = await GetExistingNoneItemAsync(existingPackageIcon);
            string packagePath = string.Empty;
            if (existingItem != null)
            {
                string itemPackagePath = await existingItem.Metadata.GetEvaluatedPropertyValueAsync(PackagePathPropertyName);
                string itemPackageIcon = CreatePackageIcon(existingItem.EvaluatedInclude, itemPackagePath);
                // The new filepath is the same as the current. No item changes are required.
                if (relativePath.Equals(existingItem.EvaluatedInclude, StringComparison.Ordinal))
                {
                    return itemPackageIcon;
                }

                await existingItem.SetUnevaluatedIncludeAsync(relativePath);
                packagePath = itemPackagePath;
            }
            else
            {
                await _sourceItemsProvider.AddAsync(NoneItem, relativePath, new Dictionary<string, string> { { PackPropertyName, "True" }, { PackagePathPropertyName, packagePath } });
            }

            return CreatePackageIcon(relativePath, packagePath);
        }

        private async Task<string> GetItemIncludeValueAsync(string propertyValue)
        {
            IProjectItem? existingItem = await GetExistingNoneItemAsync(propertyValue);
            return existingItem?.EvaluatedInclude ?? propertyValue;
        }

        public override Task<string> OnGetUnevaluatedPropertyValueAsync(string propertyName, string unevaluatedPropertyValue, IProjectProperties defaultProperties) =>
            GetItemIncludeValueAsync(unevaluatedPropertyValue);

        public override Task<string> OnGetEvaluatedPropertyValueAsync(string propertyName, string evaluatedPropertyValue, IProjectProperties defaultProperties) =>
            GetItemIncludeValueAsync(evaluatedPropertyValue);
    }
}
