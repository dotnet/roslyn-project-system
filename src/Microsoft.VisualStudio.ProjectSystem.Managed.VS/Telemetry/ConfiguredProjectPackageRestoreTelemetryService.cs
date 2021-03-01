// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem;

namespace Microsoft.VisualStudio.Telemetry
{
    [Export(typeof(IConfiguredProjectPackageRestoreTelemetryService))]
    internal class ConfiguredProjectPackageRestoreTelemetryService : IConfiguredProjectPackageRestoreTelemetryService
    {
        private readonly ConfiguredProject _project;
        private readonly ITelemetryService _telemetryService;
        private string? _projectTelemetryId;

        [ImportingConstructor]
        public ConfiguredProjectPackageRestoreTelemetryService(ConfiguredProject project, ITelemetryService telemetryService)
        {
            _project = project;
            _telemetryService = telemetryService;
        }

        private string ProjectTelemetryId => _projectTelemetryId ??= _telemetryService.GetProjectId(_project.UnconfiguredProject);

        public void PostPackageRestoreEvent(string packageRestoreOperationName)
        {
            _telemetryService.PostProperties(TelemetryEventName.ProcessPackageRestore, new (string propertyName, object propertyValue)[]
                {
                    (TelemetryPropertyName.PackageRestoreOperation, packageRestoreOperationName),
                    (TelemetryPropertyName.PackageRestoreProjectId, ProjectTelemetryId),
                });
        }

        public void PostPackageRestoreEvent(string packageRestoreOperationName, long packageRestoreProgressTrackerId)
        {
            _telemetryService.PostProperties(TelemetryEventName.ProcessPackageRestore, new (string propertyName, object propertyValue)[]
                {
                    (TelemetryPropertyName.PackageRestoreOperation, packageRestoreOperationName),
                    (TelemetryPropertyName.PackageRestoreProjectId, ProjectTelemetryId),
                    (TelemetryPropertyName.PackageRestoreProgressTrackerId, packageRestoreProgressTrackerId),
                });
        }

        public void PostPackageRestoreCompletedEvent(bool restoreSucceeded, long packageRestoreProgressTrackerId)
        {
            _telemetryService.PostProperties(TelemetryEventName.ProcessPackageRestore, new (string propertyName, object propertyValue)[]
                {
                    (TelemetryPropertyName.PackageRestoreSucceeded, restoreSucceeded),
                    (TelemetryPropertyName.PackageRestoreOperation, PackageRestoreOperationNames.PackageRestoreProgressTrackerRestoreCompleted),
                    (TelemetryPropertyName.PackageRestoreProjectId, ProjectTelemetryId),
                    (TelemetryPropertyName.PackageRestoreProgressTrackerId, packageRestoreProgressTrackerId),
                });
        }
    }
}
