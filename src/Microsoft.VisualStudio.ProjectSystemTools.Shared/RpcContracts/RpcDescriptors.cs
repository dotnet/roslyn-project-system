// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Microsoft.ServiceHub.Framework;

namespace Microsoft.VisualStudio.ProjectSystem.Tools.BuildLogging.Model.RpcContracts
{
    public static class RpcDescriptors
    {
        /// <summary>
        /// Gets the <see cref="ServiceRpcDescriptor"/> for the build logging service.
        /// Use the <see cref="IBuildLoggerService"/> interface for the client proxy for this service.
        /// </summary>
        public static ServiceRpcDescriptor LoggerServiceDescriptor { get; } = new ServiceJsonRpcDescriptor(
            new ServiceMoniker("LoggerService", new Version(1, 0)),
            ServiceJsonRpcDescriptor.Formatters.UTF8,
            ServiceJsonRpcDescriptor.MessageDelimiters.HttpLikeHeaders);
    }
}
