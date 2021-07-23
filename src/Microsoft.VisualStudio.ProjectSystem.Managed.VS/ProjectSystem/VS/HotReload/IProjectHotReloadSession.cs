// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.VS.HotReload
{
    /// <summary>
    /// Represents a Hot Reload session within the project system.
    /// </summary>
    public interface IProjectHotReloadSession
    {
        /// <summary>
        /// A name used to identify the session in diagnostic messages. Not guaranteed to be
        /// unique.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Starts the Hot Reload Session.
        /// </summary>
        Task StartSessionAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stops the Hot Reload Session.
        /// </summary>
        Task StopSessionAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Applies any pending changes to the Hot Reload session.
        /// </summary>
        Task ApplyChangesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Updates the environment variables in <paramref name="envVars"/> to include
        /// settings needed by a process using Hot Reload.
        /// </summary>
        Task<bool> ApplyLaunchVariablesAsync(IDictionary<string, string> envVars, CancellationToken cancellationToken);
    }
}
