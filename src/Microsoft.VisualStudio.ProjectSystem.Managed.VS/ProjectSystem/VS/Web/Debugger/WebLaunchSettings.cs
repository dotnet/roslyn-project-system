// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    internal enum ServerType
    {
        IISExpress,
        IIS,
        Custom
    }

    /// <summary>
    /// Represents the current set of settings used to run the project
    /// </summary>
    internal class WebLaunchSettings
    {
        public WebLaunchSettings(ServerType serverType, IReadOnlyList<string> serverUrls, bool useWindowsAuth, bool useAnonymousAuth, bool useClassicPipelineMode = false, 
                                 bool use64bitIISExpress = false, bool useGlobalAppHostCfgFile = false,bool overrideAppRootUrl = false, 
                                 string? iisOverrideAppRootUrl = null)
        {
            ServerType = serverType;
            ServerUrls = serverUrls;
            Use64bitIISExpress = use64bitIISExpress;
            UseWindowsAuth = useWindowsAuth;
            UseAnonymousAuth = useAnonymousAuth;
            UseClassicPipelineMode = useClassicPipelineMode;
            UseGlobalAppHostCfgFile = useGlobalAppHostCfgFile;
            UseOverrideAppRootUrl = overrideAppRootUrl;
            OverrideAppRootUrl = iisOverrideAppRootUrl;
        }

        public ServerType ServerType { get; private set; }
        public IReadOnlyList<string> ServerUrls { get; private set; }
        public bool UseWindowsAuth { get; private set; }
        public bool UseAnonymousAuth { get; private set; }
        public bool UseClassicPipelineMode { get; private set; }
        public bool Use64bitIISExpress { get; private set; }
        public bool UseGlobalAppHostCfgFile { get; private set; }
        public bool UseOverrideAppRootUrl { get; private set; }
        public string? OverrideAppRootUrl { get; private set; }
    }
}
