// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;

namespace Microsoft.VisualStudio.ProjectSystem.VS.PackageRestore
{
    /// <summary>
    ///     Represents restore input data for a single <see cref="ConfiguredProject"/>.
    /// </summary>
    internal class PackageRestoreConfiguredInput
    {
        public PackageRestoreConfiguredInput(ProjectConfiguration projectConfiguration, ProjectRestoreInfo restoreInfo, IComparable configuredProjectVersion)
        {
            ProjectConfiguration = projectConfiguration;
            RestoreInfo = restoreInfo;
            ConfiguredProjectVersion = configuredProjectVersion;
        }

        /// <summary>
        ///     Gets the configuration of the <see cref="ConfiguredProject"/> this input was produced from.
        /// </summary>
        public ProjectConfiguration ProjectConfiguration { get; }

        /// <summary>
        ///     Gets the restore information produced in this input.
        /// </summary>
        public ProjectRestoreInfo RestoreInfo { get; }

        /// <summary>
        ///     Get the project version produced in this input
        /// </summary>
        public IComparable ConfiguredProjectVersion { get; }
    }
}
