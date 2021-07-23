// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.Mocks;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.Properties
{
    public class LaunchProfilesProjectPropertiesTests
    {
        private const string DefaultTestProjectPath = @"C:\alpha\beta\gamma.csproj";

        private static readonly ImmutableArray<Lazy<ILaunchProfileExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>> EmptyLaunchProfileExtensionValueProviders =
            ImmutableArray<Lazy<ILaunchProfileExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>>.Empty;
        private static readonly ImmutableArray<Lazy<IGlobalSettingExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>> EmptyGlobalSettingExtensionValueProviders =
            ImmutableArray<Lazy<IGlobalSettingExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>>.Empty;

        [Fact]
        public void WhenRetrievingItemProperties_TheContextHasTheExpectedValues()
        {
            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                EmptyLaunchProfileExtensionValueProviders,
                EmptyGlobalSettingExtensionValueProviders);

            var context = properties.Context;

            Assert.Equal(expected: DefaultTestProjectPath, actual: context.File);
            Assert.True(context.IsProjectFile);
            Assert.Equal(expected: "Profile1", actual: context.ItemName);
            Assert.Equal(expected: LaunchProfileProjectItemProvider.ItemType, actual: context.ItemType);
        }

        [Fact]
        public void WhenRetrievingItemProperties_TheFilePathIsTheProjectPath()
        {
            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                EmptyLaunchProfileExtensionValueProviders,
                EmptyGlobalSettingExtensionValueProviders);

            Assert.Equal(expected: DefaultTestProjectPath, actual: properties.FileFullPath);
        }

        [Fact]
        public void WhenRetrievingItemProperties_ThePropertyKindIsItemGroup()
        {
            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                EmptyLaunchProfileExtensionValueProviders,
                EmptyGlobalSettingExtensionValueProviders);

            Assert.Equal(expected: PropertyKind.ItemGroup, actual: properties.PropertyKind);
        }

        [Fact]
        public async Task WhenRetrievingItemPropertyNames_AllStandardProfilePropertyNamesAreReturnedEvenIfNotDefined()
        {
            var profile1 = new WritableLaunchProfile { Name = "Profile1" };
            var launchSettingsProvider = ILaunchSettingsProviderFactory.Create(
                launchProfiles: new[] { profile1.ToLaunchProfile() });

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                launchSettingsProvider,
                EmptyLaunchProfileExtensionValueProviders,
                EmptyGlobalSettingExtensionValueProviders);

            var propertyNames = await properties.GetPropertyNamesAsync();

            Assert.Contains("CommandName", propertyNames);
            Assert.Contains("ExecutablePath", propertyNames);
            Assert.Contains("CommandLineArguments", propertyNames);
            Assert.Contains("WorkingDirectory", propertyNames);
            Assert.Contains("LaunchBrowser", propertyNames);
            Assert.Contains("LaunchUrl", propertyNames);
            Assert.Contains("EnvironmentVariables", propertyNames);
        }

        [Fact]
        public async Task WhenRetrievingStandardPropertyValues_TheEmptyStringIsReturnedForUndefinedProperties()
        {
            var profile1 = new WritableLaunchProfile { Name = "Profile1" };
            var launchSettingsProvider = ILaunchSettingsProviderFactory.Create(
                launchProfiles: new[] { profile1.ToLaunchProfile() });

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                launchSettingsProvider,
                EmptyLaunchProfileExtensionValueProviders,
                EmptyGlobalSettingExtensionValueProviders);

            var standardPropertyNames = new[]
            {
                "CommandName",
                "ExecutablePath",
                "CommandLineArguments",
                "WorkingDirectory",
                "LaunchUrl",
                "EnvironmentVariables"
            };

            foreach (var standardPropertyName in standardPropertyNames)
            {
                var evaluatedValue = await properties.GetEvaluatedPropertyValueAsync(standardPropertyName);
                Assert.Equal(expected: string.Empty, actual: evaluatedValue);
                var unevaluatedValue = await properties.GetUnevaluatedPropertyValueAsync(standardPropertyName);
                Assert.Equal(expected: string.Empty, actual: unevaluatedValue);
            }
        }

        [Fact]
        public async Task WhenRetrievingTheLaunchBrowserValue_TheDefaultValueIsFalse()
        {
            var profile1 = new WritableLaunchProfile { Name = "Profile1" };
            var launchSettingsProvider = ILaunchSettingsProviderFactory.Create(
                launchProfiles: new[] { profile1.ToLaunchProfile() });

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                launchSettingsProvider,
                EmptyLaunchProfileExtensionValueProviders,
                EmptyGlobalSettingExtensionValueProviders);

            var evaluatedValue = await properties.GetEvaluatedPropertyValueAsync("LaunchBrowser");
            Assert.Equal(expected: "false", actual: evaluatedValue);
            var unevaluatedValue = await properties.GetUnevaluatedPropertyValueAsync("LaunchBrowser");
            Assert.Equal(expected: "false", actual: unevaluatedValue);
        }

        [Fact]
        public async Task WhenRetrievingStandardPropertyValues_TheExpectedValuesAreReturned()
        {
            var profile1 = new WritableLaunchProfile
            {
                Name = "Profile1",
                CommandLineArgs = "alpha beta gamma",
                CommandName = "epsilon",
                EnvironmentVariables = { ["One"] = "1", ["Two"] = "2" },
                ExecutablePath = @"D:\five\six\seven\eight.exe",
                LaunchBrowser = true,
                LaunchUrl = "https://localhost/profile",
                WorkingDirectory = @"C:\users\other\temp"
            };

            var launchSettingsProvider = ILaunchSettingsProviderFactory.Create(
                launchProfiles: new[] { profile1.ToLaunchProfile() });
            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                launchSettingsProvider,
                EmptyLaunchProfileExtensionValueProviders,
                EmptyGlobalSettingExtensionValueProviders);

            var expectedValues = new Dictionary<string, string>
            {
                ["CommandLineArguments"] = "alpha beta gamma",
                ["CommandName"] = "epsilon",
                ["EnvironmentVariables"] = "One=1,Two=2",
                ["ExecutablePath"] = @"D:\five\six\seven\eight.exe",
                ["LaunchBrowser"] = "true",
                ["LaunchUrl"] = "https://localhost/profile",
                ["WorkingDirectory"] = @"C:\users\other\temp",
            };

            foreach (var (propertyName, expectedPropertyValue) in expectedValues)
            {
                var actualUnevaluatedValue = await properties.GetUnevaluatedPropertyValueAsync(propertyName);
                var actualEvaluatedValue = await properties.GetEvaluatedPropertyValueAsync(propertyName);
                Assert.Equal(expectedPropertyValue, actualUnevaluatedValue);
                Assert.Equal(expectedPropertyValue, actualEvaluatedValue);
            }
        }

        [Fact]
        public async Task WhenSettingStandardPropertyValues_StandardCallbacksAreFound()
        {
            var profile1 = new WritableLaunchProfile
            {
                Name = "Profile1",
                CommandName = "epsilon",
            };

            bool callbackInvoked;
            var launchSettingsProvider = ILaunchSettingsProviderFactory.Create(
                launchProfiles: new[] { profile1.ToLaunchProfile() },
                tryUpdateProfileCallback: (profile, action) =>
                {
                    callbackInvoked = true;
                });
            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                launchSettingsProvider,
                EmptyLaunchProfileExtensionValueProviders,
                EmptyGlobalSettingExtensionValueProviders);

            var newValues = new Dictionary<string, string>
            {
                ["CommandLineArguments"] = "delta epsilon",
                ["CommandName"] = "arugula",
                ["EnvironmentVariables"] = "Three=3,Four=4",
                ["ExecutablePath"] = @"D:\nine\ten.exe",
                ["LaunchBrowser"] = "false",
                ["LaunchUrl"] = "https://localhost/myOtherProfile",
                ["WorkingDirectory"] = @"D:\aardvark",
            };

            foreach (var (propertyName, newPropertyValue) in newValues)
            {
                callbackInvoked = false;
                await properties.SetPropertyValueAsync(propertyName, newPropertyValue);
                Assert.True(callbackInvoked);
            }
        }

        [Fact]
        public async Task WhenRetrievingAnExtensionProperty_TheExtensionValueProviderIsCalled()
        {
            string? requestedPropertyName = null;
            var extensionValueProvider = ILaunchProfileExtensionValueProviderFactory.Create(
                (propertyName, profile, globals, rule) =>
                {
                    requestedPropertyName = propertyName;
                    return "alpha";
                });
            var metadata = ILaunchProfileExtensionValueProviderMetadataFactory.Create("MyProperty");

            var lazy = new Lazy<ILaunchProfileExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>(
                () => extensionValueProvider,
                metadata);

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                ImmutableArray.Create(lazy),
                EmptyGlobalSettingExtensionValueProviders);

            var propertyValue = await properties.GetEvaluatedPropertyValueAsync("MyProperty");
            Assert.Equal(expected: "MyProperty", actual: requestedPropertyName);
            Assert.Equal(expected: "alpha", actual: propertyValue);
        }

        [Fact]
        public async Task WhenRetrievingAnExtensionProperty_TheRuleIsPassedToTheExtensionValueProvider()
        {
            bool rulePassed = false;
            var extensionValueProvider = ILaunchProfileExtensionValueProviderFactory.Create(
                (propertyName, profile, globals, rule) =>
                {
                    rulePassed = rule is not null;
                    return "alpha";
                });

            var metadata = ILaunchProfileExtensionValueProviderMetadataFactory.Create("MyProperty");

            var lazy = new Lazy<ILaunchProfileExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>(
                () => extensionValueProvider,
                metadata);

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                ImmutableArray.Create(lazy),
                EmptyGlobalSettingExtensionValueProviders);

            var rule = new Rule();
            properties.SetRuleContext(rule);

            var propertyValue = await properties.GetEvaluatedPropertyValueAsync("MyProperty");
            Assert.True(rulePassed);
            Assert.Equal(expected: "alpha", actual: propertyValue);
        }

        [Fact]
        public async Task WhenRetrievingPropertyNames_LaunchProfileExtensionNamesAreIncludedForDefinedProperties()
        {
            var alphaValueProvider = ILaunchProfileExtensionValueProviderFactory.Create(
                (propertyName, profile, globals, rule) =>
                {
                    return "alpha";
                });
            var alphaMetadata = ILaunchProfileExtensionValueProviderMetadataFactory.Create("AlphaProperty");
            var alphaLazy = new Lazy<ILaunchProfileExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>(
                () => alphaValueProvider,
                alphaMetadata);

            var betaValueProvider = ILaunchProfileExtensionValueProviderFactory.Create(
                (propertyName, profile, globals, rule) =>
                {
                    return "";
                });
            var betaMetadata = ILaunchProfileExtensionValueProviderMetadataFactory.Create("BetaProperty");
            var betaLazy = new Lazy<ILaunchProfileExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>(
                () => betaValueProvider,
                betaMetadata);

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                ImmutableArray.Create(alphaLazy, betaLazy),
                EmptyGlobalSettingExtensionValueProviders);

            var names = await properties.GetPropertyNamesAsync();

            Assert.Contains("AlphaProperty", names);
            Assert.DoesNotContain("BetaProperty", names);
        }

        [Fact]
        public async Task WhenSettingAnExtensionProperty_TheExtensionValueProviderIsCalled()
        {
            string? updatedPropertyName = null;
            string? updatedPropertyValue = null;
            var extensionValueProvider = ILaunchProfileExtensionValueProviderFactory.Create(
                onSetPropertyValue: (propertyName, value, profile, globals, rule) =>
                {
                    updatedPropertyName = propertyName;
                    updatedPropertyValue = value;
                });
            var metadata = ILaunchProfileExtensionValueProviderMetadataFactory.Create("MyProperty");

            var lazy = new Lazy<ILaunchProfileExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>(
                () => extensionValueProvider,
                metadata);

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                ImmutableArray.Create(lazy),
                EmptyGlobalSettingExtensionValueProviders);

            await properties.SetPropertyValueAsync("MyProperty", "alpha");

            Assert.Equal(expected: "MyProperty", actual: updatedPropertyName);
            Assert.Equal(expected: "alpha", actual: updatedPropertyValue);
        }

        [Fact]
        public async Task WhenSettingAnExtensionProperty_TheRuleIsPassedToTheExtensionValueProvider()
        {
            bool rulePassed = false;
            var extensionValueProvider = ILaunchProfileExtensionValueProviderFactory.Create(
                onSetPropertyValue: (propertyName, value, profile, globals, rule) =>
                {
                    rulePassed = rule is not null;
                });
            var metadata = ILaunchProfileExtensionValueProviderMetadataFactory.Create("MyProperty");

            var lazy = new Lazy<ILaunchProfileExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>(
                () => extensionValueProvider,
                metadata);

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                ImmutableArray.Create(lazy),
                EmptyGlobalSettingExtensionValueProviders);
            properties.SetRuleContext(new Rule());

            await properties.SetPropertyValueAsync("MyProperty", "alpha");

            Assert.True(rulePassed);
        }

        [Fact]
        public async Task WhenRetrievingAGlobalProperty_TheExtensionValueProviderIsCalled()
        {
            string? requestedPropertyName = null;
            var extensionValueProvider = IGlobalSettingExtensionValueProviderFactory.Create(
                (propertyName, globals, rule) =>
                {
                    requestedPropertyName = propertyName;
                    return "alpha";
                });
            var metadata = ILaunchProfileExtensionValueProviderMetadataFactory.Create("MyProperty");

            var lazy = new Lazy<IGlobalSettingExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>(
                () => extensionValueProvider,
                metadata);

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                EmptyLaunchProfileExtensionValueProviders,
                ImmutableArray.Create(lazy));

            var propertyValue = await properties.GetEvaluatedPropertyValueAsync("MyProperty");
            Assert.Equal(expected: "MyProperty", actual: requestedPropertyName);
            Assert.Equal(expected: "alpha", actual: propertyValue);
        }

        [Fact]
        public async Task WhenRetrievingAGlobalProperty_TheRuleIsPassedToTheExtensionValueProvider()
        {
            bool rulePassed = false;
            var extensionValueProvider = IGlobalSettingExtensionValueProviderFactory.Create(
                (propertyName, globals, rule) =>
                {
                    rulePassed = rule is not null;
                    return "alpha";
                });
            var metadata = ILaunchProfileExtensionValueProviderMetadataFactory.Create("MyProperty");

            var lazy = new Lazy<IGlobalSettingExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>(
                () => extensionValueProvider,
                metadata);

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                EmptyLaunchProfileExtensionValueProviders,
                ImmutableArray.Create(lazy));
            properties.SetRuleContext(new Rule());

            var propertyValue = await properties.GetEvaluatedPropertyValueAsync("MyProperty");
            Assert.True(rulePassed);
            Assert.Equal(expected: "alpha", actual: propertyValue);
        }

        [Fact]
        public async Task WhenSettingAGlobalProperty_TheExtensionValueProviderIsCalled()
        {
            string? updatedPropertyName = null;
            string? updatedPropertyValue = null;
            var extensionValueProvider = IGlobalSettingExtensionValueProviderFactory.Create(
                onSetPropertyValue: (propertyName, value, globals, rule) =>
                {
                    updatedPropertyName = propertyName;
                    updatedPropertyValue = value;
                    return ImmutableDictionary<string, object?>.Empty.Add(propertyName, value);
                });
            var metadata = ILaunchProfileExtensionValueProviderMetadataFactory.Create("MyProperty");

            var lazy = new Lazy<IGlobalSettingExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>(
                () => extensionValueProvider,
                metadata);

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                EmptyLaunchProfileExtensionValueProviders,
                ImmutableArray.Create(lazy));

            await properties.SetPropertyValueAsync("MyProperty", "alpha");

            Assert.Equal(expected: "MyProperty", actual: updatedPropertyName);
            Assert.Equal(expected: "alpha", actual: updatedPropertyValue);
        }

        [Fact]
        public async Task WhenSettingAGlobalProperty_TheRuleIsPassedToTheExtensionValueProvider()
        {
            bool rulePassed = false;
            var extensionValueProvider = IGlobalSettingExtensionValueProviderFactory.Create(
                onSetPropertyValue: (propertyName, value, globals, rule) =>
                {
                    rulePassed = rule is not null;
                    return ImmutableDictionary<string, object?>.Empty.Add(propertyName, value);
                });
            var metadata = ILaunchProfileExtensionValueProviderMetadataFactory.Create("MyProperty");

            var lazy = new Lazy<IGlobalSettingExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>(
                () => extensionValueProvider,
                metadata);

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                EmptyLaunchProfileExtensionValueProviders,
                ImmutableArray.Create(lazy));
            properties.SetRuleContext(new Rule());

            await properties.SetPropertyValueAsync("MyProperty", "alpha");

            Assert.True(rulePassed);
        }

        [Fact]
        public async Task WhenRetrievingPropertyNames_GlobalSettingExtensionNamesAreIncludedForDefinedProperties()
        {
            var alphaValueProvider = IGlobalSettingExtensionValueProviderFactory.Create(
                (propertyName,  globals, rule) =>
                {
                    return "alpha";
                });
            var alphaMetadata = ILaunchProfileExtensionValueProviderMetadataFactory.Create("AlphaProperty");
            var alphaLazy = new Lazy<IGlobalSettingExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>(
                () => alphaValueProvider,
                alphaMetadata);

            var betaValueProvider = IGlobalSettingExtensionValueProviderFactory.Create(
                (propertyName, globals, rule) =>
                {
                    return "";
                });
            var betaMetadata = ILaunchProfileExtensionValueProviderMetadataFactory.Create("BetaProperty");
            var betaLazy = new Lazy<IGlobalSettingExtensionValueProvider, ILaunchProfileExtensionValueProviderMetadata>(
                () => betaValueProvider,
                betaMetadata);

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                CreateDefaultTestLaunchSettings(),
                EmptyLaunchProfileExtensionValueProviders,
                ImmutableArray.Create(alphaLazy, betaLazy));

            var names = await properties.GetPropertyNamesAsync();

            Assert.Contains("AlphaProperty", names);
            Assert.DoesNotContain("BetaProperty", names);
        }

        [Fact]
        public async Task WhenRetrievingPropertyNames_PropertiesInOtherSettingsAreIncluded()
        {
            var profile1 = new WritableLaunchProfile
            {
                Name = "Profile1",
                OtherSettings = { { "alpha", 1 } }
            };
            var launchSettingsProvider = ILaunchSettingsProviderFactory.Create(
                launchProfiles: new[] { profile1.ToLaunchProfile() });

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                launchSettingsProvider,
                EmptyLaunchProfileExtensionValueProviders,
                EmptyGlobalSettingExtensionValueProviders);

            var names = await properties.GetPropertyNamesAsync();

            Assert.Contains("alpha", names);
        }

        [Fact]
        public async Task WhenRetrievingPropertyNames_PropertiesInGlobalSettingsAreNotIncluded()
        {
            var profile1 = new WritableLaunchProfile
            {
                Name = "Profile1",
                OtherSettings = { { "alpha", 1 } }
            };
            var launchSettingsProvider = ILaunchSettingsProviderFactory.Create(
                launchProfiles: new[] { profile1.ToLaunchProfile() },
                globalSettings: ImmutableDictionary<string, object>.Empty.Add("beta", "value"));

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                launchSettingsProvider,
                EmptyLaunchProfileExtensionValueProviders,
                EmptyGlobalSettingExtensionValueProviders);

            var names = await properties.GetPropertyNamesAsync();

            Assert.DoesNotContain("beta", names);
        }

        [Fact]
        public async Task WhenRetrievingValuesFromOtherSettings_ValuesArePropertyConvertedToStrings()
        {
            var profile1 = new WritableLaunchProfile
            {
                Name = "Profile1",
                OtherSettings =
                {
                    { "anInteger", 1 },
                    { "aBoolean", true },
                    { "aString", "Hello, world" },
                    { "anEnumStoredAsAsAString", "valueOne" },
                    { "anotherString", "Hi, friends!" }
                }
            };
            var launchSettingsProvider = ILaunchSettingsProviderFactory.Create(
                launchProfiles: new[] { profile1.ToLaunchProfile() });

            var rule = new Rule
            {
                Properties =
                {
                    new IntProperty { Name = "anInteger" },
                    new BoolProperty { Name = "aBoolean" },
                    new StringProperty { Name = "aString" },
                    new EnumProperty { Name = "anEnumStoredAsAString" }
                    // anotherString intentionally not represented
                }
            };

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                launchSettingsProvider,
                EmptyLaunchProfileExtensionValueProviders,
                EmptyGlobalSettingExtensionValueProviders);

            properties.SetRuleContext(rule);

            var anIntegerValue = await properties.GetEvaluatedPropertyValueAsync("anInteger");
            Assert.Equal(expected: "1", actual: anIntegerValue);

            var aBooleanValue = await properties.GetEvaluatedPropertyValueAsync("aBoolean");
            Assert.Equal(expected: "true", actual: aBooleanValue);

            var aStringValue = await properties.GetEvaluatedPropertyValueAsync("aString");
            Assert.Equal(expected: "Hello, world", actual: aStringValue);

            var anEnumStoredAsAsAStringValue = await properties.GetEvaluatedPropertyValueAsync("anEnumStoredAsAsAString");
            Assert.Equal(expected: "valueOne", actual: anEnumStoredAsAsAStringValue);

            var anotherStringValue = await properties.GetEvaluatedPropertyValueAsync("anotherString");
            Assert.Equal(expected: "Hi, friends!", actual: anotherStringValue);
        }

        [Fact]
        public async Task WhenSettingValuesNotHandledByExtenders_ValuesOfTheExpectedTypesAreStoredInOtherSettings()
        {
            var writableProfile = new WritableLaunchProfile
            {
                Name = "Profile1",
            };
            var launchSettingsProvider = ILaunchSettingsProviderFactory.Create(
                launchProfiles: new[] { writableProfile.ToLaunchProfile() },
                tryUpdateProfileCallback: (profile, action) =>
                {
                    // Update writableProfile since we're hanging on to it rather than the profile given us by the mock. 
                    action(writableProfile);
                });

            var rule = new Rule
            {
                Properties =
                {
                    new IntProperty { Name = "anInteger" },
                    new BoolProperty { Name = "aBoolean" },
                    new StringProperty { Name = "aString" },
                    new EnumProperty { Name = "anEnumStoredAsAString" }
                    // anotherString intentionally not represented
                }
            };

            var properties = new LaunchProfileProjectProperties(
                DefaultTestProjectPath,
                "Profile1",
                launchSettingsProvider,
                EmptyLaunchProfileExtensionValueProviders,
                EmptyGlobalSettingExtensionValueProviders);

            properties.SetRuleContext(rule);

            await properties.SetPropertyValueAsync("anInteger", "2");
            await properties.SetPropertyValueAsync("aBoolean", "false");
            await properties.SetPropertyValueAsync("aString", "Hello, world!");
            await properties.SetPropertyValueAsync("anEnumStoredAsAString", "valueTwo");
            await properties.SetPropertyValueAsync("anotherString", "Hello, friends!");

            Assert.Equal(expected: 2, actual: writableProfile.OtherSettings["anInteger"]);
            Assert.Equal(expected: false, actual: writableProfile.OtherSettings["aBoolean"]);
            Assert.Equal(expected: "Hello, world!", actual: writableProfile.OtherSettings["aString"]);
            Assert.Equal(expected: "valueTwo", actual: writableProfile.OtherSettings["anEnumStoredAsAString"]);
            Assert.Equal(expected: "Hello, friends!", actual: writableProfile.OtherSettings["anotherString"]);
        }

        /// <summary>
        /// Creates an <see cref="ILaunchSettingsProvider"/> with two empty profiles named
        /// "Profile1" and "Profile2".
        /// </summary>
        private static ILaunchSettingsProvider3 CreateDefaultTestLaunchSettings()
        {
            var profile1 = new WritableLaunchProfile { Name = "Profile1" };
            var profile2 = new WritableLaunchProfile { Name = "Profile2" };
            var launchSettingsProvider = ILaunchSettingsProviderFactory.Create(
                launchProfiles: new[] { profile1.ToLaunchProfile(), profile2.ToLaunchProfile() });
            return launchSettingsProvider;
        }
    }
}
