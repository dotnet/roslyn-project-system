﻿// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Linq;
using Microsoft.Build.Construction;
using Xunit;

namespace Microsoft.VisualStudio.Build
{
    public class ConfigUtilitiesTests
    {
        [Fact]
        public void GetProperty_MissingProperty()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty>MyPropertyValue</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            var property = ConfigUtilities.GetProperty(project, "NonExistentProperty");
            Assert.Null(property);
        }

        [Fact]
        public void GetProperty_ExistentProperty()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty>MyPropertyValue</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            var property = ConfigUtilities.GetProperty(project, "MyProperty");
            Assert.NotNull(property);
        }

        [Fact]
        public void GetPropertyValues_SingleValue()
        {
            var values = ConfigUtilities.GetPropertyValues("MyPropertyValue");
            Assert.Collection(values, firstValue => Assert.Equal("MyPropertyValue", firstValue));
        }

        [Fact]
        public void GetPropertyValues_MultipleValues()
        {
            var values = ConfigUtilities.GetPropertyValues("1;2");
            Assert.Collection(values,
                firstValue => Assert.Equal("1", firstValue),
                secondValue => Assert.Equal("2", secondValue));
        }

        [Fact]
        public void GetPropertyValues_EmptyValues()
        {
            var values = ConfigUtilities.GetPropertyValues("1;   ;;;2");
            Assert.Collection(values,
                firstValue => Assert.Equal("1", firstValue),
                secondValue => Assert.Equal("2", secondValue));
        }

        [Fact]
        public void GetPropertyValues_WhiteSpace()
        {
            var values = ConfigUtilities.GetPropertyValues("   1;   ; ; ; 2 ");
            Assert.Collection(values,
                firstValue => Assert.Equal("1", firstValue),
                secondValue => Assert.Equal("2", secondValue));
        }

        [Fact]
        public void GetPropertyValues_Duplicates()
        {
            var values = ConfigUtilities.GetPropertyValues("1;2;1;1;2;2;2;1");
            Assert.Collection(values,
                firstValue => Assert.Equal("1", firstValue),
                secondValue => Assert.Equal("2", secondValue));
        }

        [Fact]
        public void GetOrAddProperty_NoGroups()
        {
            var project = ProjectRootElementFactory.Create();
            ConfigUtilities.GetOrAddProperty(project, "MyProperty");
            Assert.Single(project.Properties);
            Assert.Collection(project.PropertyGroups,
                group => Assert.Collection(group.Properties,
                    firstProperty => Assert.Equal(string.Empty, firstProperty.Value)));
        }

        [Fact]
        public void GetOrAddProperty_FirstGroup()
        {
            string projectXml =
@"<Project>
  <PropertyGroup/>
  <PropertyGroup/>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.GetOrAddProperty(project, "MyProperty");
            Assert.Single(project.Properties);
            AssertEx.CollectionLength(project.PropertyGroups, 2);

            var group = project.PropertyGroups.First();
            Assert.Single(group.Properties);

            var property = group.Properties.First();
            Assert.Equal(string.Empty, property.Value);
        }

        [Fact]
        public void GetOrAddProperty_ExistingProperty()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
    <MyProperty>1</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.GetOrAddProperty(project, "MyProperty");
            Assert.Single(project.Properties);
            Assert.Single(project.PropertyGroups);

            var group = project.PropertyGroups.First();
            Assert.Single(group.Properties);

            var property = group.Properties.First();
            Assert.Equal("1", property.Value);
        }

        [Fact]
        public void AppendPropertyValue_DefaultDelimiter()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty>1;2</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.AppendPropertyValue(project, "1;2", "MyProperty", "3");
            var property = ConfigUtilities.GetProperty(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("1;2;3", property!.Value);
        }

        [Fact]
        public void AppendPropertyValue_EmptyProperty()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty/>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.AppendPropertyValue(project, "", "MyProperty", "1");
            var property = ConfigUtilities.GetProperty(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("1", property!.Value);
        }

        [Fact]
        public void AppendPropertyValue_InheritedValue()
        {
            var project = ProjectRootElementFactory.Create();
            ConfigUtilities.AppendPropertyValue(project, "1;2", "MyProperty", "3");
            var property = ConfigUtilities.GetProperty(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("1;2;3", property!.Value);
        }

        [Fact]
        public void AppendPropertyValue_MissingProperty()
        {
            var project = ProjectRootElementFactory.Create();
            ConfigUtilities.AppendPropertyValue(project, "", "MyProperty", "1");
            var property = ConfigUtilities.GetProperty(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("1", property!.Value);
        }

        [Fact]
        public void RemovePropertyValue_DefaultDelimiter()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty>1;2</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.RemovePropertyValue(project, "1;2", "MyProperty", "2");
            var property = ConfigUtilities.GetProperty(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("1", property!.Value);
        }

        [Fact]
        public void RemovePropertyValue_EmptyAfterRemove()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty>1</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.RemovePropertyValue(project, "1", "MyProperty", "1");
            var property = ConfigUtilities.GetProperty(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal(string.Empty, property!.Value);
        }

        [Fact]
        public void RemovePropertyValue_InheritedValue()
        {
            var project = ProjectRootElementFactory.Create();
            ConfigUtilities.RemovePropertyValue(project, "1;2", "MyProperty", "1");
            var property = ConfigUtilities.GetProperty(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("2", property!.Value);
        }

        [Fact]
        public void RemovePropertyValue_MissingProperty()
        {
            var project = ProjectRootElementFactory.Create();
            Assert.Throws<ArgumentException>("valueToRemove", () => ConfigUtilities.RemovePropertyValue(project, "", "MyProperty", "1"));
            var property = ConfigUtilities.GetProperty(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal(string.Empty, property!.Value);
        }

        [Fact]
        public void RenamePropertyValue_DefaultDelimiter()
        {
            string projectXml =
@"<Project>
  <PropertyGroup>
     <MyProperty>1;2</MyProperty>
  </PropertyGroup>
</Project>";

            var project = ProjectRootElementFactory.Create(projectXml);
            ConfigUtilities.RenamePropertyValue(project, "1;2", "MyProperty", "2", "5");
            var property = ConfigUtilities.GetProperty(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("1;5", property!.Value);
        }

        [Fact]
        public void RenamePropertyValue_InheritedValue()
        {
            var project = ProjectRootElementFactory.Create();
            ConfigUtilities.RenamePropertyValue(project, "1;2", "MyProperty", "1", "3");
            var property = ConfigUtilities.GetProperty(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal("3;2", property!.Value);
        }

        [Fact]
        public void RenamePropertyValue_MissingProperty()
        {
            var project = ProjectRootElementFactory.Create();
            Assert.Throws<ArgumentException>("oldValue", () => ConfigUtilities.RenamePropertyValue(project, "", "MyProperty", "1", "2"));
            var property = ConfigUtilities.GetProperty(project, "MyProperty");
            Assert.NotNull(property);
            Assert.Equal(string.Empty, property!.Value);
        }
    }
}