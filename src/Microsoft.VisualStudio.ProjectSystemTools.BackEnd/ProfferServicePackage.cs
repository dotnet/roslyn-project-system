// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.ServiceBroker;
using Microsoft.VisualStudio.ProjectSystem.Tools.BuildLogging.Model.RpcContracts;
using ProvideBrokeredServiceAttribute = Microsoft.VisualStudio.Shell.ServiceBroker.ProvideBrokeredServiceAttribute;
using Microsoft.VisualStudio.ComponentModelHost;

namespace Microsoft.VisualStudio.ProjectSystemTools.BackEnd
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid("6BEB9F10-4E64-41B7-B4E3-1BE77CD63DD4")]
    [ProvideBrokeredService("LoggerService", "1.0", Audience = ServiceAudience.AllClientsIncludingGuests)]
    public class ProfferServicePackage : AsyncPackage
    {
        protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            await JoinableTaskFactory.SwitchToMainThreadAsync();

            var componentModel = (IComponentModel)await GetServiceAsync(typeof(SComponentModel));
            Assumes.Present(componentModel);
            IBuildLoggerService loggerService = componentModel.GetService<IBuildLoggerService>();

            IBrokeredServiceContainer brokeredServiceContainer = await this.GetServiceAsync<SVsBrokeredServiceContainer, IBrokeredServiceContainer>();
            brokeredServiceContainer.Proffer(RpcDescriptors.LoggerServiceDescriptor, (mk, options, sb, ct) => new ValueTask<object?>(loggerService));
        }
    }
}
