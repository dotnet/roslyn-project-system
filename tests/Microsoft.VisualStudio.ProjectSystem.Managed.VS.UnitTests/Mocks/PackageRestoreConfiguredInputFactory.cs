// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.ProjectSystem.VS.PackageRestore
{
    internal static class PackageRestoreConfiguredInputFactory
    {
        public static IReadOnlyCollection<PackageRestoreConfiguredInput>? Create(ProjectRestoreInfo restoreInfo)
        {
            ProjectConfiguration projectConfiguration = ProjectConfigurationFactory.Create("Debug|x64");
            IComparable projectVersion = (IComparable)0;

            return new PackageRestoreConfiguredInput[1]{new PackageRestoreConfiguredInput(projectConfiguration, restoreInfo, projectVersion)};
        }
    }
}
