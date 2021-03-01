// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.ProjectSystem;

namespace Microsoft.VisualStudio.Telemetry
{
    /// <summary>
    /// The managed project system is responsible for requesting NuGet restore for projects.
    /// This is done by invoking the NuGet project nomination APIs and is necessary to ensure
    /// that all the information necessary for features like intellisense is generated.
    /// 
    /// This interface enables the package restore components to post telemetry events indicating
    /// whether the initialization of the package restore components (for the UnconfiguredProject)
    /// suceeded and whether the project was nominated for a package restore.
    /// Project nomination is indicated by these events:
    /// 
    ///  1. PackageRestoreOperationNames.BeginNominateRestore
    ///  2. PackageRestoreOperationNames.EndNominateRestore
    /// 
    /// Ultimately, the most important step in the package restore process is notifying the
    /// operation progress system that package restore is complete. That process happens for
    /// configured projects and is therefore handled by the <see cref="IConfiguredProjectPackageRestoreTelemetryService"/>.
    /// </summary>
    [ProjectSystemContract(ProjectSystemContractScope.UnconfiguredProject, ProjectSystemContractProvider.Private, Cardinality = ImportCardinality.ExactlyOne)]
    internal interface IUnconfiguredProjectPackageRestoreTelemetryService
    {
        /// <summary>
        /// Posts a telemetry event from a package restore component.
        /// </summary>
        /// <param name="packageRestoreOperationName">The name of the specific package restore operation.</param>
        void PostPackageRestoreEvent(string packageRestoreOperationName);

        /// <summary>
        /// Posts a <see cref="TelemetryEventName.ProcessPackageRestore"/> event with a
        /// <see cref="TelemetryPropertyName.PackageRestoreNominationSucceeded"/> property whose value will be set to
        /// indicate whether or not nomination of a project for restore succeeded.
        /// </summary>
        /// <param name="nominationSucceeded">Flag indicating whether the project nomination completed successfully.</param>
        void PostNominationCompleteEvent(bool nominationSucceeded);
    }
}
