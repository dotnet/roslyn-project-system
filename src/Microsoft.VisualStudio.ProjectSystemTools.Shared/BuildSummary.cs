// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Newtonsoft.Json;

namespace Microsoft.VisualStudio.ProjectSystem.Tools.BuildLogging.Model
{
    /// <summary>
    /// Immutable Type
    /// 
    /// Contains data about builds that are necessary on the client and server side.
    /// Each BuildSummary also has a unique int Id to be referred by.
    /// This type can be sent to the client side.
    /// </summary>
    public sealed class BuildSummary
    {
        public int BuildId { get; }

        public BuildType BuildType { get; }

        public ImmutableArray<string?> Dimensions { get; }

        public ImmutableArray<string?> Targets { get; }

        public DateTime? StartTime { get; }

        public TimeSpan? Elapsed { get; }

        public BuildStatus Status { get; }

        public string ProjectName { get; }

        public BuildSummary(int buildId, string projectPath, IEnumerable<string?> dimensions, IEnumerable<string?>? targets, BuildType buildType, DateTime? startTime)
        {
            BuildId = buildId;
            ProjectName = Path.GetFileName(projectPath);
            Dimensions = dimensions.ToImmutableArray();
            Targets = targets?.ToImmutableArray() ?? ImmutableArray<string?>.Empty;
            BuildType = buildType;
            StartTime = startTime;
            Status = BuildStatus.Running;
        }
        public BuildSummary(BuildSummary other, BuildStatus status, TimeSpan? elapsed) {
            BuildId = other.BuildId;
            BuildType = other.BuildType;
            Dimensions = other.Dimensions;
            Targets = other.Targets;
            StartTime = other.StartTime;
            ProjectName = other.ProjectName;
            Elapsed = elapsed;
            Status = status;
        }
        [JsonConstructor]
        public BuildSummary(int buildId, BuildType buildType,
            IEnumerable<string?> dimensions, IEnumerable<string?> targets,
            DateTime startTime, TimeSpan elapsed, BuildStatus status, string projectName)
        {
            BuildId = buildId;
            BuildType = buildType;
            Dimensions = dimensions.ToImmutableArray();
            Targets = targets?.ToImmutableArray() ?? ImmutableArray<string?>.Empty;
            StartTime = startTime;
            Elapsed = elapsed;
            Status = status;
            ProjectName = projectName;
        }
    }
}
