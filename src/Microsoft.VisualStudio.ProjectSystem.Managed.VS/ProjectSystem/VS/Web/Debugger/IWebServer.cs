// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    internal enum WebServerStartOption
    {
        Any,
        Debug,
        Profile
    }

    internal interface IWebServer
    {
       string? ProjectUrl { get; }
       IReadOnlyList<string> WebServerUrls { get; }
       ServerType ActiveWebServerType { get; }

       Task ConfigureWebServerAsync();
       Task<bool> IsRunningAsync();
       Task<bool> WaitForListeningAsync();
       Task StartAsync(WebServerStartOption startOption);
       Task StopAsync();
       Task<(string exePath, string commandLine)> GetWebServerCommandLineAsync();
       Task<int> GetWebServerProcessIdAsync();
    }
}
