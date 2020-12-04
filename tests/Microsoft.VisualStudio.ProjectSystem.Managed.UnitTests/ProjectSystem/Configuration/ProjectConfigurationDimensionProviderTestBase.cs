// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio.Build;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.Configuration
{
    public abstract class ProjectConfigurationDimensionProviderTestBase
    {
        protected abstract string PropertyName { get; }
        protected abstract string DimensionName { get; }
        protected abstract string? DimensionDefaultValue { get; }

        private protected abstract BaseProjectConfigurationDimensionProvider CreateInstance(string projectXml);
        private protected abstract BaseProjectConfigurationDimensionProvider CreateInstance(IProjectAccessor projectAccessor);

        [Fact]
        public void PropertiesHaveExpectedValues()
        {
            var provider = CreateInstance("<Project />");

            Assert.Equal(PropertyName, provider.PropertyName);
            Assert.Equal(DimensionName, provider.DimensionName);
            Assert.Equal(DimensionDefaultValue, provider.DimensionDefaultValue);
        }

        [Fact]
        public async Task GetDefaultValuesForDimensionsAsync()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
    <PROP>A;B;C</PROP>
    <DIM>X</DIM>
  </PropertyGroup>
</Project>";

            var provider = CreateInstance(projectXml.Replace("PROP", PropertyName).Replace("DIM", DimensionName));

            var project = UnconfiguredProjectFactory.Create(configuredProject: ConfiguredProjectFactory.Create());
            var values = await provider.GetDefaultValuesForDimensionsAsync(project);

            Assert.Single(values);
            var value = values.First();
            Assert.Equal(DimensionName, value.Key);
            Assert.Equal("A", value.Value);
        }

        [Theory]
        [InlineData("<Project />")]
        [InlineData(@"<Project>
  <PropertyGroup>
    <DIM>X</DIM>
  </PropertyGroup>
</Project>")]
        public async Task GetDefaultValuesForDimensionsAsync_ReturnsEmptyIfUndefined(string projectXml)
        {
            var provider = CreateInstance(projectXml.Replace("DIM", DimensionName));

            var project = UnconfiguredProjectFactory.Create(configuredProject: ConfiguredProjectFactory.Create());
            var values = await provider.GetDefaultValuesForDimensionsAsync(project);

            Assert.Empty(values);
        }

        [Fact]
        public async Task GetProjectConfigurationDimensionsAsync()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
    <PROP>A;B;C</PROP>
    <DIM>X</DIM>
  </PropertyGroup>
</Project>";

            var provider = CreateInstance(projectXml.Replace("PROP", PropertyName).Replace("DIM", DimensionName));

            var project = UnconfiguredProjectFactory.Create(configuredProject: ConfiguredProjectFactory.Create());
            var values = await provider.GetProjectConfigurationDimensionsAsync(project);

            Assert.Single(values);
            var value = values.First();
            Assert.Equal(DimensionName, value.Key);
            string[] dimensionValues = value.Value.ToArray();
            AssertEx.CollectionLength(dimensionValues, 3);
            Assert.Equal("A", dimensionValues[0]);
            Assert.Equal("B", dimensionValues[1]);
            Assert.Equal("C", dimensionValues[2]);
        }

        [Theory]
        [InlineData("<Project />")]
        [InlineData(@"<Project>
  <PropertyGroup>
    <DIM>X</DIM>
  </PropertyGroup>
</Project>")]
        public async Task GetProjectConfigurationDimensionsAsync_ReturnsEmptyIfUndefined(string projectXml)
        {
            var provider = CreateInstance(projectXml.Replace("DIM", DimensionName));

            var project = UnconfiguredProjectFactory.Create(configuredProject: ConfiguredProjectFactory.Create());
            var values = await provider.GetProjectConfigurationDimensionsAsync(project);

            Assert.Empty(values);
        }

        [Fact]
        public async Task OnDimensionValueChanged_Add()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
    <PROP>A;B;C</PROP>
  </PropertyGroup>
</Project>";

            var rootElement = ProjectRootElementFactory.Create(projectXml.Replace("PROP", PropertyName));
            var projectAccessor = IProjectAccessorFactory.Create(rootElement);
            var configuredProject = ConfiguredProjectFactory.Create();
            var provider = CreateInstance(projectAccessor);
            var project = UnconfiguredProjectFactory.Create(configuredProject: configuredProject);

            // On ChangeEventStage.After nothing should be changed
            var args = new ProjectConfigurationDimensionValueChangedEventArgs(
                project,
                ConfigurationDimensionChange.Add,
                ChangeEventStage.After,
                DimensionName,
                "Added");
            await provider.OnDimensionValueChangedAsync(args);
            var property = ConfigUtilities.GetDimension(rootElement, PropertyName);
            Assert.NotNull(property);
            Assert.Equal("A;B;C", property!.Value);

            // On ChangeEventStage.Before the property should be added
            args = new ProjectConfigurationDimensionValueChangedEventArgs(
                project,
                ConfigurationDimensionChange.Add,
                ChangeEventStage.Before,
                DimensionName,
                "Added");
            await provider.OnDimensionValueChangedAsync(args);
            property = ConfigUtilities.GetDimension(rootElement, PropertyName);
            Assert.NotNull(property);
            Assert.Equal("A;B;C;Added", property!.Value);
        }

        [Fact]
        public async Task OnDimensionValueChanged_Remove()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
    <PROP>A;B;C</PROP>
  </PropertyGroup>
</Project>";

            var rootElement = ProjectRootElementFactory.Create(projectXml.Replace("PROP", PropertyName));
            var projectAccessor = IProjectAccessorFactory.Create(rootElement);
            var configuredProject = ConfiguredProjectFactory.Create();
            var provider = CreateInstance(projectAccessor);
            var project = UnconfiguredProjectFactory.Create(configuredProject: configuredProject);

            // On ChangeEventStage.After nothing should be changed
            var args = new ProjectConfigurationDimensionValueChangedEventArgs(
                project,
                ConfigurationDimensionChange.Delete,
                ChangeEventStage.After,
                DimensionName,
                "B");
            await provider.OnDimensionValueChangedAsync(args);
            var property = ConfigUtilities.GetDimension(rootElement, PropertyName);
            Assert.NotNull(property);
            Assert.Equal("A;B;C", property!.Value);

            // On ChangeEventStage.Before the property should be removed
            args = new ProjectConfigurationDimensionValueChangedEventArgs(
                project,
                ConfigurationDimensionChange.Delete,
                ChangeEventStage.Before,
                DimensionName,
                "B");
            await provider.OnDimensionValueChangedAsync(args);
            property = ConfigUtilities.GetDimension(rootElement, PropertyName);
            Assert.NotNull(property);
            Assert.Equal("A;C", property!.Value);
        }

        [Fact]
        public async Task OnDimensionValueChanged_Remove_UnknownValue()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
    <PROP>A;B;C</PROP>
  </PropertyGroup>
</Project>";

            var rootElement = ProjectRootElementFactory.Create(projectXml.Replace("PROP", PropertyName));
            var projectAccessor = IProjectAccessorFactory.Create(rootElement);
            var configuredProject = ConfiguredProjectFactory.Create();
            var provider = CreateInstance(projectAccessor);
            var project = UnconfiguredProjectFactory.Create(configuredProject: configuredProject);

            var args = new ProjectConfigurationDimensionValueChangedEventArgs(
                project,
                ConfigurationDimensionChange.Delete,
                ChangeEventStage.Before,
                DimensionName,
                "Unknown");
            await Assert.ThrowsAsync<ArgumentException>(() => provider.OnDimensionValueChangedAsync(args));
            var property = ConfigUtilities.GetDimension(rootElement, PropertyName);
            Assert.NotNull(property);
            Assert.Equal("A;B;C", property!.Value);
        }

        [Fact]
        public async Task OnDimensionValueChanged_Rename()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
    <PROP>A;B;C</PROP>
  </PropertyGroup>
</Project>";

            var rootElement = ProjectRootElementFactory.Create(projectXml.Replace("PROP", PropertyName));
            var projectAccessor = IProjectAccessorFactory.Create(rootElement);
            var configuredProject = ConfiguredProjectFactory.Create();
            var provider = CreateInstance(projectAccessor);
            var project = UnconfiguredProjectFactory.Create(configuredProject: configuredProject);

            // On ChangeEventStage.Before nothing should be changed
            var args = new ProjectConfigurationDimensionValueChangedEventArgs(
                project,
                ConfigurationDimensionChange.Rename,
                ChangeEventStage.Before,
                DimensionName,
                "Renamed",
                "B");
            await provider.OnDimensionValueChangedAsync(args);
            var property = ConfigUtilities.GetDimension(rootElement, PropertyName);
            Assert.NotNull(property);
            Assert.Equal("A;B;C", property!.Value);

            // On ChangeEventStage.Before the property should be renamed
            args = new ProjectConfigurationDimensionValueChangedEventArgs(
                project,
                ConfigurationDimensionChange.Rename,
                ChangeEventStage.After,
                DimensionName,
                "Renamed",
                "B");
            await provider.OnDimensionValueChangedAsync(args);
            property = ConfigUtilities.GetDimension(rootElement, PropertyName);
            Assert.NotNull(property);
            Assert.Equal("A;Renamed;C", property!.Value);
        }

        [Fact]
        public async Task OnDimensionValueChanged_Rename_UnknownValue()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
    <PROP>A;B;C</PROP>
  </PropertyGroup>
