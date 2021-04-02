// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.VisualStudio.ProjectSystem.Tools.BuildLogging.Model.BackEnd
{
    internal abstract class LoggerBase : ILogger
    {
        protected readonly BackEndBuildTableDataSource DataSource;

        public LoggerVerbosity Verbosity { get => LoggerVerbosity.Diagnostic; set { } }

        public string? Parameters { get; set; }

        protected LoggerBase(BackEndBuildTableDataSource dataSource)
        {
            DataSource = dataSource;
        }

        protected static string GetLogPath(Build build)
        {
            string dimensionsString =
                build.Dimensions.Any() ? $"{build.Dimensions.Aggregate((c, n) => string.IsNullOrEmpty(n) ? c : $"{c}_{n}")}_" : string.Empty;

            string filename = $"{Path.GetFileNameWithoutExtension(build.ProjectPath)}_{dimensionsString}{build.BuildType}_{build.StartTime:o}.binlog".Replace(':', '_');

            return Path.Combine(Path.GetTempPath(), filename);
        }

        protected static void Copy(string from, string to)
        {
            File.Copy(from, to, overwrite: true);
        }

        public abstract void Initialize(IEventSource eventSource);

        public virtual void Shutdown()
        {
        }
    }
}
