// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    public partial class UnconfiguredProjectRetargetingDataSourceTests
    {
        [Theory]
        [MemberData(nameof(GetTargetChanges))]
        public void GetMostImportantChangeTests(IProjectTargetChange current, IProjectTargetChange candidate, bool candidateShouldBeReturned)
        {
            var result = UnconfiguredProjectRetargetingDataSource.GetMostImportantChange((ProjectTargetChange)current, (ProjectTargetChange)candidate);

            Assert.Equal(candidateShouldBeReturned, result == candidate);
        }

        public static IEnumerable<object[]> GetTargetChanges()
        {
            var missingVSSetupPreReq = ProjectTargetChange.CreateForPrerequisite(new Prerequisite(VSConstants.SetupDrivers.SetupDriver_VS));
            var missingOOBPreReq = ProjectTargetChange.CreateForPrerequisite(new Prerequisite(VSConstants.SetupDrivers.SetupDriver_OOBFeed));
            var retarget = ProjectTargetChange.CreateForRetarget(new ProjectRetarget(), IProjectRetargetCheckProviderFactory.Create());

            yield return new object[] { retarget, missingVSSetupPreReq, true };
            yield return new object[] { missingVSSetupPreReq, retarget, false };
            yield return new object[] { missingOOBPreReq, missingVSSetupPreReq, true };
            yield return new object[] { missingVSSetupPreReq, missingOOBPreReq, false };
            yield return new object[] { retarget, missingOOBPreReq, true };
            yield return new object[] { missingOOBPreReq, retarget, false };
            yield return new object[] { ProjectTargetChange.None, retarget, true };
        }
    }
}