</Project>";

            var rootElement = ProjectRootElementFactory.Create(projectXml.Replace("PROP", PropertyName));
            var projectAccessor = IProjectAccessorFactory.Create(rootElement);
            var configuredProject = ConfiguredProjectFactory.Create();
            var provider = CreateInstance(projectAccessor);
            var project = UnconfiguredProjectFactory.Create(configuredProject: configuredProject);

            var args = new ProjectConfigurationDimensionValueChangedEventArgs(
                project,
                ConfigurationDimensionChange.Rename,
                ChangeEventStage.After,
                DimensionName,
                "Renamed",
                "Unknown");
            await Assert.ThrowsAsync<ArgumentException>(() => provider.OnDimensionValueChangedAsync(args));
            var property = ConfigUtilities.GetDimension(rootElement, PropertyName);
            Assert.NotNull(property);
            Assert.Equal("A;B;C", property!.Value);
        }

        [Theory]
        [InlineData("net45", "net45")]
        [InlineData(" net45 ", "net45")]
        [InlineData("net46", "net46")]
        [InlineData("net45;", "net45")]
        [InlineData("net45;net46", "net45")]
        [InlineData(";net45;net46", "net45")]
        [InlineData("$(Foo);net45;net46", "net45")]
        [InlineData("$(Foo); net45 ;net46", "net45")]
        [InlineData("net45_$(Foo); net45 ;net46", "net45")]
        public async Task GetBestGuessDefaultValuesForDimensionsAsync_ReturnsFirstParseableValue(string propertyValue, string expected)
        {
            string projectXml =
$@"<Project>
  <PropertyGroup>
    <PROP>{propertyValue}</PROP>
  </PropertyGroup>
</Project>";

            var provider = CreateInstance(projectXml.Replace("PROP", PropertyName));

            var result = await provider.GetBestGuessDefaultValuesForDimensionsAsync(UnconfiguredProjectFactory.Create());

            Assert.Single(result);
            Assert.Equal(provider.DimensionName, result.First().Key);
            Assert.Equal(expected, result.First().Value);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(";")]
        [InlineData(" ;")]
        [InlineData(" ; ")]
        [InlineData(";;;")]
        [InlineData("$(Property)")]
        [InlineData("Foo_$(Property)")]
        [InlineData("Foo_$(Property);")]
        [InlineData(";Foo_$(Property);")]
        public async Task GetBestGuessDefaultValuesForDimensionsAsync_WhenPropertyIsEmpty_ReturnsDefaultOrEmpty(string propertyValue)
        {
            string projectXml =
$@"<Project>
  <PropertyGroup>
    <PROP>{propertyValue}</PROP>
  </PropertyGroup>
</Project>";

            var provider = CreateInstance(projectXml.Replace("PROP", PropertyName));

            var result = await provider.GetBestGuessDefaultValuesForDimensionsAsync(UnconfiguredProjectFactory.Create());

            AssertDefaultOrEmpty(provider, result);
        }

        [Fact]
        public async Task GetBestGuessDefaultValuesForDimensionsAsync_ReturnsFirstValueFromLastElement()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
    <PROP>first</PROP>
    <PROP>last</PROP>
  </PropertyGroup>
