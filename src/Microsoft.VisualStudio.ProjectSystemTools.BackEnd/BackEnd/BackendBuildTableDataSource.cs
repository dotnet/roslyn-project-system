// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.ProjectSystemTools.BackEnd;

namespace Microsoft.VisualStudio.ProjectSystem.Tools.BuildLogging.Model.BackEnd
{
    [Export(typeof(ILoggingController))]
    [Export(typeof(ILoggingDataSource))]
    public sealed class BackEndBuildTableDataSource : ILoggingController, ILoggingDataSource
    {
        private readonly EvaluationLogger _evaluationLogger;
        private readonly RoslynLogger _roslynLogger;

        private ImmutableList<Build> _entries = ImmutableList<Build>.Empty;

        public event EventHandler? BuildsUpdated;

        public bool IsLogging { get; private set; }

        public bool SupportsRoslynLogging => _roslynLogger.Supported;

        public BackEndBuildTableDataSource()
        {
            _evaluationLogger = new EvaluationLogger(this);
            _roslynLogger = new RoslynLogger(this);
        }

        public void Start()
        {
            IsLogging = true;
            ProjectCollection.GlobalProjectCollection.RegisterLogger(_evaluationLogger);
            _roslynLogger.Start();
        }

        public void Stop()
        {
            IsLogging = false;
            ProjectCollection.GlobalProjectCollection.UnregisterAllLoggers();
            _roslynLogger.Stop();
        }

        public void Clear()
        {
            foreach (Build build in _entries)
            {
                build.Dispose();
            }
            _entries = ImmutableList<Build>.Empty;
        }

        public ILogger CreateLogger(bool isDesignTime) => new ProjectLogger(this, isDesignTime);

        /// <summary>
        /// return log path on server for a given build
        /// </summary>
        /// <param name="buildId">ID to return build for</param>
        /// <returns> returns filepath to log path (on server)
        /// returns null if no match is found.</returns>
        public string? GetLogForBuild(int buildId)
        {
            return _entries.Find(x => x?.BuildId == buildId)?.LogPath;
        }

        ImmutableList<BuildSummary> ILoggingDataSource.GetAllBuilds()
        {
            return _entries.Select(build => build.BuildSummary).ToImmutableList();
        }

        public void NotifyChange()
        {
            OnDataChanged();
        }

        private void OnDataChanged()
        {
            BuildsUpdated?.Invoke(this, null);
        }

        public void AddEntry(Build build)
        {
            _entries = _entries.Add(build);
            NotifyChange();
        }
    }
}
