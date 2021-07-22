// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.LanguageServices.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Build;

namespace Microsoft.VisualStudio.ProjectSystem.LanguageServices
{
    /// <summary>
    ///     Applies <see cref="IProjectVersionedValue{T}"/> values to a <see cref="IWorkspaceProjectContext"/>.
    /// </summary>
    [ProjectSystemContract(ProjectSystemContractScope.ConfiguredProject, ProjectSystemContractProvider.Private, Cardinality = ImportCardinality.ExactlyOne)]
    internal interface IApplyChangesToWorkspaceContext
    {
        /// <summary>
        ///     Returns an enumerable of project evaluation rules that should passed to
        ///     <see cref="ApplyProjectEvaluationAsync(IProjectVersionedValue{IProjectSubscriptionUpdate}, ContextState, CancellationToken)"/>.
        /// </summary>
        IEnumerable<string> GetProjectEvaluationRules();

        /// <summary>
        ///     Returns an enumerable of project build rules that should passed to
        ///     <see cref="ApplyProjectBuildAsync(IProjectVersionedValue{IProjectSubscriptionUpdate}, IProjectBuildSnapshot, ContextState, CancellationToken)"/>.
        /// </summary>
        IEnumerable<string> GetProjectBuildRules();

        /// <summary>
        ///     Initializes the service with the specified <see cref="IWorkspaceProjectContext"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     <see cref="Initialize(IWorkspaceProjectContext)"/> has already been called.
        /// </exception>
        void Initialize(IWorkspaceProjectContext context);

        /// <summary>
        ///     Applies project evaluation changes to the underlying <see cref="IWorkspaceProjectContext"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="update"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     <see cref="Initialize(IWorkspaceProjectContext)"/> has not been called.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///     The <see cref="IApplyChangesToWorkspaceContext"/> has been disposed of.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        ///     The result is awaited and <paramref name="cancellationToken"/> is cancelled.
        /// </exception>
        /// <remarks>
        ///     Note: Cancelling the <paramref name="cancellationToken"/> may result in the underlying
        ///     <see cref="IWorkspaceProjectContext"/> to be left in an inconsistent state with respect
        ///     to the project snapshot state. The cancellation token should only be cancelled with the
        ///     intention that the <see cref="IWorkspaceProjectContext"/> will be immediately disposed.
        /// </remarks>
        Task ApplyProjectEvaluationAsync(IProjectVersionedValue<IProjectSubscriptionUpdate> update, ContextState state, CancellationToken cancellationToken);

        /// <summary>
        ///     Applies project build changes to the underlying <see cref="IWorkspaceProjectContext"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="update"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     <see cref="Initialize(IWorkspaceProjectContext)"/> has not been called.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///     The <see cref="IApplyChangesToWorkspaceContext"/> has been disposed of.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        ///     The result is awaited and <paramref name="cancellationToken"/> is cancelled.
        /// </exception>
        /// <remarks>
        ///     Note: Cancelling the <paramref name="cancellationToken"/> may result in the underlying
        ///     <see cref="IWorkspaceProjectContext"/> to be left in an inconsistent state with respect
        ///     to the project snapshot state. The cancellation token should only be cancelled with the
        ///     intention that the <see cref="IWorkspaceProjectContext"/> will be immediately disposed.
        /// </remarks>
        Task ApplyProjectBuildAsync(IProjectVersionedValue<IProjectSubscriptionUpdate> update, IProjectBuildSnapshot buildSnapshot, ContextState state, CancellationToken cancellationToken);

        /// <summary>
        ///     Applies source items changes to the underlying <see cref="IWorkspaceProjectContext"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="update"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     <see cref="Initialize(IWorkspaceProjectContext)"/> has not been called.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///     The <see cref="IApplyChangesToWorkspaceContext"/> has been disposed of.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        ///     The result is awaited and <paramref name="cancellationToken"/> is cancelled.
        /// </exception>
        /// <remarks>
        ///     Note: Cancelling the <paramref name="cancellationToken"/> may result in the underlying
        ///     <see cref="IWorkspaceProjectContext"/> to be left in an inconsistent state with respect
        ///     to the project snapshot state. The cancellation token should only be cancelled with the
        ///     intention that the <see cref="IWorkspaceProjectContext"/> will be immediately disposed.
        /// </remarks>
        Task ApplySourceItemsAsync(IProjectVersionedValue<IProjectSubscriptionUpdate> update, ContextState state, CancellationToken cancellationToken);
    }
}