</Project>";

            var provider = CreateInstance(projectXml.Replace("PROP", PropertyName));

            var result = await provider.GetBestGuessDefaultValuesForDimensionsAsync(UnconfiguredProjectFactory.Create());

            Assert.Single(result);
            Assert.Equal(provider.DimensionName, result.First().Key);
            Assert.Equal("last", result.First().Value);
        }

        [Fact]
        public async Task GetBestGuessDefaultValuesForDimensionsAsync_WhenPropertyIsMissing_ReturnsDefaultOrEmpty()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
  </PropertyGroup>
</Project>";

            var provider = CreateInstance(projectXml);

            var result = await provider.GetBestGuessDefaultValuesForDimensionsAsync(UnconfiguredProjectFactory.Create());

            AssertDefaultOrEmpty(provider, result);
        }

        [Theory]
        [InlineData(
            @"<Project>
  <PropertyGroup>
    <PROP Condition=""'$(BuildingInsideVisualStudio)' != 'true'"">net45</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
            @"<Project>
  <PropertyGroup>
    <PROP Condition=""'$(BuildingInsideVisualStudio)' != 'true'"">net45</PROP>
    <TargetFramework>net45</TargetFramework>
  </PropertyGroup>
</Project>")]
        [InlineData(
            @"<Project>
  <PropertyGroup>
    <TargetFramework>net45</TargetFramework>
    <PROP Condition=""'$(BuildingInsideVisualStudio)' != 'true'"">net45</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
            @"<Project>
  <PropertyGroup>
    <PROP Condition=""'$(OS)' != 'Windows_NT'"">net45</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
            @"<Project>
  <PropertyGroup>
    <PROP Condition=""'$(OS)' == 'Unix'"">net45</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
            @"<Project>
  <PropertyGroup>
    <PROP Condition=""'$(Foo)' == 'true'"">net45</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
            @"<Project>
  <PropertyGroup>
    <TargetFramework>net45</TargetFramework>
    <PROP Condition=""'$(OS)' != 'Windows_NT'"">net45</PROP>
  </PropertyGroup>
</Project>")]
        public async Task GetBestGuessDefaultValuesForDimensionsAsync_WhenPropertyHasUnrecognizedCondition_ReturnsDefaultOrEmpty(string projectXml)
        {
            var provider = CreateInstance(projectXml.Replace("PROP", PropertyName));

            var result = await provider.GetBestGuessDefaultValuesForDimensionsAsync(UnconfiguredProjectFactory.Create());

            AssertDefaultOrEmpty(provider, result);
        }

        [Theory]
        [InlineData(
@"<Project>
  <PropertyGroup>
    <PROP Condition=""'$(BuildingInsideVisualStudio)' == 'true'"">expected</PROP>
    <PROP Condition=""'$(BuildingInsideVisualStudio)' != 'true'"">other</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
@"<Project>
  <PropertyGroup>
    <PROP Condition=""'$(OS)' == 'Windows_NT'"">expected</PROP>
    <PROP Condition=""'$(OS)' != 'Windows_NT'"">other</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
@"<Project>
  <PropertyGroup>
    <PROP Condition=""'$(OS)' == 'Windows_NT'"">expected</PROP>
    <PROP Condition=""'$(OS)' == 'Unix'"">other</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
@"<Project>
  <PropertyGroup>
    <PROP Condition=""true"">expected</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
@"<Project>
  <PropertyGroup>
    <PROP Condition="""">expected</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
@"<Project>
  <PropertyGroup>
    <PROP Condition=""'$(BuildingInsideVisualStudio)' == 'true'"">other</PROP>
    <PROP>expected</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
@"<Project>
  <PropertyGroup>
    <PROP>other</PROP>
    <PROP Condition=""'$(BuildingInsideVisualStudio)' == 'true'"">expected</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
@"<Project>
  <PropertyGroup>
    <PROP Condition=""'$(BuildingInsideVisualStudio)' != 'true'"">other</PROP>
    <PROP>expected</PROP>
  </PropertyGroup>
</Project>")]
        [InlineData(
@"<Project>
  <PropertyGroup>
    <PROP>expected</PROP>
    <PROP Condition=""'$(BuildingInsideVisualStudio)' != 'true'"">other</PROP>
  </PropertyGroup>
</Project>")]
        public async Task GetBestGuessDefaultValuesForDimensionsAsync_WhenPlatformsHasRecognizedCondition_ReturnsValue2222222222222222(string projectXml)
        {
            var provider = CreateInstance(projectXml.Replace("PROP", PropertyName));

            var result = await provider.GetBestGuessDefaultValuesForDimensionsAsync(UnconfiguredProjectFactory.Create());

            Assert.Single(result);
            Assert.Equal(provider.DimensionName, result.First().Key);
            Assert.Equal("expected", result.First().Value);
        }

        private static void AssertDefaultOrEmpty(BaseProjectConfigurationDimensionProvider provider, IEnumerable<KeyValuePair<string, string>> result)
        {
            if (provider.DimensionDefaultValue != null)
            {
                Assert.Single(result);
                Assert.Equal(provider.DimensionName, result.First().Key);
                Assert.Equal(provider.DimensionDefaultValue, result.First().Value);
            }
            else
            {
                Assert.Empty(result);
            }
        }
    }
}
