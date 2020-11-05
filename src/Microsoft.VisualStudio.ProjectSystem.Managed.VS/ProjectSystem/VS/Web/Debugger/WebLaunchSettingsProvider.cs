// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debug;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    /// <summary>
    /// Creates an in-memory launch profile representing launching the web server from data in the flavored project section
    /// </summary>
    [Export(typeof(IWebLaunchSettingsProvider))]
    [AppliesTo(ProjectCapability.AspNetLaunchProfiles)]
    internal class WebLaunchSettingsProvider : OnceInitializedOnceDisposedAsync, IWebLaunchSettingsProvider
    {

        private static class ProjectFileProperties
        {
            public const string UseIISExpress = nameof(UseIISExpress);
            public const string Use64BitIISExpress = nameof(Use64BitIISExpress);
            public const string IISExpressWindowsAuthentication = nameof(IISExpressWindowsAuthentication);
            public const string IISExpressAnonymousAuthentication = nameof(IISExpressAnonymousAuthentication);
            public const string IISExpressSSLPort = nameof(IISExpressSSLPort);
            public const string IISExpressUseClassicPipelineMode = nameof(IISExpressUseClassicPipelineMode);
            public const string UseGlobalApplicationHostFile = nameof(UseGlobalApplicationHostFile);
        }

        private const string WAPProjectGuidString = "{" + ProjectType.LegacyWebApplicationProject + "}";
        private readonly IUnconfiguredProjectCommonServices _projectServices;
        private readonly IProjectExtensionDataService _projectExtensionDataService;
        private FlavorServerSettings? _flavorServerSettings;

        private bool _useIISExpress;
        private bool _use64BitIISExpress;
        private int _issExpressSSLPort;
        private bool _useWindowsAuth;
        private bool _useAnonymousAuth;
        private bool _useClassicPipelineMode;
        private bool _useGlobalApplicationHostFile;

        [ImportingConstructor]
        public WebLaunchSettingsProvider(
            ILaunchSettingsProvider2 launchSettingsProvider,
            IUnconfiguredProjectCommonServices projectServices,
            IProjectExtensionDataService projectExtensionDataService,
            IProjectThreadingService threadingService)
             : base(threadingService.JoinableTaskContext)
        {
            _projectServices = projectServices;
            _projectExtensionDataService = projectExtensionDataService;
        }

        public async Task<WebLaunchSettings> GetLaunchSettingsAsync()
        {
            await InitializeAsync();

            List<string> urls = new List<string>();
            ServerType serverType = ServerType.IISExpress;

            if (_flavorServerSettings == null)
            {
                return new WebLaunchSettings(serverType, urls, _useWindowsAuth, _useAnonymousAuth, _useClassicPipelineMode, _use64BitIISExpress, _useGlobalApplicationHostFile);
            }

            if (_flavorServerSettings.UseCustomServer)
            {
                if (!Strings.IsNullOrWhiteSpace(_flavorServerSettings.CustomServerUrl))
                {
                    urls.Add(_flavorServerSettings.CustomServerUrl);
                }

                serverType = ServerType.Custom;
            }
            else if (_flavorServerSettings.UseIIS)
            {
                if (_useIISExpress)
                {
                    if (!Strings.IsNullOrWhiteSpace(_flavorServerSettings.IISUrl))
                    {
                        urls.Add(_flavorServerSettings.IISUrl);
                        if (_issExpressSSLPort != 0 && !_flavorServerSettings.IISUrl.StartsWith(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                        {
                            var uriBuilder = new UriBuilder(_flavorServerSettings.IISUrl);
                            uriBuilder.Port = _issExpressSSLPort;
                            uriBuilder.Scheme = Uri.UriSchemeHttps;
                            urls.Add(uriBuilder.Uri.AbsoluteUri);
                        }
                    }
                    else
                    {
                        // We'll just create new ones
                        AddNewIISExpressUrls(urls, _flavorServerSettings.DevelopmentServerPort, _flavorServerSettings.DevelopmentServerVPath);
                    }
                }
                else
                {
                    serverType = ServerType.IIS;
                    if (!Strings.IsNullOrWhiteSpace(_flavorServerSettings.IISUrl))
                    {
                        urls.Add(_flavorServerSettings.IISUrl);
                    }
                }
            }
            else
            {
                // In this case we use IIS Express but synthesize the url from the development server port
                AddNewIISExpressUrls(urls, _flavorServerSettings.DevelopmentServerPort, _flavorServerSettings.DevelopmentServerVPath);
            }

            return new WebLaunchSettings(serverType, urls, _useWindowsAuth, _useAnonymousAuth, _useClassicPipelineMode, _use64BitIISExpress, _useGlobalApplicationHostFile,
                                         _flavorServerSettings.UseIISAppRootOverrideUrl, _flavorServerSettings.IISAppRootOverrideUrl);
        }

        private void AddNewIISExpressUrls(List<string> urls, int port, string vPath)
        {
            if (port == 0)
            {
                port = GetNextAvailablePort();
            }

            urls.Add($"http://localhost:{port}{vPath}");

            if (_issExpressSSLPort != 0)
            {
                urls.Add($"http://localhost:{_issExpressSSLPort}{vPath}");
            }
        }

        protected override async Task InitializeCoreAsync(CancellationToken cancellationToken)
        {
            var root = await FindFlavoredElement();
            if (root != null)
            {
                _flavorServerSettings = FlavorServerSettings.CreateFromXml(root);
            }

            await ReadPropertySettingsAsync();
        }

        private async Task<XElement?> FindFlavoredElement()
        {
            var xmlData = await _projectExtensionDataService.GetXmlAsync("VisualStudio");
            foreach (var element in xmlData)
            {
                if (string.Equals(element.Name.LocalName, "FlavorProperties", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)element.Attribute("GUID"), WAPProjectGuidString, StringComparison.OrdinalIgnoreCase))
                {
                    return element;
                }
            }

            return null;
        }

        private async Task ReadPropertySettingsAsync()
        {
            var commonProps = _projectServices.ActiveConfiguredProject?.Services?.ProjectPropertiesProvider?.GetCommonProperties();
            if (commonProps == null)
            {
                return;
            }

            var stringVal = await commonProps.GetEvaluatedPropertyValueAsync(ProjectFileProperties.IISExpressSSLPort);
            int.TryParse(stringVal, out _issExpressSSLPort);

            stringVal = await commonProps.GetEvaluatedPropertyValueAsync(ProjectFileProperties.UseIISExpress);
            bool.TryParse(stringVal, out _useIISExpress);

            stringVal = await commonProps.GetEvaluatedPropertyValueAsync(ProjectFileProperties.Use64BitIISExpress);
            bool.TryParse(stringVal, out _use64BitIISExpress);

            stringVal = await commonProps.GetEvaluatedPropertyValueAsync(ProjectFileProperties.IISExpressAnonymousAuthentication);
            if (TryParseEnabled(stringVal, out bool isEnabled))
            {
                _useAnonymousAuth = isEnabled;
            }

            stringVal = await commonProps.GetEvaluatedPropertyValueAsync(ProjectFileProperties.IISExpressWindowsAuthentication);
            if (TryParseEnabled(stringVal, out isEnabled))
            {
                _useWindowsAuth = isEnabled;
            }

            stringVal = await commonProps.GetEvaluatedPropertyValueAsync(ProjectFileProperties.IISExpressUseClassicPipelineMode);
            bool.TryParse(stringVal, out _useClassicPipelineMode);

            stringVal = await commonProps.GetEvaluatedPropertyValueAsync(ProjectFileProperties.UseGlobalApplicationHostFile);
            bool.TryParse(stringVal, out _useGlobalApplicationHostFile);
        }

        private static bool TryParseEnabled(string stringVal, out bool isEnabled)
        {
            isEnabled = false;
            if (string.Compare(stringVal, "enabled", StringComparison.OrdinalIgnoreCase) == 0)
            {
                isEnabled = true;
                return true;
            }
            else if (string.Compare(stringVal, "disabled", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the next available dynamic port. Filters unsafe ports as defined by chrome 
        /// (http://superuser.com/questions/188058/which-ports-are-considered-unsafe-on-chrome)
        /// </summary>
        private static readonly int[] s_unsafePorts = new int[] {
                    2049, // nfs
                    3659, // apple-sasl / PasswordServer
                    4045, // lockd
                    6000, // X11
                    6665, // Alternate IRC [Apple addition]
                    6666, // Alternate IRC [Apple addition]
                    6667, // Standard IRC [Apple addition]
                    6668, // Alternate IRC [Apple addition]
                    6669, // Alternate IRC [Apple addition]
        };

        public static int GetNextAvailablePort()
        {
            // Creates the Socket to send data over a TCP connection.
            const int MAX_RETRIES = 10;
            int port = 0;
            try
            {
                // In case every port we get is the same value, or other aberant behavior a maximum number of retries
                // will be used. At that point we just use the port we have - no filtering of ports blocked in Chrome
                int retries = 1;
                while (true)
                {
                    using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
                        socket.Bind(endPoint);
                        IPEndPoint endPointUsed = (IPEndPoint)socket.LocalEndPoint;
                        if (!s_unsafePorts.Contains(endPointUsed.Port) || retries == MAX_RETRIES)
                        {
                            port = endPointUsed.Port;
                            break;
                        }
                    }

                    retries++;
                }
            }
            catch (SocketException)
            {
            }

            if (port == 0)
            {
                try
                {
                    int retries = 1;
                    while (true)
                    {
                        using (Socket socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp))
                        {
                            IPEndPoint endPoint = new IPEndPoint(IPAddress.IPv6Any, 0);
                            socket.Bind(endPoint);
                            IPEndPoint endPointUsed = (IPEndPoint)socket.LocalEndPoint;
                            if (!s_unsafePorts.Contains(endPointUsed.Port) || retries == MAX_RETRIES)
                            {
                                port = endPointUsed.Port;
                                break;
                            }
                        }

                        retries++;
                    }
                }
                catch (SocketException)
                {
                }
            }

            return port;
        }
        protected override Task DisposeCoreAsync(bool initialized)
        {
            return Task.CompletedTask;
        }
    }
}
