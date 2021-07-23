// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.Threading;
using GlobalSettingValueProviderAndMetadata = System.Lazy<
    Microsoft.VisualStudio.ProjectSystem.Properties.IGlobalSettingExtensionValueProvider,
    Microsoft.VisualStudio.ProjectSystem.Properties.ILaunchProfileExtensionValueProviderMetadata>;
using LaunchProfileValueProviderAndMetadata = System.Lazy<
    Microsoft.VisualStudio.ProjectSystem.Properties.ILaunchProfileExtensionValueProvider,
    Microsoft.VisualStudio.ProjectSystem.Properties.ILaunchProfileExtensionValueProviderMetadata>;

namespace Microsoft.VisualStudio.ProjectSystem.Properties
{
    internal class LaunchProfileProjectProperties : IProjectProperties, IRuleAwareProjectProperties
    {
        private const string CommandNamePropertyName = "CommandName";
        private const string ExecutablePathPropertyName = "ExecutablePath";
        private const string CommandLineArgumentsPropertyName = "CommandLineArguments";
        private const string WorkingDirectoryPropertyName = "WorkingDirectory";
        private const string LaunchBrowserPropertyName = "LaunchBrowser";
        private const string LaunchUrlPropertyName = "LaunchUrl";
        private const string EnvironmentVariablesPropertyName = "EnvironmentVariables";

        private Rule? _rule;

        /// <remarks>
        /// These correspond to the properties explicitly declared on <see cref="ILaunchProfile"/>
        /// and as such they are always considered to exist on the profile, though they may
        /// not have a value.
        /// </remarks>
        private static readonly string[] s_standardPropertyNames = new[]
        {
            CommandNamePropertyName,
            ExecutablePathPropertyName,
            CommandLineArgumentsPropertyName,
            WorkingDirectoryPropertyName,
            LaunchBrowserPropertyName,
            LaunchUrlPropertyName,
            EnvironmentVariablesPropertyName
        };

        private readonly LaunchProfilePropertiesContext _context;
        private readonly ILaunchSettingsProvider3 _launchSettingsProvider;
        private readonly ImmutableDictionary<string, LaunchProfileValueProviderAndMetadata> _launchProfileValueProviders;
        private readonly ImmutableDictionary<string, GlobalSettingValueProviderAndMetadata> _globalSettingValueProviders;

        public LaunchProfileProjectProperties(
            string filePath,
            string profileName,
            ILaunchSettingsProvider3 launchSettingsProvider,
            ImmutableArray<LaunchProfileValueProviderAndMetadata> launchProfileExtensionValueProviders,
            ImmutableArray<GlobalSettingValueProviderAndMetadata> globalSettingExtensionValueProviders)
        {
            _context = new LaunchProfilePropertiesContext(filePath, profileName);
            _launchSettingsProvider = launchSettingsProvider;

            ImmutableDictionary<string, LaunchProfileValueProviderAndMetadata>.Builder launchProfileValueBuilder =
                ImmutableDictionary.CreateBuilder<string, LaunchProfileValueProviderAndMetadata>(StringComparers.PropertyNames);
            foreach (LaunchProfileValueProviderAndMetadata valueProvider in launchProfileExtensionValueProviders)
            {
                string[] propertyNames = valueProvider.Metadata.PropertyNames;

                foreach (string propertyName in propertyNames)
                {
                    Requires.Argument(!string.IsNullOrEmpty(propertyName), nameof(valueProvider), "A null or empty property name was found");

                    // CONSIDER: Allow duplicate intercepting property value providers for same property name.
                    Requires.Argument(!launchProfileValueBuilder.ContainsKey(propertyName), nameof(launchProfileValueBuilder), "Duplicate property value providers for same property name");

                    launchProfileValueBuilder.Add(propertyName, valueProvider);
                }
            }
            _launchProfileValueProviders = launchProfileValueBuilder.ToImmutable();

            ImmutableDictionary<string, GlobalSettingValueProviderAndMetadata>.Builder globalSettingValueBuilder =
                ImmutableDictionary.CreateBuilder<string, GlobalSettingValueProviderAndMetadata>(StringComparers.PropertyNames);
            foreach (GlobalSettingValueProviderAndMetadata valueProvider in globalSettingExtensionValueProviders)
            {
                string[] propertyNames = valueProvider.Metadata.PropertyNames;

                foreach (string propertyName in propertyNames)
                {
                    Requires.Argument(!string.IsNullOrEmpty(propertyName), nameof(valueProvider), "A null or empty property name was found");

                    // CONSIDER: Allow duplicate intercepting property value providers for same property name.
                    Requires.Argument(!globalSettingValueBuilder.ContainsKey(propertyName), nameof(globalSettingValueBuilder), "Duplicate property value providers for same property name");

                    globalSettingValueBuilder.Add(propertyName, valueProvider);
                }
            }
            _globalSettingValueProviders = globalSettingValueBuilder.ToImmutable();
        }

