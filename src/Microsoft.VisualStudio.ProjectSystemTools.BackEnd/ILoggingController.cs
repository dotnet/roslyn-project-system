// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using Microsoft.Build.Framework;

namespace Microsoft.VisualStudio.ProjectSystemTools.BackEnd
{
    public interface ILoggingController
    {
        /// <summary>
        /// Is this logging controller currently collecting logs?
        /// true if yes, false if no.
        /// </summary>
        bool IsLogging { get; }

        /// <summary>
        /// Creates a new logger to collect builds and their logs
        /// </summary>
        /// <param name="isDesignTime">Is this for DesignTime build logging?</param>
        /// <returns>Return newly created logger</returns>
        ILogger CreateLogger(bool isDesignTime);
    }
}
