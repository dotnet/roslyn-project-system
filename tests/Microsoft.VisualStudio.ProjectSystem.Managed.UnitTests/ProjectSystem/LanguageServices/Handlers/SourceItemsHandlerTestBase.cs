// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Immutable;
using Microsoft.VisualStudio.LanguageServices.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.VS;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.LanguageServices.Handlers
{
    public abstract class SourceItemsHandlerTestBase
    {
        [Fact]
        public void Handle_NullAsVersion_ThrowsArgumentNull()
        {
            var handler = CreateInstance();
            var projectChanges = ImmutableDictionary<string, IProjectChangeDescription>.Empty;
            var logger = Mock.Of<IProjectDiagnosticOutputService>();

            Assert.Throws<ArgumentNullException>("version", () =>
            {
                handler.Handle(null!, projectChanges, new ContextState(), logger);
            });
        }

        [Fact]
        public void Handle_NullAsProjectChanges_ThrowsArgumentNull()
        {
            var handler = CreateInstance();
            var logger = Mock.Of<IProjectDiagnosticOutputService>();

            Assert.Throws<ArgumentNullException>("projectChanges", () =>
            {
                handler.Handle(10, null!, new ContextState(), logger);
            });
        }

        [Fact]
        public void Handle_NullAsLogger_ThrowsArgumentNull()
        {
            var handler = CreateInstance();
            var projectChanges = ImmutableDictionary<string, IProjectChangeDescription>.Empty;

            Assert.Throws<ArgumentNullException>("logger", () =>
            {
                handler.Handle(10, projectChanges, new ContextState(), null!);
            });
        }

        [Fact]
        public void Handle_WhenNotInitialized_ThrowsInvalidOperation()
        {
            var handler = CreateInstance();
            var projectChanges = ImmutableDictionary<string, IProjectChangeDescription>.Empty;
            var logger = Mock.Of<IProjectDiagnosticOutputService>();

            Assert.Throws<InvalidOperationException>(() =>
            {
                handler.Handle(10, projectChanges, new ContextState(), logger);
            });
        }

        [Fact]
        public void Initialize_WhenAlreadyInitialized_ThrowsInvalidOperation()
        {
            var handler = CreateInstance();

            var workspaceContext = IWorkspaceProjectContextMockFactory.Create();

            handler.Initialize(workspaceContext);

            Assert.Throws<InvalidOperationException>(() =>
            {
                handler.Initialize(workspaceContext);
            });
        }

        internal static void Handle(ISourceItemsHandler handler, IImmutableDictionary<string, IProjectChangeDescription> projectChanges)
        {
            handler.Handle(1, projectChanges, new ContextState(), IProjectDiagnosticOutputServiceFactory.Create());
        }

        internal abstract ISourceItemsHandler CreateInstance();
    }
}
