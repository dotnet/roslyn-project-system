// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    internal interface IWebProjectProperties
    {
        string ApplicationDirectory
        {
            get;
        }

        Uri? ApplicationUrl
        {
            get;
        }

        Uri? BrowseUrl
        {
            get;
        }

        string ProjectDirectory
        {
            get;
        }

        Uri? ProjectUrl
        {
            get;
        }
        
        ServerType ServerType
        {
            get;
        }

        bool IISExpressUsesGlobalAppHostCfgFile
        {
            get;
        }
    }
}
