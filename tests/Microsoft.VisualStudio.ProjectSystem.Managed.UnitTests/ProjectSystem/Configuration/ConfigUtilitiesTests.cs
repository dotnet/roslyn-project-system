// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Linq;
using Microsoft.Build.Construction;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.Configuration
{
    public class ConfigUtilitiesTests
    {
        [Fact]
        public void GetDimension_MissingProperty()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty>MyPropertyValue</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            var property = ConfigUtilities.GetDimension(project, "NonExistentProperty");
            Assert.Null(property);
        }

        [Fact]
        public void GetDimension_ExistentProperty()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty>MyPropertyValue</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            var property = ConfigUtilities.GetDimension(project, "MyProperty");
            Assert.NotNull(property);
        }

        [Fact]
        public void EnumerateDimensionValues_SingleValue()
        {
            var values = ConfigUtilities.EnumerateDimensionValues("MyPropertyValue");
            Assert.Collection(values, firstValue => Assert.Equal("MyPropertyValue", firstValue));
        }

        [Fact]
        public void EnumerateDimensionValues_MultipleValues()
        {
            var values = ConfigUtilities.EnumerateDimensionValues("1;2");
            Assert.Collection(values,
                firstValue => Assert.Equal("1", firstValue),
                secondValue => Assert.Equal("2", secondValue));
        }

        [Fact]
        public void EnumerateDimensionValues_EmptyValues()
        {
            var values = ConfigUtilities.EnumerateDimensionValues("1;   ;;;2");
            Assert.Collection(values,
                firstValue => Assert.Equal("1", firstValue),
                secondValue => Assert.Equal("2", secondValue));
        }

        [Fact]
        public void EnumerateDimensionValues_WhiteSpace()
        {
            var values = ConfigUtilities.EnumerateDimensionValues("   1;   ; ; ; 2 ");
            Assert.Collection(values,
                firstValue => Assert.Equal("1", firstValue),
                secondValue => Assert.Equal("2", secondValue));
        }

        [Fact]
        public void EnumerateDimensionValues_Duplicates()
        {
            var values = ConfigUtilities.EnumerateDimensionValues("1;2;1;1;2;2;2;1");
            Assert.Collection(values,
                firstValue => Assert.Equal("1", firstValue),
                secondValue => Assert.Equal("2", secondValue));
        }

        [Fact]
        public void GetOrAddDimension_NoGroups()
        {
            var project = ProjectRootElementFactory.Create();
            ConfigUtilities.GetOrAddDimension(project, "MyProperty");
            Assert.Single(project.Properties);
            Assert.Collection(project.PropertyGroups,
                group => Assert.Collection(group.Properties,
                    firstProperty => Assert.Equal(string.Empty, firstProperty.Value)));
        }

        [Fact]
        public void GetOrAddDimension_FirstGroup()
        {
            string projectXml =
@"<Project>
  <PropertyGroup/>
  <PropertyGroup/>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.GetOrAddDimension(project, "MyProperty");
            Assert.Single(project.Properties);
            AssertEx.CollectionLength(project.PropertyGroups, 2);

            var group = project.PropertyGroups.First();
            Assert.Single(group.Properties);

            var property = group.Properties.First();
            Assert.Equal(string.Empty, property.Value);
        }

        [Fact]
        public void GetOrAddDimension_ExistingProperty()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
    <MyProperty>1</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.GetOrAddDimension(project, "MyProperty");
            Assert.Single(project.Properties);
            Assert.Single(project.PropertyGroups);

            var group = project.PropertyGroups.First();
            Assert.Single(group.Properties);

            var property = group.Properties.First();
            Assert.Equal("1", property.Value);
        }

        [Fact]
        public void AppendDimensionValue_DefaultDelimiter()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty>1;2</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.AppendDimensionValue(project, "1;2", "MyProperty", "3");
            var property = ConfigUtilities.GetDimension(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("1;2;3", property!.Value);
        }

        [Fact]
        public void AppendDimensionValue_EmptyProperty()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty/>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.AppendDimensionValue(project, "", "MyProperty", "1");
            var property = ConfigUtilities.GetDimension(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("1", property!.Value);
        }

        [Fact]
        public void AppendDimensionValue_InheritedValue()
        {
            var project = ProjectRootElementFactory.Create();
            ConfigUtilities.AppendDimensionValue(project, "1;2", "MyProperty", "3");
            var property = ConfigUtilities.GetDimension(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("1;2;3", property!.Value);
        }

        [Fact]
        public void AppendDimensionValue_MissingProperty()
        {
            var project = ProjectRootElementFactory.Create();
            ConfigUtilities.AppendDimensionValue(project, "", "MyProperty", "1");
            var property = ConfigUtilities.GetDimension(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("1", property!.Value);
        }

        [Fact]
        public void RemoveDimensionValue_DefaultDelimiter()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty>1;2</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.RemoveDimensionValue(project, "1;2", "MyProperty", "2");
            var property = ConfigUtilities.GetDimension(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("1", property!.Value);
        }

        [Fact]
        public void RemoveDimensionValue_EmptyAfterRemove()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty>1</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.RemoveDimensionValue(project, "1", "MyProperty", "1");
            var property = ConfigUtilities.GetDimension(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal(string.Empty, property!.Value);
        }

        [Fact]
        public void RemoveDimensionValue_InheritedValue()
        {
            var project = ProjectRootElementFactory.Create();
            ConfigUtilities.RemoveDimensionValue(project, "1;2", "MyProperty", "1");
            var property = ConfigUtilities.GetDimension(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("2", property!.Value);
        }

        [Fact]
        public void RemoveDimensionValue_MissingProperty()
        {
            var project = ProjectRootElementFactory.Create();
            Assert.Throws<ArgumentException>("valueToRemove", () => ConfigUtilities.RemoveDimensionValue(project, "", "MyProperty", "1"));
            var property = ConfigUtilities.GetDimension(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal(string.Empty, property!.Value);
        }

        [Fact]
        public void RenameDimensionValue_DefaultDelimiter()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty>1;2</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.RenameDimensionValue(project, "1;2", "MyProperty", "2", "5");
            var property = ConfigUtilities.GetDimension(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("1;5", property!.Value);
        }

        [Fact]
        public void RenameDimensionValue_InheritedValue()
        {
            var project = ProjectRootElementFactory.Create();
            ConfigUtilities.RenameDimensionValue(project, "1;2", "MyProperty", "1", "3");
            var property = ConfigUtilities.GetDimension(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("3;2", property!.Value);
        }

        [Fact]
        public void RenameDimensionValue_MissingProperty()
        {
            var project = ProjectRootElementFactory.Create();
            Assert.Throws<ArgumentException>("oldValue", () => ConfigUtilities.RenameDimensionValue(project, "", "MyProperty", "1", "2"));
            var property = ConfigUtilities.GetDimension(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal(string.Empty, property!.Value);
        }
    }
}
