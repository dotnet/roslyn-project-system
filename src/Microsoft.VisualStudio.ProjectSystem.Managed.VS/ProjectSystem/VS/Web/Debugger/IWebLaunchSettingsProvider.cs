// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    /// <summary>
    /// Creates an in-memory launch profile representing launching the web server from data in the flavored project section
    /// </summary>
    internal interface IWebLaunchSettingsProvider
    {
        Task<WebLaunchSettings> GetLaunchSettingsAsync();       
    }
}
