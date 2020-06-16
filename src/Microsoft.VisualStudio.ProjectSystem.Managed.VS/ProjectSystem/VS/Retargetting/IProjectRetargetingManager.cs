// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    [ProjectSystemContract(ProjectSystemContractScope.ProjectService, ProjectSystemContractProvider.Private, Cardinality = Composition.ImportCardinality.ExactlyOne)]
    internal interface IProjectRetargetingManager
    {
        void ReportProjectMightRetarget(string projectFile);
        void ReportProjectNeedsRetargeting(string projectFile, ProjectTargetChange changes);
    }
}
