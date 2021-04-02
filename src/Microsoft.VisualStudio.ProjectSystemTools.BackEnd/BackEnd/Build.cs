// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;

namespace Microsoft.VisualStudio.ProjectSystem.Tools.BuildLogging.Model.BackEnd
{
    /// <summary>
    /// Represents a build collected by the loggers
    /// Deals with data needed by the client as well as data related to log files
    /// and the file system on the server.
    /// 
    /// Build should only be used on the server side,
    /// where BuildSummary type (a subset of this type)
    /// can be sent to the client side
    /// </summary>
    public sealed class Build : IDisposable
    {
        public BuildSummary BuildSummary { get; private set; }
        public string? ProjectPath { get; }
        public string? LogPath { get; private set; }
        public int BuildId => BuildSummary.BuildId;
        public BuildType BuildType => BuildSummary.BuildType;
        public ImmutableArray<string?> Dimensions => BuildSummary.Dimensions;
        public ImmutableArray<string?> Targets => BuildSummary.Targets;
        public DateTime? StartTime => BuildSummary?.StartTime;
        public TimeSpan? Elapsed => BuildSummary?.Elapsed;
        public BuildStatus Status => BuildSummary.Status;
        public string ProjectName => BuildSummary.ProjectName;
        private static int s_sharedBuildId;
        public Build(string projectPath, IEnumerable<string?> dimensions, IEnumerable<string?>? targets, BuildType buildType, DateTime? startTime)
        {
            int nextId = Interlocked.Increment(ref s_sharedBuildId);
            BuildSummary = new BuildSummary(nextId, projectPath, dimensions, targets, buildType, startTime);
        }

        public void Finish(bool succeeded, DateTime time)
        {
            if (Status != BuildStatus.Running)
            {
                throw new InvalidOperationException();
            }

            BuildStatus newStatus = succeeded ? BuildStatus.Finished : BuildStatus.Failed;
            TimeSpan? elapsedTime = time - StartTime;
            BuildSummary = new BuildSummary(BuildSummary, newStatus, elapsedTime);
        }

        public void SetLogPath(string logPath)
        {
            LogPath = logPath;
        }

        public void Dispose()
        {
            if (LogPath == null)
            {
                return;
            }

            string? logPath = LogPath;
            LogPath = null;
            try
            {
                Assumes.NotNull(logPath);
                File.Delete(logPath);
            }
            catch
            {
                // If it fails, it fails...
            }
        }
    }
}
