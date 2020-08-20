﻿// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Mocks;
using Microsoft.VisualStudio.ProjectSystem.LanguageServices.CSharp;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Rename.CSharp
{
    public class RenamerTests : RenamerTestsBase
    {
        protected override string ProjectFileExtension => "csproj";

        [Theory]
        [InlineData("class Foo{}", "Foo.cs", "Bar.cs")]
        [InlineData("interface Foo { void m1(); void m2();}", "Foo.cs", "Bar.cs")]
        [InlineData("delegate int Foo(string s);", "Foo.cs", "Bar.cs")]
        [InlineData("partial class Foo {} partial class Foo {}", "Foo.cs", "Bar.cs")]
        [InlineData("struct Foo { decimal price; string title; string author;}", "Foo.cs", "Bar.cs")]
        [InlineData("enum Foo { None, enum1, enum2, enum3, enum4 };", "Foo.cs", "Bar.cs")]
        [InlineData("namespace n1 {class Foo{}} namespace n2 {class Foo{}}", "Foo.cs", "Bar.cs")]
        public async Task Rename_Symbol_Should_TriggerUserConfirmationAsync(string sourceCode, string oldFilePath, string newFilePath)
        {
            var userNotificationServices = IUserNotificationServicesFactory.Create();
            var roslynServices = IRoslynServicesFactory.Implement(new CSharpSyntaxFactsService());
            var vsOnlineServices = IVsOnlineServicesFactory.Create(online: false);

            await RenameAsync(sourceCode, oldFilePath, newFilePath, userNotificationServices, roslynServices, vsOnlineServices, LanguageNames.CSharp);

            Mock.Get(userNotificationServices).Verify(h => h.Confirm(It.IsAny<string>()), Times.Once);
            Mock.Get(roslynServices).Verify(h => h.RenameSymbolAsync(It.IsAny<Solution>(), It.IsAny<ISymbol>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("class Foo{}", "Bar.cs", "Foo.cs")]
        [InlineData("class Foo1{}", "Foo.cs", "Bar.cs")]
        [InlineData("interface Foo1 { void m1(); void m2();}", "Foo.cs", "Bar.cs")]
        [InlineData("delegate int Foo1(string s);", "Foo.cs", "Bar.cs")]
        [InlineData("partial class Foo1 {} partial class Foo2 {}", "Foo.cs", "Bar.cs")]
        [InlineData("struct Foo1 { decimal price; string title; string author;}", "Foo.cs", "Bar.cs")]
        [InlineData("enum Foo1 { None, enum1, enum2, enum3, enum4 };", "Foo.cs", "Bar.cs")]
        [InlineData("class Foo{}", "Bar.cs", "Foo`.cs")]
        [InlineData("class Foo{}", "Bar.cs", "Foo@.cs")]
        [InlineData("class Foo{}", "Foo.cs", "Foo.cs")]
        [InlineData("class Foo{}", "Foo.cs", "Folder1\\Foo.cs")]
        public async Task Rename_Symbol_Should_Not_HappenAsync(string sourceCode, string oldFilePath, string newFilePath)
        {
            var userNotificationServices = IUserNotificationServicesFactory.Create();
            var roslynServices = IRoslynServicesFactory.Implement(new CSharpSyntaxFactsService());
            var vsOnlineServices = IVsOnlineServicesFactory.Create(online: false);

            await RenameAsync(sourceCode, oldFilePath, newFilePath, userNotificationServices, roslynServices, vsOnlineServices, LanguageNames.CSharp).TimeoutAfter(TimeSpan.FromSeconds(1));

            Mock.Get(userNotificationServices).Verify(h => h.Confirm(It.IsAny<string>()), Times.Never);
            Mock.Get(roslynServices).Verify(h => h.RenameSymbolAsync(It.IsAny<Solution>(), It.IsAny<ISymbol>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            Mock.Get(roslynServices).Verify(h => h.ApplyChangesToSolution(It.IsAny<Workspace>(), It.IsAny<Solution>()), Times.Never);
        }

        [Fact]
        public async Task Rename_Symbol_Should_ExitEarlyInVSOnlineAsync()
        {
            const string sourceCode = "class Foo { }";
            const string oldFilePath = "Foo.cs";
            const string newFilePath = "Bar.cs";

            var userNotificationServices = IUserNotificationServicesFactory.Create();
            var roslynServices = IRoslynServicesFactory.Implement(new CSharpSyntaxFactsService());
            var vsOnlineService = IVsOnlineServicesFactory.Create(online: true);

            await RenameAsync(sourceCode, oldFilePath, newFilePath, userNotificationServices, roslynServices, vsOnlineService, LanguageNames.CSharp);

            Mock.Get(userNotificationServices).Verify(h => h.Confirm(It.IsAny<string>()), Times.Never);
            Mock.Get(roslynServices).Verify(h => h.RenameSymbolAsync(It.IsAny<Solution>(), It.IsAny<ISymbol>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            Mock.Get(roslynServices).Verify(h => h.ApplyChangesToSolution(It.IsAny<Workspace>(), It.IsAny<Solution>()), Times.Never);
        }
    }
}
