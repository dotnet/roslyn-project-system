// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.VS.PackageRestore
{
    // implemented by the project system, and is called by NuGet to know whether there is any pending restore work
    // so it can hold the current batch.
    public interface IVsProjectRestoreInfoSource
    {
        bool HasPendingNomination();

        Task WhenRestoreNominated();
    }
}
