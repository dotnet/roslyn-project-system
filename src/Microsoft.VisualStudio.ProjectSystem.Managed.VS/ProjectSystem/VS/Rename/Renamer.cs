﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.ProjectSystem.Rename;
using Microsoft.VisualStudio.ProjectSystem.Waiting;
using DTE = EnvDTE.DTE;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Rename
{
    [Export(typeof(IFileRenameHandler))]
    [AppliesTo(ProjectCapability.CSharpOrVisualBasic)]
    internal sealed partial class Renamer : IFileRenameHandler
    {
        private readonly IUnconfiguredProjectVsServices _projectVsServices;
        private readonly IUnconfiguredProjectTasksService _unconfiguredProjectTasksService;
        private readonly Workspace _workspace;
        private readonly IUserNotificationServices _userNotificationServices;
        private readonly IEnvironmentOptions _environmentOptions;
        private readonly IRoslynServices _roslynServices;
        private readonly IWaitIndicator _waitService;

        [ImportingConstructor]
        internal Renamer(IUnconfiguredProjectVsServices projectVsServices,
                         IUnconfiguredProjectTasksService unconfiguredProjectTasksService,
                         [Import(typeof(VisualStudioWorkspace))]Workspace workspace,
                         IVsUIService<Shell.Interop.SDTE, DTE> dte,
                         IEnvironmentOptions environmentOptions,
                         IUserNotificationServices userNotificationServices,
                         IRoslynServices roslynServices,
                         IWaitIndicator waitService)
        {
            _projectVsServices = projectVsServices;
            _unconfiguredProjectTasksService = unconfiguredProjectTasksService;
            _workspace = workspace;
            _environmentOptions = environmentOptions;
            _userNotificationServices = userNotificationServices;
            _roslynServices = roslynServices;
            _waitService = waitService;
        }

        public void HandleRename(string oldFilePath, string newFilePath)
        {
            // We do not need to block the completion of HandleRename so we queue it using the threading service
            // and ignore the result.
            // NOTE: we queue work on JTF queue so VS shutdown can happen cleanly
            _projectVsServices.ThreadingService.RunAndForget(
                () => HandleRenameAsync(oldFilePath, newFilePath),
                    unconfiguredProject: _projectVsServices.Project);
        }

        internal async Task HandleRenameAsync(string oldFilePath, string newFilePath)
        {
            // Do not offer to rename types if the user changes the file extensions
            if (!oldFilePath.EndsWith(Path.GetExtension(newFilePath), StringComparisons.Paths))
            {
                return;
            }

            if (GetCurrentProject() is null)
                return;

            // see if the current project contains a compilation
            (bool success, bool isCaseSensitive) = await TryDetermineIfCompilationIsCaseSensitiveAsync(GetCurrentProject());
            if (!success)
                return;

            // Check that the new name is a valid identifier in the current programming language
            string oldName = Path.GetFileNameWithoutExtension(oldFilePath);
            string newName = Path.GetFileNameWithoutExtension(newFilePath);
            if (!CanHandleRename(oldName, newName, isCaseSensitive))
                return;

            // Check if there are any symbols that need to be renamed
            ISymbol? symbol = await TryGetSymbolToRename(oldName, oldFilePath, newFilePath, isCaseSensitive, GetCurrentProject);
            Document? newDocument = GetDocument(GetCurrentProject, oldFilePath, newFilePath);
            if (symbol is null || newDocument is null)
                return;

            // Ask if the user wants to rename the symbol
            bool userWantsToRenameSymbol = await CheckUserConfirmation(oldName);
            if (!userWantsToRenameSymbol)
                return;

            // Try and apply the changes to the current solution
            IDocumentRefactoringService? documentRefacotringService = _workspace.Services.GetService<IDocumentRefactoringService>();
            bool renamed = false;
            if (documentRefacotringService != null)
            {
                WaitIndicatorResult result;
                await _projectVsServices.ThreadingService.SwitchToUIThread();
                string renameOperationName = string.Format(CultureInfo.CurrentCulture, VSResources.Renaming_Type_from_0_to_1, oldName, newName);
                (result, renamed) = _waitService.WaitForAsyncFunctionWithResult(
                    title: VSResources.Renaming_Type,
                    message: renameOperationName,
                    allowCancel: true,
                    token => RenameAsync(documentRefacotringService, newDocument.Id, oldFilePath, token));

                // Do not warn the user if the rename was cancelled by the user
                if (result.WasCanceled())
                {
                    return;
                }
            }

            // Notify the user if the rename could not be performed
            if (!renamed)
            {
                string failureMessage = string.Format(CultureInfo.CurrentCulture, VSResources.RenameSymbolFailed, oldName);
                _userNotificationServices.ShowWarning(failureMessage);
            }
        }

        private Task<bool> RenameAsync(IDocumentRefactoringService documentRefactoringService, DocumentId documentId, string oldFilePath, CancellationToken token)
        {
            return _unconfiguredProjectTasksService.LoadedProjectAsync(
                () => documentRefactoringService.TryUpdateNamedTypeToMatchFilePath(documentId, oldFilePath, token));
        }

        private Project? GetCurrentProject()
        {
            foreach (Project proj in _workspace.CurrentSolution.Projects)
            {
                if (StringComparers.Paths.Equals(proj.FilePath, _projectVsServices.Project.FullPath!))
                {
                    return proj;
                }
            }

            return null;
        }

        private static async Task<(bool success, bool isCaseSensitive)> TryDetermineIfCompilationIsCaseSensitiveAsync(Project? project)
        {
            if (project is null)
                return (false, false);

            Compilation? compilation = await project.GetCompilationAsync();
            if (compilation is null)
            {
                // this project does not support compilations
                return (false, false);
            }

            return (true, compilation.IsCaseSensitive);
        }

        private bool CanHandleRename(string oldName, string newName, bool isCaseSensitive)
            => _roslynServices.IsValidIdentifier(oldName) &&
               _roslynServices.IsValidIdentifier(newName) &&
              (!string.Equals(
                  oldName,
                  newName,
                  isCaseSensitive
                    ? StringComparisons.LanguageIdentifiers
                    : StringComparisons.LanguageIdentifiersIgnoreCase));

        private static async Task<ISymbol?> TryGetSymbolToRename(string oldName,
                                                                 string oldFilePath,
                                                                 string newFileName,
                                                                 bool isCaseSensitive,
                                                                 Func<Project?> getProject,
                                                                 CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            Document? newDocument = GetDocument(getProject, oldFilePath, newFileName);
            if (newDocument == null)
                return null;

            SyntaxNode? root = await GetRootNode(newDocument, token);
            if (root == null)
                return null;

            SemanticModel? semanticModel = await newDocument.GetSemanticModelAsync(token);
            if (semanticModel == null)
                return null;

            IEnumerable<SyntaxNode> declarations = root.DescendantNodes().Where(n => HasMatchingSyntaxNode(semanticModel, n, oldName, isCaseSensitive, token));
            SyntaxNode declaration = declarations.FirstOrDefault();
            if (declaration == null)
                return null;

            return semanticModel.GetDeclaredSymbol(declaration);
        }

        private static Document? GetDocument(Func<Project?> getProject, string oldFilePath, string newFilePath)
        {
            Project? project = getProject();
            if (project is null)
                return null;

            Document newDocument = GetDocument(project, newFilePath);
            if (newDocument != null)
                return newDocument;

            return GetDocument(project, oldFilePath);
        }

        private static Document GetDocument(Project project, string filePath) =>
            (from d in project.Documents where StringComparers.Paths.Equals(d.FilePath, filePath) select d).FirstOrDefault();

        private static Task<SyntaxNode?> GetRootNode(Document newDocument, CancellationToken token) =>
            newDocument.GetSyntaxRootAsync(token);

        private static bool HasMatchingSyntaxNode(SemanticModel model, SyntaxNode syntaxNode, string name, bool isCaseSensitive, CancellationToken token)
        {
            if (model.GetDeclaredSymbol(syntaxNode, token) is INamedTypeSymbol symbol &&
                (symbol.TypeKind == TypeKind.Class ||
                 symbol.TypeKind == TypeKind.Interface ||
                 symbol.TypeKind == TypeKind.Delegate ||
                 symbol.TypeKind == TypeKind.Enum ||
                 symbol.TypeKind == TypeKind.Struct ||
                 symbol.TypeKind == TypeKind.Module))
            {
                return string.Compare(symbol.Name, name, !isCaseSensitive) == 0;
            }

            return false;
        }

        private async Task<bool> CheckUserConfirmation(string oldFileName)
        {
            await _projectVsServices.ThreadingService.SwitchToUIThread();
            bool userNeedPrompt = _environmentOptions.GetOption("Environment", "ProjectsAndSolution", "PromptForRenameSymbol", false);
            if (userNeedPrompt)
            {
                string renamePromptMessage = string.Format(CultureInfo.CurrentCulture, VSResources.RenameSymbolPrompt, oldFileName);

                await _projectVsServices.ThreadingService.SwitchToUIThread();
                return _userNotificationServices.Confirm(renamePromptMessage);
            }

            return true;
        }
    }
}
