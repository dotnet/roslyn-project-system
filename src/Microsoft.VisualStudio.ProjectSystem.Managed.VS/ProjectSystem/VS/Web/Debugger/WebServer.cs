// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Web.Application;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    internal enum VDirCollisionOption
    {
        Fail,
        Overwrite,
        PromptToOverwrite
    };

    /// <summary>
    /// Wraps the webserver being used by the project system
    /// </summary>
    [Export(typeof(IWebServer))]
    [AppliesTo(ProjectCapability.AspNetLaunchProfiles)]
    internal class WebServer : OnceInitializedOnceDisposedAsync, IWebServer, IVsDeveloperWebServerSvcEvents, IVsIISServiceEvents
    {
        private const int MaxNonElevatedPort = 1024;
        private const uint WST_IISExpress = 1;
        private const uint WebServerListeningTimeout = 10000;       // 10 seconds

        private readonly IWebLaunchSettingsProvider _launchSettingsProvider;
        private readonly IUnconfiguredProjectCommonServices _projectServices;
        private readonly IProjectThreadingService _threadingService;
        private readonly IVsUIService<IVsLocalIISService, IVsLocalIISService> _localIISService;
        private readonly IVsUIService<SVsUIShell, IVsUIShell> _uiShell;
        private readonly IVsUIService<SVsShell, IVsShell> _vsShell;
        private readonly IVsUIService<IVsWebAppUpgrade> _webAppUpgrade;
        private readonly IVsUIService<IVsDeveloperWebServerProviderSvc, IVsDeveloperWebServerProviderSvc> _webServerProviderSvc;
        private readonly IUserNotificationServices _userNotificationServices;
        private readonly IUnconfiguredProjectVsServices _projectVsServices;

        private IVsLocalIISService2 LocalIISService2 => (IVsLocalIISService2)_localIISService.Value;
        private IVsLocalIISService3 LocalIISService3 => (IVsLocalIISService3)_localIISService.Value;

        private IVsDeveloperWebServerSvc? _devWebServersServiceUsedToGetWebServer;
        private IVsDeveloperWebServer? _vsDeveloperWebServer;
        private uint _vsWebServerEventCookie;
        private uint _iisServiceEventsCookie;
        private bool _webAppUpgradeInitalized;

        private WebLaunchSettings? _currentSettings;

        [ImportingConstructor]
        public WebServer(IWebLaunchSettingsProvider launchSettingsProvider, IUnconfiguredProjectCommonServices projectServices,
                         IProjectThreadingService threadingService, IVsUIService<IVsLocalIISService, IVsLocalIISService> localIISService,
                         IVsUIService<SVsUIShell, IVsUIShell> uiShell, IVsUIService<SVsShell, IVsShell> vsShell, IVsUIService<IVsWebAppUpgrade> webAppUpgrade,
                         IVsUIService<IVsDeveloperWebServerProviderSvc, IVsDeveloperWebServerProviderSvc> webServerProviderSvc,
                         IUserNotificationServices userNotificationServices, IUnconfiguredProjectVsServices projectVsServices)
          :
            base(threadingService.JoinableTaskContext)
        {
            _launchSettingsProvider = launchSettingsProvider;
            _projectServices = projectServices;
            _threadingService = threadingService;
            _localIISService = localIISService;
            _uiShell = uiShell;
            _vsShell = vsShell;
            _webAppUpgrade = webAppUpgrade;
            _userNotificationServices = userNotificationServices;
            _webServerProviderSvc = webServerProviderSvc;
            _projectVsServices = projectVsServices;
        }

        public Task ConfigureWebServerAsync()
        {
            return InitializeAsync();
        }

        private async Task ConfigureWebServerInternalAsync()
        {
            WebLaunchSettings? settings = await _launchSettingsProvider.GetLaunchSettingsAsync();
            await ConfigureISSettingsAsync(settings);
            _currentSettings = settings;
        }

        public async Task<bool> IsRunningAsync()
        {
            WebLaunchSettings? settings = _currentSettings;
            if (settings == null)
            {
                return false;
            }

            if (settings.ServerType == ServerType.IIS)
            {
                // Assume IIS is running
                return true;
            }
            else if (settings.ServerType == ServerType.IISExpress)
            {
                await _threadingService.SwitchToUIThread();

                IVsDeveloperWebServer webServer = GetWebServerController();
                return webServer != null && webServer.IsRunning();
            }

            return false;
        }

        public async Task<bool> WaitForListeningAsync()
        {
            WebLaunchSettings? settings = _currentSettings;
            if (settings == null)
            {
                return false;
            }
            if (settings.ServerType == ServerType.IIS)
            {
                // Assume IIS is listening
                return true;
            }
            else if (settings.ServerType == ServerType.IISExpress)
            {
                await _threadingService.SwitchToUIThread();

                IVsDeveloperWebServer webServer = GetWebServerController();
                if (webServer != null && webServer.IsRunning())
                {
                    webServer.EnsureIsListening(WebServerListeningTimeout, out bool isListening);
                    return isListening;
                }
            }

            return false;
        }

        public async Task StartAsync(WebServerStartOption startOption)
        {
            await InitializeAsync();

            var settings = _currentSettings;
            if (settings == null)
            {
                return;
            }

            if (settings.ServerType == ServerType.IISExpress)
            {
                if (settings.Use64bitIISExpress == true && !Environment.Is64BitOperatingSystem)
                {
                    throw new COMException(string.Format(WebResources.Project_Requires64BitOS, ProjectName), HResult.Fail);
                }

                await _threadingService.SwitchToUIThread();

                var webServerController = GetWebServerController();

                if (ErrorHandler.Failed(webServerController.EnsureRunning((uint)startOption, environment: GetDeveloperEnvironement())))
                {
                    // Hopefully we have the startup failure captured and make sure it is stopped
                    string stdError = webServerController.GetProperty("StartupErrorMessage");
                    webServerController.Stop();

                    throw new COMException(string.Format(WebResources.UnableToLaunchIISExpress, stdError), HResult.Fail);
                }
            }
        }

        public async Task StopAsync()
        {
            var settings = _currentSettings;
            if (settings == null)
            {
                return;
            }

            if (settings.ServerType == ServerType.IISExpress)
            {
                await _threadingService.SwitchToUIThread();

                IVsDeveloperWebServer webServer = GetWebServerController();
                if (webServer != null && webServer.IsRunning())
                {
                    webServer.Stop();
                }
            }
        }

        public async Task<(string exePath, string commandLine)> GetWebServerCommandLineAsync()
        {
            var settings = _currentSettings;

            if (settings != null && settings.ServerType == ServerType.IISExpress)
            {
                await _threadingService.SwitchToUIThread();

                IVsDeveloperWebServer webServer = GetWebServerController();
                webServer.GetProcessInformation(out string exePath, out string commandLine);
                return (exePath, commandLine);
            }

            return (string.Empty, string.Empty);
        }

        public async Task<int> GetWebServerProcessIdAsync()
        {
            var settings = _currentSettings;

            if (settings != null && settings.ServerType == ServerType.IISExpress)
            {
                await _threadingService.SwitchToUIThread();

                IVsDeveloperWebServer webServer = GetWebServerController();
                webServer.GetExistingProcess(out uint pid);
                return (int)pid;
            }

            return 0;
        }

        private string ProjectDirectory => Path.GetDirectoryName(_projectServices.Project.FullPath);
        private string ProjectName => Path.GetFileNameWithoutExtension(_projectServices.Project.FullPath);
        private string? TargetFrameworkMoniker
        {
            get
            {
                _projectVsServices.VsHierarchy.GetProperty(VsHierarchyPropID.TargetFrameworkMoniker, defaultValue: null, result: out string? targetFramework);
                return targetFramework;
            }
        }

        private Version TargetFrameworkVersion
        {
            get
            {
                var moniker = TargetFrameworkMoniker;
                if (moniker != null)
                {
                    return new FrameworkName(moniker).Version;
                }

                return new Version();
            }
        }

        public string? ProjectUrl
        {
            get
            {
                if (_currentSettings == null || _currentSettings.ServerUrls.Count == 0)
                {
                    return null;
                }

                return _currentSettings.ServerUrls[0];
            }
        }

        public IReadOnlyList<string> WebServerUrls => _currentSettings?.ServerUrls ?? Array.Empty<string>();

        public ServerType ActiveWebServerType => _currentSettings == null ? ServerType.IISExpress : _currentSettings.ServerType;

        /// <summary>
        /// Helper validates that the user has access to the IIS metabase and that the vdir is 
        /// correctly configured.
        /// </summary>
        private async Task ConfigureISSettingsAsync(WebLaunchSettings settings)
        {
            await _threadingService.SwitchToUIThread();

            if (settings.ServerType == ServerType.Custom || settings.ServerUrls.Count == 0)
            {
                return;
            }

            // Note whether we are overriding the app root url
            bool isOverridingAppUrl = settings.UseOverrideAppRootUrl && !string.IsNullOrEmpty(settings.OverrideAppRootUrl);

            SplitUrl(settings.ServerUrls[0], out string server, out string relUrl);
            bool isIISExpress = settings.ServerType == ServerType.IISExpress;

            int hr = GetIVsIISSite(settings, out IVsIISSite? vsIISSite);
            if (ErrorHandler.Succeeded(hr) && vsIISSite != null)
            {
                vsIISSite.GetPathForRelativeUrl(relUrl, out string? fullPath, out bool bIsVDir, out bool bIsApp);
                vsIISSite.IsIISExpress(out bool bIsHostedOnIISExpress);
                System.Diagnostics.Debug.Assert(bIsHostedOnIISExpress == isIISExpress);

                // Only thing left to do is ensure that path matches and if we are not overriding the app url, make sure it is marked as an application. If path doesn't 
                // match and is valid we need to prompt to change it. 
                // Otherwise, we just overwrite it.
                if (!DirectoriesMatch(fullPath, ProjectDirectory) || (!isOverridingAppUrl && !bIsApp))
                {
                    hr = TryCreateVDirectory(vsIISSite, settings.ServerUrls[0], relUrl, bIsVDir, fullPath, false/*promptToReplace*/);
                    if (ErrorHandler.Failed(hr) && hr != HResult.Abort)
                    {
                        throw new COMException(string.Format(WebResources.Project_ErrCreatingVDIROnOpen, ProjectName, GetErrorInfo()), hr);
                    }
                }
            }
            else if (hr == ErrorCodes.ERROR_INVALID_DATA)
            {
                // ApplicationHost.config file is invalid.
                if (settings.ServerType == ServerType.IISExpress)
                {
                    throw new COMException(string.Format(WebResources.Project_InvalidAppHost_IISX_Open, ProjectName, GetErrorInfo()), hr);
                }
                else
                {
                    throw new COMException(string.Format(WebResources.Project_InvalidAppHost_IIS_Open, ProjectName, GetErrorInfo()), hr);
                }
            }
            else
            {
                if (settings.ServerType == ServerType.IISExpress)
                {
                    // IIS express is there, just configure it.
                    hr = CreateSiteAndVDir(settings, VDirCollisionOption.Overwrite, out vsIISSite);
                    if (ErrorHandler.Failed(hr) && hr != HResult.Abort)
                    {
                        throw new COMException(string.Format(WebResources.Project_ErrCreatingVDIROnOpen, ProjectName, GetErrorInfo()), hr);
                    }
                }
                else
                {   
                    // IIS Case. Just let the failure go through. It should already report if IIS is not installed, or some other problem
                    if (!IISIsInstalled)
                    {
                        throw new COMException(string.Format(WebResources.Project_IISNotInstalled, ProjectName), hr);
                    }
                    else
                    {
                        throw new COMException(string.Format(WebResources.Project_IIS_Required, ProjectName, GetErrorInfo()), hr);
                    }
                }

                // If all that works we need to make sure the script maps are OK. vsIISSite will be null when ConvertToIISExpressHelper() has been called.
                // However, it will have already done all the configuration below and the member variable _vsIISSite will be set.
                if (ErrorHandler.Succeeded(hr) && vsIISSite != null)
                {
                    Version ourVersion = TargetFrameworkVersion;

                    ScriptMapInfo scriptInfo = ScriptMapInfo.NoInfo;
                    if (ErrorHandler.Succeeded(vsIISSite.GetScriptMapInfo(relUrl, out string strVersion, ref scriptInfo)))
                    {
                        // IIS express doesn't care about this
                        if (!isIISExpress && (scriptInfo & ScriptMapInfo.NotRegistered) == ScriptMapInfo.NotRegistered)
                        {
                            _userNotificationServices.ShowError(string.Format(WebResources.Project_AspNetNotRegistered, ourVersion.ToString(2)));
                        }
                        else
                        {
                            if (strVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                            {
                                strVersion = strVersion.Substring(1);
                            }

                            Version v = new Version(strVersion);
                            uint majorVersion = (uint)(ourVersion.Major >= 4 ? 4 : 2);
                            if (v.Major != majorVersion)
                            {
                                // Don't do any prompting in iis express scenarios
                                if (isIISExpress || _userNotificationServices.Confirm(string.Format(WebResources.Project_P_FixScriptMaps, settings.ServerUrls[0], ourVersion.ToString(2))))
                                {
                                    UpdateScriptMaps(vsIISSite, relUrl, ourVersion, true/*isUpgrade*/);
                                }
                            }
                        }
                    }
                }
            }
        }

        public int CreateSiteAndVDir(WebLaunchSettings settings, VDirCollisionOption vdirOption, out IVsIISSite? vsIISSite)
        {
            int hr = HResult.OK;
            vsIISSite = null;

            try
            {
                SplitUrl(settings.ServerUrls[0], out string siteUrl, out string serverRelUrl);

                Uri uri = new Uri(settings.ServerUrls[0]);
                string uriString = settings.ServerUrls[0];
                string errText;
                bool fIsFullUrlASite = (string.IsNullOrEmpty(serverRelUrl) || (serverRelUrl.Length == 1 && serverRelUrl[0] == '/'));

                bool isOverridingAppUrl = settings.UseOverrideAppRootUrl && !string.IsNullOrEmpty(settings.OverrideAppRootUrl);

                int hrSite = GetIVsIISSite(settings, out vsIISSite);
                if (ErrorHandler.Succeeded(hrSite) && vsIISSite != null)
                {
                    vsIISSite.GetPathForRelativeUrl(serverRelUrl, out string sitePath, out bool isVdir, out bool isApp);
                    if (fIsFullUrlASite)
                    {
                        // Our URL is a site URL. We check to see where it is currently mapped to. If it is to our project folder we are done. For IIS Express Only 
                        // we will offer to remap the site to our project folder.
                        if (DirectoriesMatch(sitePath, ProjectDirectory))
                        {
                            return HResult.OK;
                        }
                        else if (vdirOption == VDirCollisionOption.Fail)
                        {
                            hr = HResult.Fail;
                            SetErrorInfo(hr, string.Format(WebResources.Project_ErrAlreadyHostedOnDifferentFolder, uri.OriginalString, sitePath));

                        }
                        else if (vdirOption == VDirCollisionOption.PromptToOverwrite)
                        {
                            if (_userNotificationServices.Confirm(string.Format(WebResources.Project_P_ReMapVDir_Path2, settings.ServerUrls[0], sitePath)))
                            {
                                hr = CreateVDirectory(vsIISSite, uriString, serverRelUrl, overwriteExisting: true, isUpgrade: false);
                            }
                            else
                            {
                                hr = HResult.Abort;
                            }
                        }
                        else
                        {
                            hr = CreateVDirectory(vsIISSite, uriString, serverRelUrl, overwriteExisting: true, isUpgrade: false);
                        }
                    }
                    else
                    {
                        // URL is a Sub Web
                        int hrApp = vsIISSite.GetPathForRelativeUrl(serverRelUrl, out string? fullPath, out isVdir, out isApp);
                        if (ErrorHandler.Succeeded(hrApp) && fullPath != null)
                        {
                            // found the App see if it points to our project folder
                            if (DirectoriesMatch(fullPath, ProjectDirectory) && (isOverridingAppUrl || isApp))
                            {
                                hr = HResult.OK;
                            }
                            else
                            {   // If it is not a vdir, or the path doesn't exist, or the folder is empty, we silently overwrite, otherwise
                                // we prompt to overwrite.
                                if (vdirOption == VDirCollisionOption.Overwrite || !isVdir || !Directory.Exists(fullPath) || DirectoryIsEmpty(fullPath))
                                {
                                    hr = CreateVDirectory(vsIISSite, uriString, serverRelUrl, overwriteExisting: true, isUpgrade: false);
                                }
                                else if (vdirOption == VDirCollisionOption.PromptToOverwrite)
                                {
                                    if (_userNotificationServices.Confirm(string.Format(WebResources.Project_P_ReMapVDir_Path2, settings.ServerUrls[0], fullPath)))
                                    {
                                        hr = CreateVDirectory(vsIISSite, uriString, serverRelUrl, overwriteExisting: true, isUpgrade: false);
                                    }
                                    else
                                    {
                                        hr = HResult.Abort;
                                    }
                                }
                                else
                                {
                                    hr = HResult.Fail;
                                    SetErrorInfo(hr, string.Format(WebResources.Project_ErrAlreadyHostedOnDifferentFolder, settings.ServerUrls[0], fullPath));
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.Assert(!fIsFullUrlASite, "We don't expected this is bound to a site");
                            hr = CreateVDirectory(vsIISSite, uriString, serverRelUrl, overwriteExisting: true, isUpgrade: false);
                        }
                    }
                }
                else if (hrSite == ErrorCodes.ERROR_INVALID_DATA)
                {
                    // ApplicationHost.config file is invalid.
                    if (settings.ServerType == ServerType.IISExpress)
                    {
                        SetErrorInfo(hrSite, string.Format(WebResources.Project_InvalidAppHost_IISX_Open, ProjectName, GetErrorInfo()));
                    }
                    else
                    {
                        SetErrorInfo(hrSite, string.Format(WebResources.Project_InvalidAppHost_IIS_Open, ProjectName, GetErrorInfo()));
                    }

                    hr = hrSite;
                }
                else
                {
                    // The site itself does not exist.
                    if (settings.ServerType == ServerType.IISExpress)
                    {
                        if (!uri.IsLoopback)
                        {
                            // Must specify a local url. 
                            hr = HResult.Fail;
                            errText = string.Format(WebResources.Project_LocalUrlRequiredToCreateSite, uriString);
                            SetErrorInfo(hr, errText);
                        }
                        else if (uri.Port < MaxNonElevatedPort && !VSIsRunningElevated())
                        {
                            // Can't create sites on ports less than 1024 unless running elevated
                            hr = HResult.Fail;
                            SetErrorInfo(hr, string.Format(WebResources.Project_AdminReqToCreateSiteWithPort, uriString));
                        }
                        else
                        {
                            IVsIISExpressService? iisExpSvc = GetIISExpressService(settings.UseGlobalAppHostCfgFile);

                            // site is not bound
                            if (!fIsFullUrlASite)
                            {
                                // Create the site name.
                                string sitename = ProjectName + "-Site";
                                if (ErrorHandler.Failed(iisExpSvc.GetUniqueNewSiteName(sitename, out string siteUniqueName)))
                                {
                                    siteUniqueName = sitename;
                                }

                                // Passing null for the disk path, will default the path to siteUniqueName under the default location 
                                // for iis express sites. We don't really care, since we only care about the vdir pointing back to
                                // us.
                                hr = iisExpSvc.CreateNewIISSite(siteUniqueName, null/*diskPath*/, (ushort)uri.Port, uri.Scheme == Uri.UriSchemeHttps/*AddSecureBindings*/, out vsIISSite);
                                if (ErrorHandler.Succeeded(hr))
                                {
                                    UpdateScriptMaps(vsIISSite, string.Empty, TargetFrameworkVersion, false/*isUpgrade*/);
                                    hr = CreateVDirectory(vsIISSite, uriString, serverRelUrl, overwriteExisting: true, isUpgrade: false);
                                }
                                else if (hr == ErrorCodes.ERROR_INVALID_DATA)
                                {
                                    SetErrorInfo(hr, string.Format(WebResources.Project_InvalidAppHost_IISX, GetErrorInfo()));
                                }
                            }
                            else
                            {
                                // Create as a site and update script maps (something CreateVDirectory() already does)
                                if (ErrorHandler.Failed(iisExpSvc.GetUniqueNewSiteName(ProjectName, out string siteUniqueName)))
                                {
                                    siteUniqueName = ProjectName;
                                }

                                hr = iisExpSvc.CreateNewIISSite(siteUniqueName, ProjectDirectory, (ushort)uri.Port, uri.Scheme == Uri.UriSchemeHttps/*AddSecureBindings*/, out vsIISSite);
                                if (ErrorHandler.Succeeded(hr))
                                {
                                    UpdateScriptMaps(vsIISSite, "", TargetFrameworkVersion, false/*isUpgrade*/);
                                }
                                else if (hr == ErrorCodes.ERROR_INVALID_DATA)
                                {
                                    SetErrorInfo(hr, string.Format(WebResources.Project_InvalidAppHost_IISX, GetErrorInfo()));
                                }
                            }
                        }
                    }
                }
            }
            catch (UriFormatException ex)
            {
                SetErrorInfo(HResult.Fail, string.Format(WebResources.Project_InvalidIISUrl1, settings.ServerUrls[0], ex.Message));
            }
            catch (Exception ex)
            {
                hr = HResult.Fail;
                SetErrorInfo(hr, ex.Message);
            }

            if (ErrorHandler.Failed(hr))
            {
                vsIISSite = null;
            }

            return hr;
        }

        /// <summary>
        /// Validates conditions required to create a Virtual Directory when there is an existing
        /// vdir which maps to a different folder.
        /// </summary>
        private int TryCreateVDirectory(IVsIISSite vsIISSite, string serverUrl, string serverRelUri, bool bIsVDir, string existingVDirPath, bool promptToReplace)
        {
            int hr = HResult.Fail;

            // If not a vdir, or the exisitng path doens't exist, or the existing directory is empty
            // we just blindly replace it. 
            if (!promptToReplace || !bIsVDir || !Directory.Exists(existingVDirPath) || DirectoryIsEmpty(existingVDirPath))
            {
                hr = CreateVDirectory(vsIISSite, serverUrl, serverRelUri, overwriteExisting: true, isUpgrade: false);
                if (ErrorHandler.Failed(hr) && hr != HResult.Abort)
                {
                    SetErrorInfo(hr, string.Format(WebResources.Project_ErrCreatingVDIROnOpen, serverUrl, GetErrorInfo()));
                }
            }
            else if (promptToReplace && _userNotificationServices.Confirm(string.Format(WebResources.Project_P_ReMapVDir_Path, serverUrl, ProjectName, existingVDirPath)))
            {
                hr = CreateVDirectory(vsIISSite, serverUrl, serverRelUri, overwriteExisting: true, isUpgrade: false);
                if (ErrorHandler.Failed(hr) && hr != HResult.Abort)
                {
                    SetErrorInfo(hr, string.Format(WebResources.Project_ErrCreatingVDIROnOpen, serverUrl, GetErrorInfo()));
                }
            }
            else
            {   // User canceled out of the prompt to replace the vdir,  so we just set an error message.
                hr = HResult.Fail;
                SetErrorInfo(hr, string.Format(WebResources.Project_E_VDirAlreadyMapped, serverUrl, ProjectName, existingVDirPath));
            }

            return hr;
        }

        public int CreateVDirectory(IVsIISSite vsIISSite, string fullUrl, string serverRelUrl, bool overwriteExisting, bool isUpgrade)
        {
            int hr = vsIISSite.CreateVirtualDirectory(serverRelUrl, ProjectDirectory, overwriteExisting);

            if (ErrorHandler.Succeeded(hr))
            {
                // Update script maps
                UpdateScriptMaps(vsIISSite, serverRelUrl, TargetFrameworkVersion, isUpgrade);

                // Don't modify permissions for IIS Express.
                vsIISSite.IsIISExpress(out bool isIISExpress);
                if (!isIISExpress)
                {
                    // Grant asp.net read access to the folder and write access to app_data
                    IVsWebAppUpgrade waUpgrade = GetWebAppUpgrade();
                    if (waUpgrade != null)
                    {
                        GetWebAppUpgrade().SetASPNETPermissionsForPath(fullUrl, true /*read access only*/);
                    }

                    SetDataDirectoryPermissions(fullUrl);
                    SeRoslynDirectoryPermissions(fullUrl);
                }
            }

            return hr;
        }

        private int UpdateScriptMaps(IVsIISSite site, string siteRelUrl, Version version, bool isUpgrade)
        {
            uint majorVersion = (uint)(version.Major >= 4 ? 4 : 2);
            uint minorVersion = 0;
            int hr = site.UpdateScriptMaps(siteRelUrl, majorVersion, minorVersion, isUpgrade);
            if (ErrorHandler.Failed(hr))
            {
                string strVersion = version.Major.ToString(CultureInfo.InvariantCulture) + "." + version.Minor.ToString(CultureInfo.InvariantCulture);
                if (hr == ErrorCodes.DIRPRJ_E_ASPNETNOTREGISTERED)
                {
                    _userNotificationServices.ShowError(string.Format(WebResources.Project_AspNetNotRegistered, strVersion));
                }
                else
                {
                    _userNotificationServices.ShowError(string.Format(WebResources.Project_ScriptmapFailure, siteRelUrl, strVersion, GetErrorInfo()));
                }
            }

            return hr;
        }

        public int SetDataDirectoryPermissions(string url)
        {
            int hr = HResult.OK;
            string appDataPath = Path.Combine(ProjectDirectory, "app_data");
            if (Directory.Exists(appDataPath))
            {
                hr = GetWebAppUpgrade().SetASPNETDataDirectoryPermissions(url, appDataPath);
            }

            return hr;
        }

        public int SeRoslynDirectoryPermissions(string url)
        {
            int hr = HResult.OK;
            string roslynFolder = Path.Combine(ProjectDirectory, @"bin\Roslyn");
            if (Directory.Exists(roslynFolder))
            {
                hr = GetWebAppUpgrade().SetDirectoryPermissions(url, roslynFolder, bIncludeWrite: false, bIncludeExecute: true);
            }

            return hr;
        }

        internal IVsWebAppUpgrade GetWebAppUpgrade()
        {
            if (!_webAppUpgradeInitalized)
            {
                _webAppUpgradeInitalized = true;
                _webAppUpgrade.Value.SetProjPathAndHierarchy(ProjectDirectory, _projectVsServices.VsHierarchy);
            }

            return _webAppUpgrade.Value;
        }

        private int GetIVsIISSite(WebLaunchSettings settings, out IVsIISSite? vsIISSite)
        {
            int hr = HResult.NoInterface;
            vsIISSite = null;

            // If using IIS Express get that service
            if (settings.ServerType == ServerType.IISExpress)
            {
                IVsIISExpressService? expSvc = GetIISExpressService(settings.UseGlobalAppHostCfgFile);
                hr = expSvc.OpenIISSite(settings.ServerUrls[0], out vsIISSite);
            }
            else if (settings.ServerType == ServerType.IIS)
            {
                hr = _localIISService.Value.OpenIISSite(settings.ServerUrls[0], false/*iisExpress*/, null, out vsIISSite);
            }

            return hr;
        }

        private IVsIISExpressService GetIISExpressService(bool usingGlobalAppHostCfg)
        {
            string? configFile = GetIISExpressAppHostConfigFile(usingGlobalAppHostCfg);

            int hr = LocalIISService2.GetIISExpressServiceForConfigFile(configFile, out IVsIISExpressService? iisExpressSvc);
            if (iisExpressSvc == null)
            {
                System.Diagnostics.Debug.Assert(ErrorHandler.Succeeded(hr), "Can't get IVsIISExpressService");
                throw new COMException(WebResources.Project_MissingComponents, HResult.Fail);
            }
            return iisExpressSvc;
        }

        /// <summary>
        /// Returns NULL if using the global per user one, otherwise the path to the one being used by the solution
        /// </summary>
        private string? GetIISExpressAppHostConfigFile(bool usingGlobalAppHostCfg)
        {
            string? configFile = null;
            if (!usingGlobalAppHostCfg)
            {

                LocalIISService2.GetDefaultConfigFileForSolution(out configFile);
                System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(configFile));
            }

            return configFile;
        }

        protected override Task InitializeCoreAsync(CancellationToken cancellationToken)
        {
            return ConfigureWebServerInternalAsync();
        }

        protected override async Task DisposeCoreAsync(bool initialized)
        {
            await _threadingService.SwitchToUIThread();

            ReleaseVsDeveloperWebServer();
            UnadviseIISServiceEvents();
        }

        public bool IISIsInstalled => ErrorHandler.Succeeded(_localIISService.Value.IsIISInstalled(out bool isInstalled)) && isInstalled;
        public bool IISExpressIsInstalled => ErrorHandler.Succeeded(_localIISService.Value.IsIISExpressInstalled(out bool isInstalled)) && isInstalled;

        /// <summary>
        /// Instantiates and returns the IIsExpress web server interface. It does NOT start it.
        /// </summary>
        private IVsDeveloperWebServer GetWebServerController()
        {
            if (_vsDeveloperWebServer == null)
            {
                if (ProjectUrl != null)
                {
                    IVsDeveloperWebServerSvc devWebServersService = GetVsDeveloperWebServerSvc();
                    int hr = devWebServersService.GetVsWebServer(WST_IISExpress, ProjectUrl, out IVsDeveloperWebServer devWebServer);
                    if (ErrorHandler.Succeeded(hr) && devWebServer != null)
                    {
                        _vsDeveloperWebServer = devWebServer;

                        // Need to remember this so that we close the server with the same one (in case the app host config file moves)
                        _devWebServersServiceUsedToGetWebServer = devWebServersService;

                        // If we haven't hooked into developer web server events, this is a good time to
                        // do it.
                        if (_vsWebServerEventCookie == 0)
                        {
                            devWebServersService.AdviseWebServerEvents(this, out _vsWebServerEventCookie);
                            System.Diagnostics.Debug.Assert(_vsWebServerEventCookie != 0);
                        }

                        // Make sure we are listening to IIS server events
                        AdviseIISServiceEvents();
                    }
                    else
                    {
                        throw new COMException(string.Format(WebResources.UnableToGetIISExpressWebServer, ProjectName), hr);
                    }
                }
                else
                {
                    throw new COMException(string.Format(WebResources.Project_MustSpecifyIISExpressUrl, ProjectName), HResult.Fail);
                }
            }

            return _vsDeveloperWebServer;
        }

        /// <summary>
        /// Utility to free up IISExpress server properly
        /// </summary>
        private void ReleaseVsDeveloperWebServer()
        {
            if (_vsDeveloperWebServer != null)
            {
                IVsDeveloperWebServerSvc devWebServersService = _devWebServersServiceUsedToGetWebServer ?? GetVsDeveloperWebServerSvc();
                if (devWebServersService != null)
                {
                    devWebServersService.CloseVsWebServer(_vsDeveloperWebServer);
                    if (_vsWebServerEventCookie != 0)
                    {
                        devWebServersService.UnadviseWebServerEvents(_vsWebServerEventCookie);
                        _vsWebServerEventCookie = 0;
                    }
                }

                _vsDeveloperWebServer = null;
                _devWebServersServiceUsedToGetWebServer = null;
            }
        }

        /// <summary>
        /// Helper to get the developer web server service to the solutions applicationhost.config file
        /// </summary>
        private IVsDeveloperWebServerSvc GetVsDeveloperWebServerSvc()
        {
            _webServerProviderSvc.Value.GetIVsDeveloperWebServerSvcForConfigFile(
                GetSolutionLevelApplicationHostConfigPath(), out IVsDeveloperWebServerSvc webServerSvc);

            if (webServerSvc == null)
            {
                throw new COMException(string.Format(WebResources.UnableToGetIISExpressWebServer, ProjectName), HResult.Fail);
            }

            return webServerSvc;
        }

        /// <summary>
        /// IVsDeveloperWebServerSvcEvents
        /// This method is called when a developer web server becomes invalid. Call close on the 
        /// web server which will force getting a new one
        /// </summary>
        public int OnDeveloperWebServerInvalid(IVsDeveloperWebServer pWebServer)
        {
            if (pWebServer == _vsDeveloperWebServer)
            {
                ReleaseVsDeveloperWebServer();
            }

            return HResult.OK;
        }

        public string GetSolutionLevelApplicationHostConfigPath()
        {
            if (ErrorHandler.Succeeded(LocalIISService2.GetDefaultConfigFileForSolution(out var configFile)))
            {
                return configFile;
            }

            throw new COMException(WebResources.CantGetApplcationHostConfigFile, HResult.Unexpected);

        }

        /// <summary>
        /// Hooks into IIS events to detect when the applicationhost.config file changes. This can be called many times. It is
        /// ignored if already listening
        /// </summary>
        private void AdviseIISServiceEvents()
        {
            if (_iisServiceEventsCookie == 0)
            {
                LocalIISService3.AdviseIISServiceEvents(this, out _iisServiceEventsCookie);
            }
        }

        /// <summary>
        /// Unhooks from IIS service events
        /// </summary>
        private void UnadviseIISServiceEvents()
        {
            if (_iisServiceEventsCookie != 0)
            {
                LocalIISService3.UnadviseIISServiceEvents(_iisServiceEventsCookie);
                _iisServiceEventsCookie = 0;
            }
        }

        /// <summary>
        /// Called just prior to the applicationhost.config file path changing. We must close any web server, so
        /// that it forces a re-get of these on the next run.
        /// </summary>
        public void BeforeMoveApplicationHostConfigFile()
        {
            ReleaseVsDeveloperWebServer();
        }

        /// <summary>
        /// Note that in a couple of scenarios, the Before function is not called first so we
        /// also run the cleanup here.
        /// </summary>
        public void AfterMoveApplicationHostConfigFile()
        {
            ReleaseVsDeveloperWebServer();
        }

        private string GetErrorInfo()
        {
            _uiShell.Value.GetErrorInfo(out string errText);

            // Sometimes the shell returns trailing nulls in the string. These need to be removed
            return errText == null ? string.Empty : errText.TrimEnd('\0');
        }

        private void SetErrorInfo(int hr, string description)
        {
            _uiShell.Value.SetErrorInfo(hr, description, 0, null, string.Empty);
        }

        private bool VSIsRunningElevated()
        {
            IVsShell3 vsShell = (IVsShell3)_vsShell.Value;

            vsShell.IsRunningElevated(out bool bIsElevated);
            return bIsElevated;
        }
        private static bool DirectoriesMatch(string dir1, string dir2)
        {
            return string.Compare(EnsureTrailingChar(dir1, '\\'), EnsureTrailingChar(dir2, '\\'), StringComparison.OrdinalIgnoreCase) == 0;
        }

        public static bool DirectoryIsEmpty(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                foreach (string item in Directory.EnumerateFileSystemEntries(folderPath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    if (item != "." && item != "..")
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static string EnsureTrailingChar(string s, char ch)
        {
            return s.Length == 0 || s[s.Length - 1] != ch ? s + ch : s;
        }

        private static void SplitUrl(string url, out string server, out string relUrl)
        {
            Uri uri = new Uri(url);
            relUrl = uri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
            if (!relUrl.StartsWith("/"))
            {
                relUrl = relUrl.Insert(0, "/");
            }

            server = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped);
        }

        private static string GetDeveloperEnvironement()
        {
            return GetFullEnvironmentAsString(new KeyValuePair<string, string>[] {new KeyValuePair<string, string>("DEV_ENVIRONMENT", "1")});
        }

        private static string GetFullEnvironmentAsString(IEnumerable<KeyValuePair<string, string>> envValuesToAdd)
        {
            // First add in the system env variables if requests
            StringBuilder newEnvironmentValuesString = new StringBuilder();

            // The IDictionary returned by the framework is case sensitive so we can't just add to the one
            // they return. Instead, we set all the environment varaibles (and track any existing values), get the updated environment
            // and then put the original values back. We store null or entries not found.
            Dictionary<string, string> envValuesToReset = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, string value) in envValuesToAdd)
            {
                // Will be null if not found
                string existing = Environment.GetEnvironmentVariable(key);
                envValuesToReset[key] = existing;
                Environment.SetEnvironmentVariable(key, value);
            }

            // Get the updated env and convert to string - this will be returned by the method.
            foreach (DictionaryEntry dictEntry in Environment.GetEnvironmentVariables())
            {
                if (dictEntry.Value != null)
                {
                    newEnvironmentValuesString.Append(dictEntry.Key.ToString() + "=" + dictEntry.Value.ToString());
                    newEnvironmentValuesString.Append('\0');
                }
            }

            // Restore the environment - this is so that current process is not impacted.
            foreach ((string key, string value) in envValuesToReset)
            {
                // Items that weren't there will be set back to null which removes entry
                Environment.SetEnvironmentVariable(key, value);
            }

            return newEnvironmentValuesString.ToString();
        }
    }
}
