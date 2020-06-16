// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    internal abstract class TargetDescriptionBase : IVsProjectTargetDescription2
    {
        public IVsProjectAcquisitionSetupDriver? ActualSetupDriver { get; set; }
        public abstract Guid TargetId { get; }
        public abstract string DisplayName { get; }
        public virtual uint Order => 1000;
        public abstract bool Supported { get; }
        public abstract Guid SetupDriver { get; }

        /// <summary>
        /// Text for the menu item that fixes the problem with the project
        /// </summary>
        public abstract string CommandTitle { get; }

        public virtual string GetRetargetParameterDisplayName(string parameter) => parameter;
        public virtual string GetParameterValue(string parameter) => parameter;
        public virtual Array GetPossibleParameterValues(string parameter) => Array.Empty<string>();
        public virtual Array GetRetargetParameters() => Array.Empty<string>();
        public virtual string GetValueDisplayName(string parameter, string pValue) => "";
        public virtual void PutParameterValue(string parameter, string pValue)
        {
        }
        public virtual void ResetSelectedValues()
        {
        }

        public virtual object? GetProperty(uint prop)
        {
            switch ((__VSPTDPROPID)prop)
            {
                case __VSPTDPROPID.VSPTDPROPID_AcquisitionCommandTitle:
                case __VSPTDPROPID.VSPTDPROPID_RetargetProjectCommandTitle:
                case __VSPTDPROPID.VSPTDPROPID_RetargetSolutionCommandTitle:
                    return CommandTitle;

                case __VSPTDPROPID.VSPTDPROPID_AcquisitionSetupDriver:
                    return ActualSetupDriver;

                case __VSPTDPROPID.VSPTDPROPID_ProjectRetargetingDescription:
                    break;
                case __VSPTDPROPID.VSPTDPROPID_ProjectRetargetingTitle:
                    break;
                case __VSPTDPROPID.VSPTDPROPID_ProjectUnloadUntilRetargetedDescription:
                    break;
                case __VSPTDPROPID.VSPTDPROPID_ProjectUnloadUntilRetargetedTitle:
                    break;
                case __VSPTDPROPID.VSPTDPROPID_UnloadInfoLine:
                    break;
                case __VSPTDPROPID.VSPTDPROPID_UnloadTitle:
                    break;

                case __VSPTDPROPID.VSPTDPROPID_ProjectUnloadUntilRetargetedTaskPriority:
                    break;
                case __VSPTDPROPID.VSPTDPROPID_ProjectRetargetingTaskPriority:
                    break;

                case __VSPTDPROPID.VSPTDPROPID_DoNotAdvertiseRetarget:
                    break;
                case __VSPTDPROPID.VSPTDPROPID_MigrationNextStepGuidanceLink:
                    break;
                case __VSPTDPROPID.VSPTDPROPID_ProjectRetargetingGuidanceLink:
                    break;

                case __VSPTDPROPID.VSPTDPROPID_AcquisitionComponents:
                    break;
                case __VSPTDPROPID.VSPTDPROPID_DoBackup:
                    break;
                case __VSPTDPROPID.VSPTDPROPID_IncludeAllProjectsForProjectRetargeting:
                    break;
                case __VSPTDPROPID.VSPTDPROPID_InitUnselectedInSolutionRetargeting:
                    break;
                case __VSPTDPROPID.VSPTDPROPID_MissingPrerequisites:
                    break;

                default:
                    break;
            }
            return null;
        }
    }
}
