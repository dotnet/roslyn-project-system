// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using Microsoft.VisualStudio.ProjectSystem.Debug;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    /// <summary>
    /// Represents the settings stored in the FlavorProperties section of the project file
    /// </summary>
    internal static class DebugLaunchOptionsExtensions
    {
        /// <summary>
        /// Convenient helper to detect if the launch options are defined for debugging
        /// </summary>
        internal static bool IsDebugging(this DebugLaunchOptions launchOptions)
        {
            return !launchOptions.HasFlag(DebugLaunchOptions.NoDebug);
        }

        /// <summary>
        /// Convenient helper to detect if the launch options are defined for profiling
        /// </summary>
        internal static bool IsProfiling(this DebugLaunchOptions launchOptions)
        {
            return launchOptions.HasFlag(DebugLaunchOptions.Profiling);
        }

        internal static WebServerStartOption ToWebServerStartOption(this DebugLaunchOptions launchOptions)
        {
            if (launchOptions.IsDebugging())
            {
                return WebServerStartOption.Debug;
            }
            else if (launchOptions.IsProfiling())
            {
                return WebServerStartOption.Profile;
            }

            return WebServerStartOption.Any;
        }
    }
}
