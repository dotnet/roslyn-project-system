// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.ProjectSystem
{
    /// <summary>
    /// Static class containing helper utilities for checking the feature flag.
    /// </summary>
    internal static class FeatureFlagHelper
    {
        /// <summary>
        /// LoadProjectFromCache feature flag check.
        /// </summary>
        internal const string FeatureFlagCacheModeEnabled = "CSProj.LoadProjectFromCache";

        /// <summary>
        /// Backing field for the <see cref="FeatureFlags"/> property.
        /// </summary>
        private static readonly IVsFeatureFlags? s_featureFlags = Package.GetGlobalService(typeof(SVsFeatureFlags)) as IVsFeatureFlags;

        /// <summary>
        /// Checks the value of a feature flag, guarding against a missing feature flags service.
        /// </summary>
        /// <param name="featureName">The name of the feature.</param>
        /// <param name="defaultValue">The value to return if the feature flag service is missing or the flag has no value.</param>
        /// <returns>The feature flag value if found; otherwise, <paramref name="defaultValue"/>.</returns>
        public static bool IsFeatureEnabled(string featureName, bool defaultValue)
        {
            return s_featureFlags?.IsFeatureEnabled(featureName, defaultValue) ?? false;
        }
    }
}
