// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.Rules
{
    public sealed class ItemRuleTests : XamlRuleTestBase
    {
        [Theory]
        [MemberData(nameof(GetFileItemRules))]
        public void FileRulesShouldMatchNone(string ruleName, string fullPath)
        {
            // Special case for Folder rule which hasn't been split yet, but is in the Items folder. But also its completely different.
            if (ruleName.Equals("Folder", StringComparison.Ordinal))
            {
                return;
            }

            // No need to check None against None
            if (ruleName.Equals("None", StringComparison.Ordinal))
            {
                return;
            }

            // F# has its own overrides which we can skip
            if (ruleName.EndsWith(".FSharp", StringComparison.Ordinal))
            {
                return;
            }

            string noneFile = Path.Combine(fullPath, "..", "None.xaml");

            XElement none = LoadXamlRule(noneFile);
            XElement rule = LoadXamlRule(fullPath);

            // First fix up the Name as we know they'll differ.
            rule.Attribute("Name").Value = "None";

            AssertXmlEqual(none, rule);
        }

        [Theory]
        [MemberData(nameof(GetBrowseObjectItemRules))]
        public void BrowseObjectRulesShouldMatchNone(string ruleName, string fullPath)
        {
            // Special case for Folder rule which hasn't been split yet, but is in the Items folder. But also its completely different.
            if (ruleName.Equals("Folder.BrowseObject", StringComparison.Ordinal))
            {
                return;
            }
            // No need to check None against None
            if (ruleName.Equals("None.BrowseObject", StringComparison.Ordinal))
            {
                return;
            }

            string[] excludedElements = Array.Empty<string>();
            if (ruleName.Equals("EmbeddedResource.BrowseObject", StringComparison.Ordinal))
            {
                excludedElements = new[] { "LogicalName", "ManifestResourceName" };
            }

            string noneFile = Path.Combine(fullPath, "..", "None.BrowseObject.xaml");

            XElement none = LoadXamlRule(noneFile);
            XElement rule = LoadXamlRule(fullPath);

            // First fix up the Name and DisplayName as we know they'll differ.
            rule.Attribute("Name").Value = "None";
            rule.Attribute("DisplayName").Value = "General";

            var elementsToRemove = new List<XElement>();

            foreach (var element in rule.Elements())
            {
                if (excludedElements.Contains(element.Attribute("Name")?.Value))
                {
                    elementsToRemove.Add(element);
                }
            }

            elementsToRemove.ForEach(element => element.Remove());

            AssertXmlEqual(none, rule);
        }

        [Theory]
        [MemberData(nameof(GetFileItemRules))]
        public void FileRulesShouldntBeLocalized(string ruleName, string fullPath)
        {
            // Special case for Folder rule which hasn't been split yet, but is in the Items folder
            if (ruleName.Equals("Folder", StringComparison.Ordinal))
            {
                return;
            }

            XElement rule = LoadXamlRule(fullPath);

            foreach (XElement element in rule.Elements())
            {
                // No need to define categories if they're not going to be used
                Assert.False(element.Name.LocalName.Equals("Rule.Categories", StringComparison.Ordinal));
                Assert.Null(element.Attribute("DisplayName"));
                Assert.Null(element.Attribute("Description"));
                Assert.Null(element.Attribute("Category"));
            }
        }

        [Theory]
        [MemberData(nameof(GetBrowseObjectItemRules))]
        public void HidePropertyPagesForBrowseObjectRules(string ruleName, string fullPath)
        {
            XElement rule = LoadXamlRule(fullPath);
            XAttribute attribute = rule.Attribute("PropertyPagesHidden");

            Assert.NotNull(attribute);
            Assert.Equal("true", attribute.Value, ignoreCase: true);
        }

        [Theory]
        [MemberData(nameof(GetItemRules))]
        public void RuleNameMatchesFileName(string ruleName, string fullPath)
        {
            XElement rule = LoadXamlRule(fullPath);

            // If a rule is split between File and BrowseObject we need to trim the BrowseObject part off
            if (ruleName.IndexOf('.') > -1)
            {
                ruleName = ruleName.Substring(0, ruleName.IndexOf('.'));
            }

            Assert.Equal(ruleName, rule.Attribute("Name").Value);
        }

        [Theory]
        [MemberData(nameof(GetItemRules))]
        public void ItemTypesMustMatchFileNameRoot(string ruleName, string fullPath)
        {
            // If a rule is split between File and BrowseObject we need to trim the BrowseObject part off
            if (ruleName.IndexOf('.') > -1)
            {
                ruleName = ruleName.Substring(0, ruleName.IndexOf('.'));
            }

            XElement rule = LoadXamlRule(fullPath);

            foreach (var element in rule.Descendants())
            {
                var attribute = element.Attribute("ItemType");

                if (attribute != null)
                {
                    Assert.Equal(ruleName, attribute.Value);
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetFileItemRules))]
        public void FileRulesShouldntHaveFileInfo(string ruleName, string fullPath)
        {
            XElement rule = LoadXamlRule(fullPath);

            foreach (XElement element in rule.Elements())
            {
                var nameAttribute = element.Attribute("Name");
                if (nameAttribute == null)
                {
                    continue;
                }

                var isStringProperty = element.Name.LocalName.Equals("StringProperty", StringComparison.Ordinal);

                if (!isStringProperty)
                {
                    continue;
                }

                var name = nameAttribute.Value;

                // special case - Folder's Identity property is used by dependencies node
                if (!ruleName.Equals("Folder", StringComparison.Ordinal))
                {
                    Assert.False(name.Equals("Identity", StringComparison.Ordinal));
                }

                Assert.False(name.Equals("FileNameAndExtension", StringComparison.Ordinal));
                Assert.False(name.Equals("URL", StringComparison.Ordinal));
                Assert.False(name.Equals("Extension", StringComparison.Ordinal));
            }
        }

        public static IEnumerable<object[]> GetBrowseObjectItemRules()
        {
            // Special case for Folder because it is both File and BrowseObject context (for now), but is named like a File.
            return Project(GetRules("Items")
                .Where(fileName => fileName.EndsWith(".BrowseObject.xaml", StringComparisons.Paths) ||
                                   fileName.Equals("Folder.xaml", StringComparisons.Paths)));
        }

        public static IEnumerable<object[]> GetFileItemRules()
        {
            return Project(GetRules("Items")
                .Where(fileName => !fileName.EndsWith(".BrowseObject.xaml", StringComparisons.Paths)));
        }

        public static IEnumerable<object[]> GetItemRules()
        {
            return Project(GetRules("Items"));
        }

        private static void AssertXmlEqual(XElement left, XElement right)
        {
            Assert.Equal(left.Name.LocalName, right.Name.LocalName, ignoreCase: true);

            var leftAttributes = left.Attributes().OrderBy(a => a.Name.LocalName).ToArray();
            var rightAttributes = right.Attributes().OrderBy(a => a.Name.LocalName).ToArray();
            Assert.Equal(leftAttributes.Length, rightAttributes.Length);

            for (int i = 0; i < leftAttributes.Length; i++)
            {
                AssertAttributesEqual(leftAttributes[i], rightAttributes[i]);
            }

            var leftChildNodes = left.Elements().OrderBy(a => a.Name.LocalName).ToArray();
            var rightChildNodes = right.Elements().OrderBy(a => a.Name.LocalName).ToArray();
            Assert.Equal(leftChildNodes.Length, rightChildNodes.Length);

            for (int i = 0; i < leftChildNodes.Length; i++)
            {
                AssertXmlEqual(leftChildNodes[i], rightChildNodes[i]);
            }

            static void AssertAttributesEqual(XAttribute left, XAttribute right)
            {
                Assert.Equal(left.Name.LocalName, right.Name.LocalName, ignoreCase: true);

                // ignore ItemType as we know they'll be different
                if (left.Name.LocalName.Equals("ItemType", StringComparison.Ordinal))
                {
                    return;
                }

                Assert.Equal(left.Value, right.Value, ignoreCase: true);
            }
        }
    }
}
