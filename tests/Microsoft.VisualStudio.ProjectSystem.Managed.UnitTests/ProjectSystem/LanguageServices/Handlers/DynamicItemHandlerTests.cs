// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.VisualStudio.LanguageServices.ProjectSystem;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.LanguageServices.Handlers
{
    public class DynamicItemHandlerTests : SourceItemsHandlerTestBase
    {
        [Fact]
        public void Handle_RazorAndCshtmlFiles_AddsToContext()
        {
            var dynamicFiles = new HashSet<string>(StringComparers.Paths);
            void onDynamicFileAdded(string s) => Assert.True(dynamicFiles.Add(s));

            var project = UnconfiguredProjectFactory.Create(fullPath: @"C:\Myproject.csproj");
            var context = IWorkspaceProjectContextMockFactory.CreateForDynamicFiles(project, onDynamicFileAdded);

            var handler = CreateInstance(project, context);

            var projectChanges = ImmutableDictionary<string, IProjectChangeDescription>.Empty.Add(
                "None", IProjectChangeDescriptionFactory.FromJson(
@"{
    ""Difference"": { 
        ""AnyChanges"": true,
        ""AddedItems"": [ ""File1.razor"", ""File1.cshtml"", ""File1.cs"" ]
    }
}"));

            Handle(handler, projectChanges);

            Assert.Equal(2, dynamicFiles.Count);
            Assert.Contains(@"C:\File1.razor", dynamicFiles);
            Assert.Contains(@"C:\File1.cshtml", dynamicFiles);
        }

        [Fact]
        public void Handle_RazorAndCshtmlFiles_InDifferentItemTypes_AddsToContext()
        {
            var dynamicFiles = new HashSet<string>(StringComparers.Paths);
            void onDynamicFileAdded(string s) => Assert.True(dynamicFiles.Add(s));

            var project = UnconfiguredProjectFactory.Create(fullPath: @"C:\Myproject.csproj");
            var context = IWorkspaceProjectContextMockFactory.CreateForDynamicFiles(project, onDynamicFileAdded);

            var handler = CreateInstance(project, context);

            var projectChanges = ImmutableDictionary<string, IProjectChangeDescription>.Empty.Add(
                "None", IProjectChangeDescriptionFactory.FromJson(
@"{
    ""Difference"": { 
        ""AnyChanges"": true,
        ""AddedItems"": [ ""File1.razor"", ""File1.cs"" ]
    }
}")).Add(
                "Content", IProjectChangeDescriptionFactory.FromJson(
@"{
    ""Difference"": { 
        ""AnyChanges"": true,
        ""AddedItems"": [ ""File1.cshtml"", ""File2.cs"" ]
    }
}"));

            Handle(handler, projectChanges);

            Assert.Equal(2, dynamicFiles.Count);
            Assert.Contains(@"C:\File1.razor", dynamicFiles);
            Assert.Contains(@"C:\File1.cshtml", dynamicFiles);
        }

        [Fact]
        public void Handle_RazorAndCshtmlFiles_InDifferentItemTypes_IgnoresDuplicates()
        {
            var dynamicFiles = new HashSet<string>(StringComparers.Paths);
            void onDynamicFileAdded(string s) => Assert.True(dynamicFiles.Add(s));

            var project = UnconfiguredProjectFactory.Create(fullPath: @"C:\Myproject.csproj");
            var context = IWorkspaceProjectContextMockFactory.CreateForDynamicFiles(project, onDynamicFileAdded);

            var handler = CreateInstance(project, context);

            var projectChanges = ImmutableDictionary<string, IProjectChangeDescription>.Empty.Add(
                "None", IProjectChangeDescriptionFactory.FromJson(
@"{
    ""Difference"": { 
        ""AnyChanges"": true,
        ""AddedItems"": [ ""File1.razor"", ""File1.cs"" ]
    }
}")).Add(
                "Content", IProjectChangeDescriptionFactory.FromJson(
@"{
    ""Difference"": { 
        ""AnyChanges"": true,
        ""AddedItems"": [ ""File1.razor"", ""File1.cshtml"", ""File2.cs"" ]
    }
}"));

            Handle(handler, projectChanges);

            Assert.Equal(2, dynamicFiles.Count);
            Assert.Contains(@"C:\File1.razor", dynamicFiles);
            Assert.Contains(@"C:\File1.cshtml", dynamicFiles);
        }

        internal override ISourceItemsHandler CreateInstance()
        {
            return CreateInstance(null, null);
        }

        private static DynamicItemHandler CreateInstance(UnconfiguredProject? project = null, IWorkspaceProjectContext? context = null)
        {
            project ??= UnconfiguredProjectFactory.Create();

            var handler = new DynamicItemHandler(project);
            if (context != null)
                handler.Initialize(context);

            return handler;
        }
    }
}
