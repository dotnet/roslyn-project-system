// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    [ProjectSystemContract(ProjectSystemContractScope.ConfiguredProject, ProjectSystemContractProvider.Private, Cardinality =  Composition.ImportCardinality.ExactlyOne)]
    internal interface IConfiguredProjectRetargetingDataSource : IProjectValueDataSource<IImmutableList<ProjectTargetChange>>
    {
    }
}
