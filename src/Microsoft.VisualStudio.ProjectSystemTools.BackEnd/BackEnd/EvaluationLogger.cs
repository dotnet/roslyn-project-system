// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.VisualStudio.ProjectSystem.Tools.BuildLogging.Model.BackEnd
{
    internal sealed class EvaluationLogger : LoggerBase
    {
        private sealed class Evaluation
        {
            public EventWrapper? Wrapper { get; set; }
            public Build? Build { get; set; }
            public string? LogPath { get; set; }
        }

        private sealed class EventWrapper : IEventSource
        {
            public BinaryLogger BinaryLogger { get; }

            public event BuildMessageEventHandler MessageRaised { add { } remove { } }
            public event BuildErrorEventHandler ErrorRaised { add { } remove { } }
            public event BuildWarningEventHandler WarningRaised { add { } remove { } }
            public event BuildStartedEventHandler BuildStarted { add { } remove { } }
            public event BuildFinishedEventHandler BuildFinished { add { } remove { } }
            public event ProjectStartedEventHandler ProjectStarted { add { } remove { } }
            public event ProjectFinishedEventHandler ProjectFinished { add { } remove { } }
            public event TargetStartedEventHandler TargetStarted { add { } remove { } }
            public event TargetFinishedEventHandler TargetFinished { add { } remove { } }
            public event TaskStartedEventHandler TaskStarted { add { } remove { } }
            public event TaskFinishedEventHandler TaskFinished { add { } remove { } }
            public event CustomBuildEventHandler CustomEventRaised { add { } remove { } }
            public event BuildStatusEventHandler StatusEventRaised { add { } remove { } }
            public event AnyEventHandler? AnyEventRaised;

            public EventWrapper(BinaryLogger binaryLogger)
            {
                BinaryLogger = binaryLogger;
                BinaryLogger.Initialize(this);
            }

            public void RaiseEvent(object sender, BuildEventArgs args) => AnyEventRaised?.Invoke(sender, args);
        }

        private readonly Dictionary<int, Evaluation> _evaluations = new Dictionary<int, Evaluation>();

        public EvaluationLogger(BackEndBuildTableDataSource dataSource)
            : base(dataSource)
        {
        }

        public override void Initialize(IEventSource eventSource)
        {
            eventSource.AnyEventRaised += AnyEvent;
        }

        private void AnyEvent(object sender, BuildEventArgs args)
        {
            if (args.BuildEventContext == null || args.BuildEventContext.EvaluationId == BuildEventContext.InvalidEvaluationId)
            {
                return;
            }

            switch (args)
            {
                case ProjectEvaluationStartedEventArgs evaluationStarted:
                {
                    if (!DataSource.IsLogging || evaluationStarted.ProjectFile == "(null)")
                    {
                        return;
                    }

                    string? logPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.binlog");
                    var binaryLogger = new BinaryLogger
                    {
                        Parameters = logPath,
                        Verbosity = LoggerVerbosity.Diagnostic,
                        CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.None
                    };
                    var wrapper = new EventWrapper(binaryLogger);
                    var build = new Build(evaluationStarted.ProjectFile, Array.Empty<string>(), Array.Empty<string>(),
                        BuildType.Evaluation, args.Timestamp);
                    _evaluations[evaluationStarted.BuildEventContext.EvaluationId] = new Evaluation
                    {
                        Wrapper = wrapper,
                        Build = build,
                        LogPath = logPath
                    };
                    wrapper.RaiseEvent(sender, args);
                    DataSource.AddEntry(build);
                }
                break;

                case ProjectEvaluationFinishedEventArgs:
                {
                    if (_evaluations.TryGetValue(args.BuildEventContext.EvaluationId, out Evaluation? evaluation))
                    {
                        evaluation.Build?.Finish(true, args.Timestamp);
                        evaluation.Wrapper?.RaiseEvent(sender, args);
                        evaluation.Wrapper?.BinaryLogger.Shutdown();
                        evaluation.Build?.SetLogPath(GetLogPath(evaluation.Build));
                        Copy(evaluation.LogPath, evaluation?.Build?.LogPath);
                        DataSource.NotifyChange();
                    }
                }
                break;

                default:
                {
                    if (_evaluations.TryGetValue(args.BuildEventContext.EvaluationId, out Evaluation? evaluation))
                    {
                        evaluation.Wrapper?.RaiseEvent(sender, args);
                    }
                }
                break;
            }
        }
    }
}
