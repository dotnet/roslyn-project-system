// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.VS.HotReload
{
    /// <summary>
    /// Tracks and manages the pending and active Hot Reload sessions for the project.
    /// </summary>
    [ProjectSystemContract(ProjectSystemContractScope.UnconfiguredProject, ProjectSystemContractProvider.Private, Cardinality = Composition.ImportCardinality.ExactlyOne)]
    internal interface IProjectHotReloadSessionManager
    {
        /// <summary>
        /// Creates a pending Hot Reload session for the project as possible, and updates
        /// the given <paramref name="environmentVariables"/> accordingly.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the session was created; <see langword="false"/>
        /// otherwise.
        /// </returns>
        Task<bool> TryCreatePendingSessionAsync(IDictionary<string, string> environmentVariables);
        
        /// <summary>
        /// Activates the pending Hot Reload session and associates it with the specified
        /// process.
        /// </summary>
        Task ActivateSessionAsync(int processId);
    }
}
