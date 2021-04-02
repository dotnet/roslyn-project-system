// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Immutable;

namespace Microsoft.VisualStudio.ProjectSystem.Tools.BuildLogging.Model.BackEnd
{
    public interface ILoggingDataSource
    {
        /// <summary>
        /// Does this logging service support roslyn logging?
        /// true if yes, false if no
        /// </summary>
        bool SupportsRoslynLogging { get; }

        /// <summary>
        /// Tells logging service to start collecting builds and their log files,
        /// and will use the given Action callback
        /// when data on the logging service is updated
        /// </summary>
        void Start();

        /// <summary>
        /// Tells logging service to stop collecting builds and log files
        /// Will also forget the given Action callback given from Start()
        /// </summary>
        void Stop();

        /// <summary>
        /// Clears out all currently stored build files
        /// </summary>
        void Clear();

        /// <summary>
        /// Finds and returns the filepath to a given build's log file
        /// </summary>
        /// <param name="buildId">a unique int Id for a currently stored build</param>
        /// <returns>returns a log's filepath for a build with the given Id.
        /// returns null if no build matched the given Id</returns>
        string? GetLogForBuild(int buildId);

        /// <summary>
        /// Returns all currently collected builds.
        /// </summary>
        /// <returns>An ImmutableList of builds,
        /// the list and the data inside it is immutable</returns>
        ImmutableList<BuildSummary> GetAllBuilds();

        /// <summary>
        /// Event raised whenever new data from loggers is received
        /// </summary>
        event EventHandler BuildsUpdated;
    }
}
