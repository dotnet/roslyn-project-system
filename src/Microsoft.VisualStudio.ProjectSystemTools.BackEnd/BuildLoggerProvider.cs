// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Build;
using Microsoft.VisualStudio.Shell.BuildLogging;

namespace Microsoft.VisualStudio.ProjectSystemTools.BackEnd
{
    [Export(typeof(IBuildLoggerProviderAsync))]
    [AppliesTo(ProjectCapabilities.AlwaysApplicable)]
    [Export(typeof(IVsBuildLoggerProvider))]
    internal sealed class BuildLoggerProvider : IBuildLoggerProviderAsync, IVsBuildLoggerProvider
    {
        private readonly ILoggingController _loggingController;

        [ImportingConstructor]
        public BuildLoggerProvider(ILoggingController loggingController)
        {
            _loggingController = loggingController;
        }

        public LoggerVerbosity Verbosity => LoggerVerbosity.Diagnostic;

        public BuildLoggerEvents Events =>
            BuildLoggerEvents.BuildStartedEvent |
            BuildLoggerEvents.BuildFinishedEvent |
            BuildLoggerEvents.ErrorEvent |
            BuildLoggerEvents.WarningEvent |
            BuildLoggerEvents.HighMessageEvent |
            BuildLoggerEvents.NormalMessageEvent |
            BuildLoggerEvents.ProjectStartedEvent |
            BuildLoggerEvents.ProjectFinishedEvent |
            BuildLoggerEvents.TargetStartedEvent |
            BuildLoggerEvents.TargetFinishedEvent |
            BuildLoggerEvents.CommandLine |
            BuildLoggerEvents.TaskStartedEvent |
            BuildLoggerEvents.TaskFinishedEvent |
            BuildLoggerEvents.LowMessageEvent |
            BuildLoggerEvents.ProjectEvaluationStartedEvent |
            BuildLoggerEvents.ProjectEvaluationFinishedEvent |
            BuildLoggerEvents.CustomEvent;

        public ILogger? GetLogger(string projectPath, IEnumerable<string> targets, IDictionary<string, string> properties, bool isDesignTimeBuild) => 
            _loggingController.IsLogging ? _loggingController.CreateLogger(isDesignTimeBuild) : null;

        public Task<IImmutableSet<ILogger>> GetLoggersAsync(IReadOnlyList<string> targets, IImmutableDictionary<string, string> properties, CancellationToken cancellationToken)
        {
            var loggers = (IImmutableSet<ILogger>)ImmutableHashSet<ILogger>.Empty;

            if (_loggingController.IsLogging)
            {
                bool isDesignTime = properties.TryGetValue("DesignTimeBuild", out string? value) &&
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

                loggers = loggers.Add(_loggingController.CreateLogger(isDesignTime));
            }

            return Task.FromResult(loggers);
        }
    }
}
