// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    internal class WebProjectProperties : IWebProjectProperties
    {
        public WebProjectProperties(
            string applicationDirectory,
            string projectDirectory,
            WebLaunchSettings webLaunchSettings)
        {
            ApplicationDirectory = applicationDirectory;
            ProjectDirectory = projectDirectory;

            if (webLaunchSettings.ServerUrls.Count > 0)
            {
                var projectUri = new Uri(webLaunchSettings.ServerUrls[0]);
                ProjectUrl = projectUri;
                ApplicationUrl = projectUri;
                BrowseUrl = projectUri;
            }

            ServerType = webLaunchSettings.ServerType;
            IISExpressUsesGlobalAppHostCfgFile = webLaunchSettings.UseGlobalAppHostCfgFile;
        }

        public string ApplicationDirectory { get; }

        public Uri? ApplicationUrl { get; }

        public Uri? BrowseUrl { get; }

        public string ProjectDirectory { get; }

        public Uri? ProjectUrl { get; }

        public ServerType ServerType { get; }

        public bool IISExpressUsesGlobalAppHostCfgFile { get; }
    }
}