        public IProjectPropertiesContext Context => _context;

        public string FileFullPath => _context.File;

        public PropertyKind PropertyKind => PropertyKind.ItemGroup;

        public Task DeleteDirectPropertiesAsync()
        {
            throw new NotImplementedException();
        }

        public Task DeletePropertyAsync(string propertyName, IReadOnlyDictionary<string, string>? dimensionalConditions = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> GetDirectPropertyNamesAsync()
        {
            return GetPropertyNamesAsync();
        }

        public async Task<string> GetEvaluatedPropertyValueAsync(string propertyName)
        {
            return await GetUnevaluatedPropertyValueAsync(propertyName) ?? string.Empty;
        }

        /// <remarks>
        /// If the profile exists we return all the standard property names (as they are
        /// always considered defined) plus all of the defined properties supported by
        /// extenders.
        /// </remarks>
        public async Task<IEnumerable<string>> GetPropertyNamesAsync()
        {
            ILaunchSettings? snapshot = await _launchSettingsProvider.WaitForFirstSnapshot(Timeout.Infinite);
            Assumes.NotNull(snapshot);

            ILaunchProfile? profile = snapshot.Profiles.FirstOrDefault(p => StringComparers.LaunchProfileNames.Equals(p.Name, _context.ItemName));
            if (profile is null)
            {
                return Enumerable.Empty<string>();
            }
            ImmutableDictionary<string, object> globalSettings = snapshot.GlobalSettings;

            ImmutableSortedSet<string>.Builder builder = ImmutableSortedSet.CreateBuilder<string>(StringComparers.PropertyNames);
            builder.UnionWith(s_standardPropertyNames);

            foreach ((string propertyName, LaunchProfileValueProviderAndMetadata provider) in _launchProfileValueProviders)
            {
                string propertyValue = provider.Value.OnGetPropertyValue(propertyName, profile, globalSettings, _rule);
                if (!Strings.IsNullOrEmpty(propertyValue))
                {
                    builder.Add(propertyName);
                }
            }

            foreach ((string propertyName, GlobalSettingValueProviderAndMetadata provider) in _globalSettingValueProviders)
            {
                string propertyValue = provider.Value.OnGetPropertyValue(propertyName, globalSettings, _rule);
                if (!Strings.IsNullOrEmpty(propertyValue))
                {
                    builder.Add(propertyName);
                }
            }

            if (profile.OtherSettings is not null)
            {
                foreach ((string propertyName, _) in profile.OtherSettings)
                {
                    builder.Add(propertyName);
                }
            }

            return builder.ToImmutable();
        }

        /// <returns>
        /// If the profile does not exist, returns <see langword="null"/>. Otherwise, returns the value
        /// of the property if the property is not defined, or <see langword="null"/> otherwise. The
        /// standard properties are always considered to be defined.
        /// </returns>
        public async Task<string?> GetUnevaluatedPropertyValueAsync(string propertyName)
        {
            ILaunchSettings? snapshot = await _launchSettingsProvider.WaitForFirstSnapshot(Timeout.Infinite);
            Assumes.NotNull(snapshot);

            ILaunchProfile? profile = snapshot.Profiles.FirstOrDefault(p => StringComparers.LaunchProfileNames.Equals(p.Name, _context.ItemName));
            if (profile is null)
            {
                return null;
            }

            return propertyName switch
            {
                CommandNamePropertyName => profile.CommandName ?? string.Empty,
                ExecutablePathPropertyName => profile.ExecutablePath ?? string.Empty,
                CommandLineArgumentsPropertyName => profile.CommandLineArgs ?? string.Empty,
                WorkingDirectoryPropertyName => profile.WorkingDirectory ?? string.Empty,
                LaunchBrowserPropertyName => profile.LaunchBrowser ? "true" : "false",
                LaunchUrlPropertyName => profile.LaunchUrl ?? string.Empty,
                EnvironmentVariablesPropertyName => ConvertDictionaryToString(profile.EnvironmentVariables) ?? string.Empty,
                _ => GetExtensionPropertyValue(propertyName, profile, snapshot.GlobalSettings)
            };
        }

        public Task<bool> IsValueInheritedAsync(string propertyName)
        {
            return TaskResult.False;
        }

        public async Task SetPropertyValueAsync(string propertyName, string unevaluatedPropertyValue, IReadOnlyDictionary<string, string>? dimensionalConditions = null)
        {
            Action<IWritableLaunchProfile>? profileUpdateAction = null;

            // If this is a standard property, handle it ourselves.

            profileUpdateAction = propertyName switch
            {
                CommandNamePropertyName => profile => profile.CommandName = unevaluatedPropertyValue,
                ExecutablePathPropertyName => profile => profile.ExecutablePath = unevaluatedPropertyValue,
                CommandLineArgumentsPropertyName => profile => profile.CommandLineArgs = unevaluatedPropertyValue,
                WorkingDirectoryPropertyName => profile => profile.WorkingDirectory = unevaluatedPropertyValue,
                LaunchBrowserPropertyName => setLaunchBrowserProperty,
                LaunchUrlPropertyName => profile => profile.LaunchUrl = unevaluatedPropertyValue,
                EnvironmentVariablesPropertyName => setEnvironmentVariablesProperty,
                _ => null
            };

            if (profileUpdateAction is not null)
            {
                await _launchSettingsProvider.TryUpdateProfileAsync(_context.ItemName, profileUpdateAction);
                return;
            }

            // Next, check if a launch profile extender can handle it.

            profileUpdateAction = await GetPropertyValueSetterFromLaunchProfileExtendersAsync(propertyName, unevaluatedPropertyValue);
            if (profileUpdateAction is not null)
            {
                await _launchSettingsProvider.TryUpdateProfileAsync(_context.ItemName, profileUpdateAction);
                return;
            }

            // Then, check if a global setting extender can handle it.

            Func<ImmutableDictionary<string, object>, ImmutableDictionary<string, object?>>? globalSettingsUpdateFunction = GetPropertyValueSetterFromGlobalExtenders(propertyName, unevaluatedPropertyValue);
            if (globalSettingsUpdateFunction is not null)
            {
                await _launchSettingsProvider.UpdateGlobalSettingsAsync(globalSettingsUpdateFunction);
                return;
            }

            // Finally, store it in ILaunchProfile.OtherSettings.

            object? valueObject = null;
            if (_rule?.GetProperty(propertyName) is BaseProperty property)
            {
                valueObject = property switch
                {
                    BoolProperty => bool.TryParse(unevaluatedPropertyValue, out bool result) ? result : null,
                    IntProperty => int.TryParse(unevaluatedPropertyValue, out int result) ? result : null,
                    StringProperty => unevaluatedPropertyValue,
                    EnumProperty => unevaluatedPropertyValue,
                    DynamicEnumProperty => unevaluatedPropertyValue,
                    _ => throw new InvalidOperationException($"{nameof(LaunchProfileProjectProperties)} does not know how to convert strings to `{property.GetType()}`.")
                };
            }
            else
            {
                valueObject = unevaluatedPropertyValue;
            }

            if (valueObject is not null)
            {
                profileUpdateAction = p => p.OtherSettings[propertyName] = valueObject;
                await _launchSettingsProvider.TryUpdateProfileAsync(_context.ItemName, profileUpdateAction);
            }

            void setLaunchBrowserProperty(IWritableLaunchProfile profile)
            {
                if (bool.TryParse(unevaluatedPropertyValue, out bool result))
                {
                    profile.LaunchBrowser = result;
                }
            }

            void setEnvironmentVariablesProperty(IWritableLaunchProfile profile)
            {
                ParseStringIntoDictionary(unevaluatedPropertyValue, profile.EnvironmentVariables);
            }
        }

        public void SetRuleContext(Rule rule)
        {
            _rule = rule;
        }

        private string? GetExtensionPropertyValue(string propertyName, ILaunchProfile profile, ImmutableDictionary<string, object> globalSettings)
        {
            if (_launchProfileValueProviders.TryGetValue(propertyName, out LaunchProfileValueProviderAndMetadata? launchProfileValueProvider))
            {
                return launchProfileValueProvider.Value.OnGetPropertyValue(propertyName, profile, globalSettings, rule: _rule);
            }

            if (_globalSettingValueProviders.TryGetValue(propertyName, out GlobalSettingValueProviderAndMetadata? globalSettingValueProvider))
            {
                return globalSettingValueProvider.Value.OnGetPropertyValue(propertyName, globalSettings, rule: _rule);
            }

            return GetOtherSettingsPropertyValue(propertyName, profile);
        }

        private string? GetOtherSettingsPropertyValue(string propertyName, ILaunchProfile profile)
        {
            if (profile.OtherSettings is not null
                && profile.OtherSettings.TryGetValue(propertyName, out object? valueObject))
            {
                if (_rule?.GetProperty(propertyName) is BaseProperty property)
                {
                    string? valueString = null;
                    valueString = property switch
                    {
                        BoolProperty => boolToString(valueObject),
                        IntProperty => intToString(valueObject),
                        StringProperty => valueObject as string,
                        EnumProperty => valueObject as string,
                        DynamicEnumProperty => valueObject as string,
                        _ => throw new InvalidOperationException($"{nameof(LaunchProfileProjectProperties)} does not know how to convert `{property.GetType()}` to a string.")
                    };

                    return valueString;
                }

                return valueObject as string;
            }

            return null;

            string? boolToString(object valueObject)
            {
                if (valueObject is bool value)
                {
                    return value ? "true" : "false";
                }

                return null;
            }

            string? intToString(object valueObject)
            {
                if (valueObject is int value)
                {
                    return value.ToString();
                }

                return null;
            }
        }

        private async Task<Action<IWritableLaunchProfile>?> GetPropertyValueSetterFromLaunchProfileExtendersAsync(string propertyName, string unevaluatedValue)
        {
            if (_launchProfileValueProviders.TryGetValue(propertyName, out LaunchProfileValueProviderAndMetadata? launchProfileValueProvider))
            {
                ILaunchSettings? currentSettings = await _launchSettingsProvider.WaitForFirstSnapshot(Timeout.Infinite);
                Assumes.NotNull(currentSettings);

                ImmutableDictionary<string, object>? globalSettings = currentSettings.GlobalSettings;

                return profile =>
                {
                    launchProfileValueProvider.Value.OnSetPropertyValue(propertyName, unevaluatedValue, profile, globalSettings, _rule);
                };
            }

            return null;
        }

        private Func<ImmutableDictionary<string, object>, ImmutableDictionary<string, object?>>? GetPropertyValueSetterFromGlobalExtenders(string propertyName, string unevaluatedValue)
        {
            if (_globalSettingValueProviders.TryGetValue(propertyName, out GlobalSettingValueProviderAndMetadata? globalSettingValueProvider))
            {
                return globalSettings =>
                {
                    return globalSettingValueProvider.Value.OnSetPropertyValue(propertyName, unevaluatedValue, globalSettings, _rule);
                };
            }

            return null;
        }

        private static string? ConvertDictionaryToString(ImmutableDictionary<string, string>? value)
        {
            if (value is null)
            {
                return null;
            }

            return string.Join(",", value.OrderBy(kvp => kvp.Key, StringComparer.Ordinal).Select(kvp => $"{encode(kvp.Key)}={encode(kvp.Value)}"));

            static string encode(string value)
            {
                return value.Replace("/", "//").Replace(",", "/,").Replace("=", "/=");
            }
        }

        private static void ParseStringIntoDictionary(string value, Dictionary<string, string> dictionary)
        {
            dictionary.Clear();

            foreach (string entry in readEntries(value))
            {
                (string entryKey, string entryValue) = splitEntry(entry);
                string decodedEntryKey = decode(entryKey);
                string decodedEntryValue = decode(entryValue);

                if (!string.IsNullOrEmpty(decodedEntryKey))
                {
                    dictionary[decodedEntryKey] = decodedEntryValue;
                }
            }

            static IEnumerable<string> readEntries(string rawText)
            {
                bool escaped = false;
                int entryStart = 0;
                for (int i = 0; i < rawText.Length; i++)
                {
                    if (rawText[i] == ',' && !escaped)
                    {
                        yield return rawText.Substring(entryStart, i - entryStart);
                        entryStart = i + 1;
                        escaped = false;
                    }
                    else if (rawText[i] == '/')
                    {
                        escaped = !escaped;
                    }
                    else
                    {
                        escaped = false;
                    }
                }

                yield return rawText.Substring(entryStart);
            }

            static (string encodedKey, string encodedValue) splitEntry(string entry)
            {
                bool escaped = false;
                for (int i = 0; i < entry.Length; i++)
                {
                    if (entry[i] == '=' && !escaped)
                    {
                        return (entry.Substring(0, i), entry.Substring(i + 1));
                    }
                    else if (entry[i] == '/')
                    {
                        escaped = !escaped;
                    }
                    else
                    {
                        escaped = false;
                    }
                }

                return (string.Empty, string.Empty);
            }

            static string decode(string value)
            {
                return value.Replace("/=", "=").Replace("/,", ",").Replace("//", "/");
            }
        }

        private class LaunchProfilePropertiesContext : IProjectPropertiesContext
        {
            public LaunchProfilePropertiesContext(string file, string itemName)
            {
                File = file;
                ItemName = itemName;
            }

            public bool IsProjectFile => true;

            public string File { get; }

            public string ItemType => LaunchProfileProjectItemProvider.ItemType;

            public string ItemName { get; }
        }
    }
}
