// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.ProjectSystem.UpToDate
{
    internal sealed partial class BuildUpToDateCheck
    {
        internal sealed class State
        {
            public static State Empty { get; } = new State();

            public string? MSBuildProjectFullPath { get; }
            public string? MSBuildProjectDirectory { get; }
            public string? CopyUpToDateMarkerItem { get; }
            public string? OutputRelativeOrFullPath { get; }

            /// <summary>
            /// Contains the first path from the <see cref="ConfigurationGeneral.MSBuildAllProjectsProperty"/>,
            /// which MSBuild guarantees to be the newest import from all properties (since 16.0). As we
            /// are only interested in the newest import, we need not retain the remaining paths.
            /// </summary>
            public string? NewestImportInput { get; }

            public IComparable? LastVersionSeen { get; }

            public bool IsDisabled { get; }

            /// <summary>
            /// Gets the time at which the set of items changed.
            /// </summary>
            /// <remarks>
            /// <para>
            /// This is not the last timestamp of the items themselves. It is time at which items were
            /// last added or removed from the project.
            /// </para>
            /// <para>
            /// This property is not updated until after the first query occurs. Until that time it will
            /// equal <see cref="DateTime.MinValue"/> which represents the fact that we do not know when
            /// the set of items was last changed, so we cannot base any decisions on this data property.
            /// </para>
            /// </remarks>
            public DateTime LastItemsChangedAtUtc { get; }

            public ImmutableArray<(bool IsAdd, string ItemType, string Path, string? Link, CopyType)> LastItemChanges { get; }

            /// <summary>
            /// Gets the time at which the last up-to-date check was made.
            /// </summary>
            /// <remarks>
            /// This value is required in order to protect against a race condition described in
            /// https://github.com/dotnet/project-system/issues/4014. Specifically, if source files are
            /// modified during a compilation, but before that compilation's outputs are produced, then
            /// the changed input file's timestamp will be earlier than the compilation output, making
            /// it seem as though the compilation is up to date when in fact the input was not included
            /// in that compilation. We use this property as a proxy for compilation start time, whereas
            /// the outputs represent compilation end time.
            /// </remarks>
            public DateTime LastCheckedAtUtc { get; }

            public ImmutableHashSet<string> ItemTypes { get; }

            public ImmutableDictionary<string, ImmutableHashSet<(string Path, string? Link, CopyType CopyType)>> ItemsByItemType { get; }

            public ImmutableDictionary<string, (string targetPath, CopyType copyType)> CopyToOutputDirectoryItems { get; }

            public ImmutableArray<string> SetNames { get; }

            public ImmutableDictionary<string, ImmutableHashSet<string>> UpToDateCheckInputItemsBySetName { get; }

            public ImmutableDictionary<string, ImmutableHashSet<string>> UpToDateCheckOutputItemsBySetName { get; }

            public ImmutableDictionary<string, ImmutableHashSet<string>> UpToDateCheckBuiltItemsBySetName { get; }

            /// <summary>
            /// Holds <see cref="UpToDateCheckBuilt"/> items which are copied, not built.</summary>
            /// <remarks>
            /// <para>
            /// Key is destination, value is source.
            /// </para>
            /// <para>
            /// Projects add to this collection by specifying the <see cref="UpToDateCheckBuilt.OriginalProperty"/>
            /// on <see cref="UpToDateCheckBuilt"/> items.
            /// </para>
            /// </remarks>
            public ImmutableDictionary<string, string> CopiedOutputFiles { get; }

            public ImmutableHashSet<string> ResolvedAnalyzerReferencePaths { get; }

            public ImmutableHashSet<string> ResolvedCompilationReferencePaths { get; }

            /// <summary>
            /// Holds the set of observed <see cref="CopyUpToDateMarker"/> metadata values from all
            /// <see cref="ResolvedCompilationReference"/> items in the project.
            /// </summary>
            public ImmutableHashSet<string> CopyReferenceInputs { get; }

            /// <summary>
            /// Contains files such as:
            /// <list type="bullet">
            ///   <item>Potential <c>.editorconfig</c> paths</item>
            ///   <item>Potential <c>global.json</c> paths</item>
            ///   <item>Potential <c>Directory.Build.props</c> paths</item>
            ///   <item>Potential <c>Directory.Build.targets</c> paths</item>
            ///   <item><c>project.assets.json</c></item>
            /// </list>
            /// <para>
            /// We need all <em>potential</em> paths for files which may start affecting build if they were to be added.
            /// Any files not found on disk have MinValue dates.
            /// </para>
            /// </summary>
            /// <remarks>
            /// The <see cref="DateTime"/> values here do not update dynamically.
            /// </remarks>
            public IImmutableDictionary<string, DateTime> AdditionalDependentFileTimes { get; }

            /// <summary>
            /// Gets the time at which the set of items with non-<see cref="DateTime.MinValue"/> times
            /// in <see cref="AdditionalDependentFileTimes"/> changed (files added or removed).
            /// </summary>
            /// <remarks>
            /// <para>
            /// This property is not updated until after the first query occurs. Until that time it will
            /// equal <see cref="DateTime.MinValue"/> which represents the fact that we do not know when
            /// the set of items was last added or removed, so we cannot base any decisions on this data
            /// property.
            /// </para>
            /// </remarks>
            public DateTime LastAdditionalDependentFileTimesChangedAtUtc { get; }

            private State()
            {
                var emptyPathSet = ImmutableHashSet.Create<string>(StringComparers.Paths);
                var emptyItemBySetName = ImmutableDictionary.Create<string, ImmutableHashSet<string>>(s_setNameComparer);

                LastItemsChangedAtUtc = DateTime.MinValue;
                LastCheckedAtUtc = DateTime.MinValue;
                ItemTypes = ImmutableHashSet.Create<string>(StringComparers.ItemTypes);
                ItemsByItemType = ImmutableDictionary.Create<string, ImmutableHashSet<(string Path, string? Link, CopyType CopyType)>>(StringComparers.ItemTypes);
                CopyToOutputDirectoryItems = ImmutableDictionary.Create<string, (string targetPath, CopyType copyType)>(StringComparers.ItemNames);
                SetNames = ImmutableArray<string>.Empty;
                UpToDateCheckInputItemsBySetName = emptyItemBySetName;
                UpToDateCheckOutputItemsBySetName = emptyItemBySetName;
                UpToDateCheckBuiltItemsBySetName = emptyItemBySetName;
                CopiedOutputFiles = ImmutableDictionary.Create<string, string>(StringComparers.Paths);
                ResolvedAnalyzerReferencePaths = emptyPathSet;
                ResolvedCompilationReferencePaths = emptyPathSet;
                CopyReferenceInputs = emptyPathSet;
                AdditionalDependentFileTimes = ImmutableDictionary.Create<string, DateTime>(StringComparers.Paths);
                LastAdditionalDependentFileTimesChangedAtUtc = DateTime.MinValue;
            }

            private State(
                string? msBuildProjectFullPath,
                string? msBuildProjectDirectory,
                string? copyUpToDateMarkerItem,
                string? outputRelativeOrFullPath,
                string? newestImportInput,
                IComparable? lastVersionSeen,
                bool isDisabled,
                ImmutableHashSet<string> itemTypes,
                ImmutableDictionary<string, ImmutableHashSet<(string, string?, CopyType)>> itemsByItemType,
                ImmutableDictionary<string, (string targetPath, CopyType copyType)> copyToOutputDirectoryItems,
                ImmutableDictionary<string, ImmutableHashSet<string>> upToDateCheckInputItemsBySetName,
                ImmutableDictionary<string, ImmutableHashSet<string>> upToDateCheckOutputItemsBySetName,
                ImmutableDictionary<string, ImmutableHashSet<string>> upToDateCheckBuiltItemsBySetName,
                ImmutableDictionary<string, string> copiedOutputFiles,
                ImmutableHashSet<string> resolvedAnalyzerReferencePaths,
                ImmutableHashSet<string> resolvedCompilationReferencePaths,
                ImmutableHashSet<string> copyReferenceInputs,
                IImmutableDictionary<string, DateTime> additionalDependentFileTimes,
                DateTime lastAdditionalDependentFileTimesChangedAtUtc,
                DateTime lastItemsChangedAtUtc,
                DateTime lastCheckedAtUtc,
                ImmutableArray<(bool IsAdd, string ItemType, string Path, string? Link, CopyType)> lastItemChanges)
            {
                MSBuildProjectFullPath = msBuildProjectFullPath;
                MSBuildProjectDirectory = msBuildProjectDirectory;
                CopyUpToDateMarkerItem = copyUpToDateMarkerItem;
                OutputRelativeOrFullPath = outputRelativeOrFullPath;
                NewestImportInput = newestImportInput;
                LastVersionSeen = lastVersionSeen;
                IsDisabled = isDisabled;
                ItemTypes = itemTypes;
                ItemsByItemType = itemsByItemType;
                CopyToOutputDirectoryItems = copyToOutputDirectoryItems;
                UpToDateCheckInputItemsBySetName = upToDateCheckInputItemsBySetName;
                UpToDateCheckOutputItemsBySetName = upToDateCheckOutputItemsBySetName;
                UpToDateCheckBuiltItemsBySetName = upToDateCheckBuiltItemsBySetName;
                CopiedOutputFiles = copiedOutputFiles;
                ResolvedAnalyzerReferencePaths = resolvedAnalyzerReferencePaths;
                ResolvedCompilationReferencePaths = resolvedCompilationReferencePaths;
                CopyReferenceInputs = copyReferenceInputs;
                LastItemsChangedAtUtc = lastItemsChangedAtUtc;
                LastCheckedAtUtc = lastCheckedAtUtc;
                AdditionalDependentFileTimes = additionalDependentFileTimes;
                LastAdditionalDependentFileTimesChangedAtUtc = lastAdditionalDependentFileTimesChangedAtUtc;
                LastItemChanges = lastItemChanges;

                var setNames = new HashSet<string>(s_setNameComparer);
                setNames.AddRange(upToDateCheckInputItemsBySetName.Keys);
                setNames.AddRange(upToDateCheckOutputItemsBySetName.Keys);
                setNames.AddRange(upToDateCheckBuiltItemsBySetName.Keys);
                setNames.Remove(DefaultSetName);
                SetNames = setNames.OrderBy(n => n, s_setNameComparer).ToImmutableArray();
            }

            public State Update(
                IProjectSubscriptionUpdate jointRuleUpdate,
                IProjectSubscriptionUpdate sourceItemsUpdate,
                IProjectSnapshot2 projectSnapshot,
                IProjectItemSchema projectItemSchema,
                IProjectCatalogSnapshot projectCatalogSnapshot,
                IComparable configuredProjectVersion)
            {
                bool isDisabled = jointRuleUpdate.CurrentState.IsPropertyTrue(ConfigurationGeneral.SchemaName, ConfigurationGeneral.DisableFastUpToDateCheckProperty, defaultValue: false);

                string? msBuildProjectFullPath = jointRuleUpdate.CurrentState.GetPropertyOrDefault(ConfigurationGeneral.SchemaName, ConfigurationGeneral.MSBuildProjectFullPathProperty, MSBuildProjectFullPath);
                string? msBuildProjectDirectory = jointRuleUpdate.CurrentState.GetPropertyOrDefault(ConfigurationGeneral.SchemaName, ConfigurationGeneral.MSBuildProjectDirectoryProperty, MSBuildProjectDirectory);
                string? msBuildProjectOutputPath = jointRuleUpdate.CurrentState.GetPropertyOrDefault(ConfigurationGeneral.SchemaName, ConfigurationGeneral.OutputPathProperty, OutputRelativeOrFullPath);
                string? outputRelativeOrFullPath = jointRuleUpdate.CurrentState.GetPropertyOrDefault(ConfigurationGeneral.SchemaName, ConfigurationGeneral.OutDirProperty, msBuildProjectOutputPath);
                string msBuildAllProjects = jointRuleUpdate.CurrentState.GetPropertyOrDefault(ConfigurationGeneral.SchemaName, ConfigurationGeneral.MSBuildAllProjectsProperty, "");

                // The first item in this semicolon-separated list of project files will always be the one
                // with the newest timestamp. As we are only interested in timestamps on these files, we can
                // save memory and time by only considering this first path (dotnet/project-system#4333).
                string? newestImportInput = new LazyStringSplit(msBuildAllProjects, ';').FirstOrDefault();

                ImmutableHashSet<string> resolvedAnalyzerReferencePaths;
                if (jointRuleUpdate.ProjectChanges.TryGetValue(ResolvedAnalyzerReference.SchemaName, out IProjectChangeDescription change) && change.Difference.AnyChanges)
                {
                    resolvedAnalyzerReferencePaths = change.After.Items.Select(item => item.Value[ResolvedAnalyzerReference.ResolvedPathProperty]).ToImmutableHashSet(StringComparers.Paths);
                }
                else
                {
                    resolvedAnalyzerReferencePaths = ResolvedAnalyzerReferencePaths;
                }

                ImmutableDictionary<string, ImmutableHashSet<string>> upToDateCheckInputItems;
                if (jointRuleUpdate.ProjectChanges.TryGetValue(UpToDateCheckInput.SchemaName, out change) && change.Difference.AnyChanges)
                {
                    upToDateCheckInputItems = BuildItemsBySetName(change, UpToDateCheckInput.SetProperty);
                }
                else
                {
                    upToDateCheckInputItems = UpToDateCheckInputItemsBySetName;
                }

                ImmutableDictionary<string, ImmutableHashSet<string>> upToDateCheckOutputItems;
                if (sourceItemsUpdate.ProjectChanges.TryGetValue(UpToDateCheckOutput.SchemaName, out change) && change.Difference.AnyChanges)
                {
                    upToDateCheckOutputItems = BuildItemsBySetName(change, UpToDateCheckOutput.SetProperty);
                }
                else if (jointRuleUpdate.ProjectChanges.TryGetValue(UpToDateCheckOutput.SchemaName, out change) && change.Difference.AnyChanges)
                {
                    upToDateCheckOutputItems = BuildItemsBySetName(change, UpToDateCheckOutput.SetProperty);
                }
                else
                {
                    upToDateCheckOutputItems = UpToDateCheckOutputItemsBySetName;
                }

                string? copyUpToDateMarkerItem;
                if (jointRuleUpdate.ProjectChanges.TryGetValue(CopyUpToDateMarker.SchemaName, out change) && change.Difference.AnyChanges)
                {
                    copyUpToDateMarkerItem = change.After.Items.Count == 1 ? change.After.Items.Single().Key : null;
                }
                else
                {
                    copyUpToDateMarkerItem = CopyUpToDateMarkerItem;
                }

                ImmutableHashSet<string> resolvedCompilationReferencePaths;
                ImmutableHashSet<string> copyReferenceInputs;
                if (jointRuleUpdate.ProjectChanges.TryGetValue(ResolvedCompilationReference.SchemaName, out change) && change.Difference.AnyChanges)
                {
                    ImmutableHashSet<string>.Builder resolvedCompilationReferencePathsBuilder = ImmutableHashSet.CreateBuilder(StringComparers.Paths);
                    ImmutableHashSet<string>.Builder copyReferenceInputsBuilder = ImmutableHashSet.CreateBuilder(StringComparers.Paths);

                    foreach (IImmutableDictionary<string, string> itemMetadata in change.After.Items.Values)
                    {
                        string originalPath = itemMetadata[ResolvedCompilationReference.OriginalPathProperty];
                        string resolvedPath = itemMetadata[ResolvedCompilationReference.ResolvedPathProperty];
                        string copyReferenceInput = itemMetadata[CopyUpToDateMarker.SchemaName];

                        resolvedCompilationReferencePathsBuilder.Add(resolvedPath);

                        if (!string.IsNullOrWhiteSpace(originalPath))
                        {
                            copyReferenceInputsBuilder.Add(originalPath);
                        }

                        if (!string.IsNullOrWhiteSpace(copyReferenceInput))
                        {
                            copyReferenceInputsBuilder.Add(copyReferenceInput);
                        }
                    }

                    resolvedCompilationReferencePaths = resolvedCompilationReferencePathsBuilder.ToImmutable();
                    copyReferenceInputs = copyReferenceInputsBuilder.ToImmutable();
                }
                else
                {
                    resolvedCompilationReferencePaths = ResolvedCompilationReferencePaths;
                    copyReferenceInputs = CopyReferenceInputs;
                }

                ImmutableDictionary<string, ImmutableHashSet<string>> upToDateCheckBuiltItems;
                ImmutableDictionary<string, string> copiedOutputFiles;
                if (jointRuleUpdate.ProjectChanges.TryGetValue(UpToDateCheckBuilt.SchemaName, out change) && change.Difference.AnyChanges)
                {
                    var itemsBySet = new Dictionary<string, ImmutableHashSet<string>.Builder>(s_setNameComparer);
                    ImmutableDictionary<string, string>.Builder copiedOutputFilesBuilder = ImmutableDictionary.CreateBuilder<string, string>(StringComparers.Paths);

                    foreach ((string destination, IImmutableDictionary<string, string> metadata) in change.After.Items)
                    {
                        if (metadata.TryGetValue(UpToDateCheckBuilt.OriginalProperty, out string source) && !string.IsNullOrEmpty(source))
                        {
                            // This file is copied, not built
                            // Remember the `Original` source for later
                            copiedOutputFilesBuilder[destination] = source;
                        }
                        else
                        {
                            // This file is built, not copied
                            string setName = metadata.GetStringProperty(UpToDateCheckBuilt.SetProperty) ?? DefaultSetName;

                            if (!itemsBySet.TryGetValue(setName, out ImmutableHashSet<string>.Builder builder))
                            {
                                itemsBySet[setName] = builder = ImmutableHashSet.CreateBuilder(s_setNameComparer);
                            }

                            builder.Add(destination);
                        }
                    }

                    upToDateCheckBuiltItems = itemsBySet.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.ToImmutable(), s_setNameComparer);
                    copiedOutputFiles = copiedOutputFilesBuilder.ToImmutable();
                }
                else
                {
                    upToDateCheckBuiltItems = UpToDateCheckBuiltItemsBySetName;
                    copiedOutputFiles = CopiedOutputFiles;
                }

                var itemTypes = projectItemSchema.GetKnownItemTypes()
                                                 .Where(itemType => projectItemSchema.GetItemType(itemType).UpToDateCheckInput)
                                                 .ToImmutableHashSet(StringComparers.ItemTypes);

                var itemTypeDiff = new SetDiff<string>(ItemTypes, itemTypes, StringComparers.ItemTypes);

                var itemsByItemTypeBuilder = ItemsByItemType.ToBuilder();

                bool itemTypesChanged = false;

                List<(bool IsAdd, string ItemType, string Path, string? Link, CopyType)> changes = new();

                foreach (string removedItemType in itemTypeDiff.Removed)
                {
                    itemTypesChanged = true;

                    if (itemsByItemTypeBuilder.TryGetValue(removedItemType, out var removedItems))
                    {
                        foreach ((string path, string? link, CopyType copyType) in removedItems)
                        {
                            changes.Add((false, removedItemType, path, link, copyType));
                        }

                        itemsByItemTypeBuilder.Remove(removedItemType);
                    }
                }

                itemTypesChanged |= itemTypeDiff.Added.GetEnumerator().MoveNext();

                bool itemsChanged = false;

                foreach ((string schemaName, IProjectChangeDescription projectChange) in sourceItemsUpdate.ProjectChanges)
                {
                    // ProjectChanges is keyed by the rule name which is usually the same as the item type, but not always (eg, in auto-generated rules)
                    string? itemType = null;
                    if (projectCatalogSnapshot.NamedCatalogs.TryGetValue(PropertyPageContexts.File, out IPropertyPagesCatalog fileCatalog))
                        itemType = fileCatalog.GetSchema(schemaName)?.DataSource.ItemType;

                    if (itemType == null)
                        continue;
                    if (!itemTypes.Contains(itemType))
                        continue;
                    if (!itemTypesChanged && !projectChange.Difference.AnyChanges)
                        continue;
                    if (projectChange.After.Items.Count == 0)
                        continue;

                    IEnumerable<(string Path, string? Link, CopyType)>? before = Array.Empty<(string Path, string? Link, CopyType)>();
                    if (itemsByItemTypeBuilder.TryGetValue(itemType, out ImmutableHashSet<(string Path, string? Link, CopyType CopyType)>? beforeItems))
                        before = beforeItems;

                    var after = projectChange.After.Items.Select(item => (Path: item.Key, GetLink(item.Value), GetCopyType(item.Value))).ToImmutableHashSet(ItemComparer.Instance);

                    var diff = new SetDiff<(string, string?, CopyType)>(before, after, ItemComparer.Instance);

                    foreach ((string path, string? link, CopyType copyType) in diff.Added)
                    {
                        changes.Add((true, itemType, path, link, copyType));
                    }

                    foreach ((string path, string? link, CopyType copyType) in diff.Removed)
                    {
                        changes.Add((false, itemType, path, link, copyType));
                    }

                    itemsByItemTypeBuilder[itemType] = after;
                    itemsChanged = true;
                }

                var copyToOutputDirectoryItems = GetCopyToOutputDirectoryItems(jointRuleUpdate);

                // NOTE when we previously had zero item types, we can surmise that the project has just been loaded. In such
                // a case it is not correct to assume that the items changed, and so we do not update the timestamp.
                // See https://github.com/dotnet/project-system/issues/5386
                DateTime lastItemsChangedAtUtc = itemsChanged && !ItemTypes.IsEmpty ? DateTime.UtcNow : LastItemsChangedAtUtc;

                DateTime lastAdditionalDependentFileTimesChangedAtUtc = GetLastTimeAdditionalDependentFilesAddedOrRemoved();

                return new State(
                    msBuildProjectFullPath,
                    msBuildProjectDirectory,
                    copyUpToDateMarkerItem,
                    outputRelativeOrFullPath,
                    newestImportInput,
                    lastVersionSeen: configuredProjectVersion,
                    isDisabled: isDisabled,
                    itemTypes: itemTypes,
                    itemsByItemType: itemsByItemTypeBuilder.ToImmutable(),
                    copyToOutputDirectoryItems : copyToOutputDirectoryItems,
                    upToDateCheckInputItemsBySetName: upToDateCheckInputItems,
                    upToDateCheckOutputItemsBySetName: upToDateCheckOutputItems,
                    upToDateCheckBuiltItemsBySetName: upToDateCheckBuiltItems,
                    copiedOutputFiles: copiedOutputFiles,
                    resolvedAnalyzerReferencePaths: resolvedAnalyzerReferencePaths,
                    resolvedCompilationReferencePaths: resolvedCompilationReferencePaths,
                    copyReferenceInputs: copyReferenceInputs,
                    additionalDependentFileTimes: projectSnapshot.AdditionalDependentFileTimes,
                    lastAdditionalDependentFileTimesChangedAtUtc: lastAdditionalDependentFileTimesChangedAtUtc,
                    lastItemsChangedAtUtc: lastItemsChangedAtUtc, lastCheckedAtUtc: LastCheckedAtUtc,
                    changes.ToImmutableArray());

                DateTime GetLastTimeAdditionalDependentFilesAddedOrRemoved()
                {
                    var lastExistingAdditionalDependentFiles = AdditionalDependentFileTimes.Where(pair => pair.Value != DateTime.MinValue)
                                                                       .Select(pair => pair.Key)
                                                                       .ToImmutableHashSet();

                    IEnumerable<string> currentExistingAdditionalDependentFiles = projectSnapshot.AdditionalDependentFileTimes
                                                                        .Where(pair => pair.Value != DateTime.MinValue)
                                                                        .Select(pair => pair.Key);

                    bool additionalDependentFilesChanged = !lastExistingAdditionalDependentFiles.SetEquals(currentExistingAdditionalDependentFiles);

                    return additionalDependentFilesChanged && !lastExistingAdditionalDependentFiles.IsEmpty ? DateTime.UtcNow : LastAdditionalDependentFileTimesChangedAtUtc;
                }

                static CopyType GetCopyType(IImmutableDictionary<string, string> itemMetadata)
                {
                    if (itemMetadata.TryGetValue(Compile.CopyToOutputDirectoryProperty, out string value))
                    {
                        if (string.Equals(value, Compile.CopyToOutputDirectoryValues.Always, StringComparisons.PropertyLiteralValues))
                        {
                            return CopyType.Always;
                        }

                        if (string.Equals(value, Compile.CopyToOutputDirectoryValues.PreserveNewest, StringComparisons.PropertyLiteralValues))
                        {
                            return CopyType.PreserveNewest;
                        }
                    }

                    return CopyType.Never;
                }

                static string? GetLink(IImmutableDictionary<string, string> itemMetadata)
                {
                    return itemMetadata.TryGetValue(Link, out string link) ? link : null;
                }

                static ImmutableDictionary<string, ImmutableHashSet<string>> BuildItemsBySetName(IProjectChangeDescription projectChangeDescription, string setPropertyName)
                {
                    var itemsBySet = new Dictionary<string, ImmutableHashSet<string>.Builder>(s_setNameComparer);

                    foreach ((string item, IImmutableDictionary<string, string> metadata) in projectChangeDescription.After.Items)
                    {
                        string? setNames = metadata.GetStringProperty(setPropertyName);

                        if (setNames != null)
                        {
                            foreach (string setName in new LazyStringSplit(setNames, ';'))
                            {
                                AddItem(setName, item);
                            }
                        }
                        else
                        {
                            AddItem(DefaultSetName, item);
                        }
                    }

                    return itemsBySet.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.ToImmutable(), s_setNameComparer);

                    void AddItem(string setName, string item)
                    {
                        if (!itemsBySet.TryGetValue(setName, out ImmutableHashSet<string>.Builder builder))
                        {
                            itemsBySet[setName] = builder = ImmutableHashSet.CreateBuilder(StringComparers.Paths);
                        }

                        builder.Add(item);
                    }
                }

                ImmutableDictionary<string, (string targetPath, CopyType copyType)> GetCopyToOutputDirectoryItems(IProjectSubscriptionUpdate projectSubscriptionUpdate)
                {
                    ImmutableDictionary<string, (string targetPath, CopyType copyType)>.Builder copyToOutputDirectoryItems =
                        ImmutableDictionary.CreateBuilder<string, (string targetPath, CopyType copyType)>();

                    if (projectSubscriptionUpdate.CurrentState.TryGetValue(CopyToOutputDirectoryItem.SchemaName,
                        out IProjectRuleSnapshot ruleSnapshot))
                    {
                        foreach ((string sourcePath, IImmutableDictionary<string, string> metadata) in ruleSnapshot.Items)
                        {
                            CopyType copyType = GetCopyType(metadata);
                            string? targetPath = metadata.GetStringProperty(CopyToOutputDirectoryItem.TargetPathProperty);
                            if (Strings.IsNullOrEmpty(targetPath))
                                continue;

                            copyToOutputDirectoryItems.Add(sourcePath, (targetPath, copyType));
                        }
                    }

                    return copyToOutputDirectoryItems.ToImmutable();
                }
            }

            public State WithLastCheckedAtUtc(DateTime lastCheckedAtUtc)
            {
                return new State(
                    MSBuildProjectFullPath,
                    MSBuildProjectDirectory,
                    CopyUpToDateMarkerItem,
                    OutputRelativeOrFullPath,
                    NewestImportInput,
                    LastVersionSeen,
                    IsDisabled,
                    ItemTypes,
                    ItemsByItemType,
                    CopyToOutputDirectoryItems,
                    UpToDateCheckInputItemsBySetName,
                    UpToDateCheckOutputItemsBySetName,
                    UpToDateCheckBuiltItemsBySetName,
                    CopiedOutputFiles,
                    ResolvedAnalyzerReferencePaths,
                    ResolvedCompilationReferencePaths,
                    CopyReferenceInputs,
                    AdditionalDependentFileTimes,
                    LastAdditionalDependentFileTimesChangedAtUtc,
                    LastItemsChangedAtUtc,
                    lastCheckedAtUtc,
                    LastItemChanges);
            }

            /// <summary>
            /// For unit tests only.
            /// </summary>
            internal State WithLastItemsChangedAtUtc(DateTime lastItemsChangedAtUtc)
            {
                return new State(
                    MSBuildProjectFullPath,
                    MSBuildProjectDirectory,
                    CopyUpToDateMarkerItem,
                    OutputRelativeOrFullPath,
                    NewestImportInput,
                    LastVersionSeen,
                    IsDisabled,
                    ItemTypes,
                    ItemsByItemType,
                    CopyToOutputDirectoryItems,
                    UpToDateCheckInputItemsBySetName,
                    UpToDateCheckOutputItemsBySetName,
                    UpToDateCheckBuiltItemsBySetName,
                    CopiedOutputFiles,
                    ResolvedAnalyzerReferencePaths,
                    ResolvedCompilationReferencePaths,
                    CopyReferenceInputs,
                    AdditionalDependentFileTimes,
                    LastAdditionalDependentFileTimesChangedAtUtc,
                    lastItemsChangedAtUtc,
                    LastCheckedAtUtc,
                    LastItemChanges);
            }

            /// <summary>
            /// For unit tests only.
            /// </summary>
            internal State WithLastAdditionalDependentFilesChangedAtUtc(DateTime lastAdditionalDependentFileTimesChangedAtUtc)
            {
                return new State(
                    MSBuildProjectFullPath,
                    MSBuildProjectDirectory,
                    CopyUpToDateMarkerItem,
                    OutputRelativeOrFullPath,
                    NewestImportInput,
                    LastVersionSeen,
                    IsDisabled,
                    ItemTypes,
                    ItemsByItemType,
                    CopyToOutputDirectoryItems,
                    UpToDateCheckInputItemsBySetName,
                    UpToDateCheckOutputItemsBySetName,
                    UpToDateCheckBuiltItemsBySetName,
                    CopiedOutputFiles,
                    ResolvedAnalyzerReferencePaths,
                    ResolvedCompilationReferencePaths,
                    CopyReferenceInputs,
                    AdditionalDependentFileTimes,
                    lastAdditionalDependentFileTimesChangedAtUtc,
                    LastItemsChangedAtUtc,
                    LastCheckedAtUtc,
                    LastItemChanges);
            }
        }
    }
}
