// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem;

namespace Microsoft.VisualStudio.Telemetry
{
    [Export(typeof(IUnconfiguredProjectPackageRestoreTelemetryService))]
    internal class UnconfiguredProjectPackageRestoreTelemetryService : IUnconfiguredProjectPackageRestoreTelemetryService
    {
        private readonly UnconfiguredProject _project;
        private readonly ITelemetryService _telemetryService;
        private string? _projectTelemetryId;

        [ImportingConstructor]
        public UnconfiguredProjectPackageRestoreTelemetryService(UnconfiguredProject project, ITelemetryService telemetryService)
        {
            _project = project;
            _telemetryService = telemetryService;
        }

        private string ProjectTelemetryId => _projectTelemetryId ??= _telemetryService.GetProjectId(_project);

        public void PostPackageRestoreEvent(string packageRestoreOperationName)
        {
            _telemetryService.PostProperties(TelemetryEventName.ProcessPackageRestore, new (string propertyName, object propertyValue)[]
                {
                    (TelemetryPropertyName.PackageRestoreOperation, packageRestoreOperationName),
                    (TelemetryPropertyName.PackageRestoreProjectId, ProjectTelemetryId),
                });
        }

        public void PostNominationCompleteEvent(bool nominationSucceeded)
        {
            _telemetryService.PostProperties(TelemetryEventName.ProcessPackageRestore, new (string propertyName, object propertyValue)[]
                {
                    (TelemetryPropertyName.PackageRestoreOperation, PackageRestoreOperationNames.EndNominateRestore),
                    (TelemetryPropertyName.PackageRestoreNominationSucceeded, nominationSucceeded),
                    (TelemetryPropertyName.PackageRestoreProjectId, ProjectTelemetryId),
                });
        }
    }
}
