// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio.Buffers.PooledObjects;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.ProjectSystem.Configuration
{
    /// <summary>
    /// Provides methods for manipulating configuration dimension properties and property values.
    /// </summary>
    internal static class ConfigUtilities
    {
        private const char Delimiter = ';';

        /// <summary>
        ///     Returns a value indicating whether the specified property has a condition that
        ///     always evaluates to <see langword="true"/>.
        /// </summary>
        public static bool HasWellKnownConditionsThatAlwaysEvaluateToTrue(ProjectPropertyElement element)
        {
            Requires.NotNull(element, nameof(element));

            // We look for known conditions that evaluate to true so that 
            // projects can have patterns where they opt in/out of target 
            // frameworks based on whether they are inside Visual Studio or not.

            // For example:
            // 
            // <TargetFrameworks>net461;net452</TargetFrameworks>
            // <TargetFrameworks Condition = "'$(BuildingInsideVisualStudio)' == 'true'">net461</TargetFrameworks>

            switch (element.Condition)
            {
                case "":
                case "true":
                case "'$(OS)' == 'Windows_NT'":
                case "'$(BuildingInsideVisualStudio)' == 'true'":
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the dimension with the specified name.
        /// </summary>
        /// <param name="project">Xml representation of the MsBuild project.</param>
        /// <param name="name">Name of the property.</param>
        /// <returns>A <see cref="ProjectPropertyElement"/> representing the dimension; otherwise, <see langword="null"/> if the dimension is not found.</returns>
        public static ProjectPropertyElement? GetDimension(ProjectRootElement project, string name)
        {
            Requires.NotNull(project, nameof(project));

            return project.Properties
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparisons.PropertyNames));
        }

        /// <summary>
        /// Enumerates the individual dimension values.
        /// </summary>
        /// <param name="values">Values of the dimension to evaluate.</param>
        /// <returns>Collection of individual values in the property.</returns>
        public static IEnumerable<string> EnumerateDimensionValues(string values)
        {
            HashSet<string>? seen = null;

            // We need to ensure that we return values in the specified order.
            foreach (string value in new LazyStringSplit(values, Delimiter))
            {
                string s = value.Trim();

                if (!string.IsNullOrEmpty(s))
                {
                    if (seen == null)
                    {
                        seen = new HashSet<string>(StringComparers.ConfigurationDimensionValues) { s };
                        yield return s;
                    }
                    else
                    {
                        if (seen.Add(s))
                        {
                            yield return s;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Appends a value to a dimension. If the dimension does not exist it will be added.
        /// </summary>
        /// <param name="project">Xml representation of the MsBuild project.</param>
        /// <param name="values">Original evaluated value of the property.</param>
        /// <param name="name">Property name.</param>
        /// <param name="valueToAppend">Value to add to the property.</param>
        public static void AppendDimensionValue(ProjectRootElement project, string values, string name, string valueToAppend)
        {
            Requires.NotNull(project, nameof(project));
            Requires.NotNull(values, nameof(values));
            Requires.NotNullOrEmpty(name, nameof(name));

            ProjectPropertyElement property = GetOrAddDimension(project, name);
            var newValue = PooledStringBuilder.GetInstance();
            foreach (string value in EnumerateDimensionValues(values))
            {
                newValue.Append(value);
                newValue.Append(Delimiter);
            }

            newValue.Append(valueToAppend);
            property.Value = newValue.ToStringAndFree();
        }

        /// <summary>
        /// Renames a value inside the dimension values.
        /// </summary>
        /// <param name="project">Xml representation of the MsBuild project.</param>
        /// <param name="values">Original evaluated value of the property.</param>
        /// <param name="name">Property name.</param>
        /// <param name="valueToRemove">Value to remove from the property.</param>
        /// <remarks>
        /// If the property is not present it will be added. This means that the evaluated property
        /// value came from one of the project imports.
        /// </remarks>
        public static void RemoveDimensionValue(ProjectRootElement project, string values, string name, string valueToRemove)
        {
            Requires.NotNull(project, nameof(project));
            Requires.NotNull(values, nameof(values));
            Requires.NotNullOrEmpty(name, nameof(name));

            ProjectPropertyElement property = GetOrAddDimension(project, name);
            var newValue = new StringBuilder();
            bool valueFound = false;
            foreach (string value in EnumerateDimensionValues(values))
            {
                if (!string.Equals(value, valueToRemove, StringComparisons.ConfigurationDimensionValues))
                {
                    if (newValue.Length != 0)
                    {
                        newValue.Append(Delimiter);
                    }

                    newValue.Append(value);
                }
                else
                {
                    valueFound = true;
                }
            }

            property.Value = newValue.ToString();

            if (!valueFound)
            {
                throw new ArgumentException(string.Format(Resources.MsBuildMissingValueToRemove, valueToRemove, name), nameof(valueToRemove));
            }
        }

        /// <summary>
        /// Renames a value inside a delimited property.
        /// </summary>
        /// <param name="project">Xml representation of the MsBuild project.</param>
        /// <param name="values">Original evaluated value of the property.</param>
        /// <param name="name">Property name.</param>
        /// <param name="oldValue">Current property value.</param>
        /// <param name="newValue">New property value.</param>
        /// <remarks>
        /// If the property is not present it will be added. This means that the evaluated property
        /// value came from one of the project imports.
        /// </remarks>
        public static void RenameDimensionValue(ProjectRootElement project, string values, string name, string? oldValue, string newValue)
        {
            Requires.NotNull(project, nameof(project));
            Requires.NotNull(values, nameof(values));
            Requires.NotNullOrEmpty(name, nameof(name));

            ProjectPropertyElement property = GetOrAddDimension(project, name);
            var value = new StringBuilder();
            bool valueFound = false;
            foreach (string propertyValue in EnumerateDimensionValues(values))
            {
                if (value.Length != 0)
                {
                    value.Append(Delimiter);
                }

                if (string.Equals(propertyValue, oldValue, StringComparisons.ConfigurationDimensionValues))
                {
                    value.Append(newValue);
                    valueFound = true;
                }
                else
                {
                    value.Append(propertyValue);
                }
            }

            property.Value = value.ToString();

            if (!valueFound)
            {
                throw new ArgumentException(string.Format(Resources.MsBuildMissingValueToRename, oldValue, name), nameof(oldValue));
            }
        }

        /// <summary>
        /// Adds a property to the first property group. If there are no property groups it will create one.
        /// </summary>
        /// <param name="project">Xml representation of the MsBuild project.</param>
        /// <param name="name">Property name.</param>
        public static ProjectPropertyElement GetOrAddDimension(ProjectRootElement project, string name)
        {
            Requires.NotNull(project, nameof(project));
            ProjectPropertyElement? property = GetDimension(project, name);

            if (property != null)
            {
                return property;
            }
            else
            {
                ProjectPropertyGroupElement propertyGroup;
                if (project.PropertyGroups.Count == 0)
                {
                    propertyGroup = project.AddPropertyGroup();
                }
                else
                {
                    propertyGroup = project.PropertyGroups.First();
                }

                return propertyGroup.AddProperty(name, string.Empty);
            }
        }
    }
}
