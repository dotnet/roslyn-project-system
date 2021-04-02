// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.Tools.BuildLogging.Model.RpcContracts
{
    /// <summary>
    /// The main interface between the client and the server.
    /// These operations are async
    /// </summary>
    public interface IBuildLoggerService
    {
        /// <summary>
        /// Returns whether or not the build logging window is currently tracking logs or not
        /// </summary>
        /// <param name="cancellationToken">default cancellation token</param>
        /// <returns>True if build logging window is tracking logs and false otherwise</returns>
        Task<bool> IsLoggingAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Returns whether or not build logging supports roslyn logging
        /// </summary>
        /// <param name="cancellationToken">default cancellation token</param>
        /// <returns>True if build logging supports roslyn logging, false if otherwise</returns>
        Task<bool> SupportsRoslynLoggingAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Tell build logging to start tracking logs
        /// </summary>
        /// <param name="cancellationToken">default cancellation token</param>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Tell build logging to stop tracking logs
        /// </summary>
        /// <param name="cancellationToken">default cancellation token</param>
        Task StopAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Tell build logging to clear out all the accumulated logs contained on the server.
        /// </summary>
        /// <param name="cancellationToken">default cancellation token</param>
        Task ClearAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gives the user a log of a requested build
        /// </summary>
        /// <param name="buildID">an ID used to retrieve a unique log for a build</param>
        /// <param name="cancellationToken">default cancellation token</param>
        /// <returns>The log tied to the requested BuildHandle</returns>
        Task<string?> GetLogForBuildAsync(int buildID, CancellationToken cancellationToken);

        /// <summary>
        /// Gives the user a requested build
        /// </summary>
        /// <param name="cancellationToken">default cancellation token</param>
        /// <returns>List of summary information of all builds on the server</returns>
        Task<ImmutableList<BuildSummary>> GetAllBuildsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Event that will be invoked whenever build log data has been updated on the server
        /// </summary>
        public event EventHandler DataChanged;
    }
}
