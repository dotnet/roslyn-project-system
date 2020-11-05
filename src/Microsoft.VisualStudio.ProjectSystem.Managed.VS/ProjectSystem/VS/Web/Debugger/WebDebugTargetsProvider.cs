// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.IO;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Web.Application;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    /// <summary>
    /// Provides QueryDebugTargetsAsync() support for web profile iisexpress, web command, IIS. It is 
    /// not an exported CPS debugger but is imported by ProjectDebuggerProvider to handle IIS
    /// and IIS Express web servers.
    /// </summary>
    [Export(typeof(IDebugProfileLaunchTargetsProvider))]
    [AppliesTo(ProjectCapability.AspNetLaunchProfiles)]
    [Order(100)]
    internal class WebDebugTargetsProvider : IDebugProfileLaunchTargetsProvider, IDebugProfileLaunchTargetsProvider2, IDeployedProjectItemMappingProvider, IDisposable
    {
        private readonly ConfiguredProject _configuredProject;
        private readonly IDebugTokenReplacer _tokenReplacer;
        private readonly IUnconfiguredProjectVsServices _projectVsServices;
        private readonly IProjectThreadingService _threadingService;
        private readonly IFileSystem _fileSystem;
        private readonly IWebServer _webServer;
        private readonly IDebugTargetsObserver _debugTargetsObserver;
        private readonly IVsUIService<SVsShell, IVsShell> _vsShell;
        private readonly IVsUIService<SVsUIShellOpenDocument, IVsUIShellOpenDocument> _vsShellOpenDoc;
        private readonly IVsUIService<IBrowserDebugTargetSelectionService, IBrowserDebugTargetSelectionService> _browserDebugTargetSelectionSvc;

        [ImportingConstructor]
        public WebDebugTargetsProvider(ConfiguredProject configuredProject,
                                       IDebugTokenReplacer tokenReplacer,
                                       IUnconfiguredProjectVsServices projectVsServices,
                                       IProjectThreadingService threadingService,
                                       IFileSystem fileSystem,
                                       IWebServer webServer,
                                       IDebugTargetsObserver debugTargetsObserver,
                                       IVsUIService<SVsShell, IVsShell> vsShell,
                                       IVsUIService<SVsUIShellOpenDocument, IVsUIShellOpenDocument> vsShellOpenDoc,
                                       IVsUIService<IBrowserDebugTargetSelectionService, IBrowserDebugTargetSelectionService> browserDebugTargetSelectionSvc)
        {
            _configuredProject = configuredProject;
            _tokenReplacer = tokenReplacer;
            _projectVsServices = projectVsServices;
            _threadingService = threadingService;
            _fileSystem = fileSystem;
            _webServer = webServer;
            _debugTargetsObserver = debugTargetsObserver;
            _vsShell = vsShell;
            _vsShellOpenDoc = vsShellOpenDoc;
            _browserDebugTargetSelectionSvc = browserDebugTargetSelectionSvc;
        }

        /// <summary>
        /// This is set to the URL(s) we are going to debug. There could be more than one in the case of a site with a
        /// secure and non-secure port binding. This is important for mapping urls to project items and is handled by the ProjectDebugerProvider
        /// </summary>
        private List<string> DebuggingRootUrls { get; } = new List<string>();

        /// <summary>
        /// Supports the Project profile.
        /// </summary>
        public bool SupportsProfile(ILaunchProfile profile)
        {
            return string.Equals(profile.CommandName, LaunchSettingsProvider.RunProjectCommandName, StringComparisons.LaunchProfileCommandNames);
        }

        public Task OnBeforeLaunchAsync(DebugLaunchOptions launchOptions, ILaunchProfile profile)
        {
            return Task.CompletedTask;
        }

        public Task OnAfterLaunchAsync(DebugLaunchOptions launchOptions, ILaunchProfile profile)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(DebugLaunchOptions launchOptions, ILaunchProfile activeProfile)
        {
            return QueryDebugTargetsAsync(launchOptions, activeProfile, isQuery: true);
        }

        public Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsForDebugLaunchAsync(DebugLaunchOptions launchOptions, ILaunchProfile activeProfile)
        {
            return QueryDebugTargetsAsync(launchOptions, activeProfile, isQuery: false);
        }

        /// <summary>
        /// If isQuery is true, this is being called to just get the list of targets and not actually start any webservers
        /// </summary>
        private async Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(DebugLaunchOptions launchOptions, ILaunchProfile activeProfile, bool isQuery)
        {
            activeProfile = await _tokenReplacer.ReplaceTokensInProfileAsync(activeProfile);

            // Get all the server urls
            var serverUrls = _webServer.WebServerUrls;

            if (serverUrls.Count == 0)
            {
                throw new COMException(WebResources.NoServerURLsConfigured, HResult.Fail);
            }

            var launchTargets = new List<DebugLaunchSettings>();

            // For queries we don't want to start anything
            if (isQuery)
            {
                // Only profiling and debugging involve adding a launch target. 
                if (launchOptions.IsDebugging() || launchOptions.IsProfiling())
                {
                    if (_webServer.ActiveWebServerType == ServerType.IISExpress)
                    {
                        launchTargets.Add(await GetIISExpressLaunchTargetAsync(launchOptions, isQuery));
                    }
                }
            }
            else
            {
                // Start the server (throws on failure) and make sure it listening
                await _webServer.StartAsync(launchOptions.ToWebServerStartOption());
                await _webServer.WaitForListeningAsync();

                // If we aren't debugging or profiling, we don't create server settings option. If we do so, the debugger will ignore the AlreadyRunning and
                // start the proces anyway
                if (launchOptions.IsDebugging() || launchOptions.IsProfiling())
                {
                    if (_webServer.ActiveWebServerType == ServerType.IISExpress)
                    {
                        launchTargets.Add(await GetIISExpressLaunchTargetAsync(launchOptions, isQuery));
                    }
                    else if (_webServer.ActiveWebServerType == ServerType.IIS)
                    {
                        launchTargets.Add(GetIISLaunchTarget(launchOptions, serverUrls[0]));
                    }
                }
            }

            // Remember the server urls so we can map urls to paths correctly
            foreach (string url in serverUrls)
            {
                if (url.EndsWith("/", StringComparison.Ordinal))
                {
                    DebuggingRootUrls.Add(url);
                }
                else
                {
                    DebuggingRootUrls.Add(url + "/");
                }
            }

            var browserTargets = await GetAllBrowserLaunchTargetsAsync(serverUrls[0], launchOptions, isQuery);

            if (!isQuery && launchOptions.IsDebugging() && browserTargets.Count > 0)
            {
                _debugTargetsObserver.ObserveDebugTarget(_projectVsServices.Project.FullPath, browserTargets[0].Executable);
            }

            launchTargets.AddRange(browserTargets);

            return launchTargets.ToArray();
        }

        private DebugLaunchSettings GetIISLaunchTarget(DebugLaunchOptions launchOptions, string browserUrl)
        {
            // To get the debugger to attach we need to specify a url with an aspx type extension
            string ext = Path.GetExtension(browserUrl);
            if (ext.Length == 0 || (
                !browserUrl.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase) &&
                !browserUrl.EndsWith(".svc", StringComparison.OrdinalIgnoreCase) &&
                !browserUrl.EndsWith(".asmx", StringComparison.OrdinalIgnoreCase) &&
                !browserUrl.EndsWith(".xamlx", StringComparison.OrdinalIgnoreCase)))
            {
                int index = browserUrl.LastIndexOf('/');
                browserUrl = browserUrl.Remove(index);
                browserUrl += "/debugattach.aspx";
            }

            var serverSettings = new DebugLaunchSettings(launchOptions)
            {
                Project = _projectVsServices.VsHierarchy,
                LaunchOperation = DebugLaunchOperation.AlreadyRunning,
                Executable = browserUrl,
                RemoteMachine = new Uri(browserUrl).Host,
                LaunchDebugEngineGuid = DebuggerEngines.ManagedOnlyEngine,
                LaunchOptions = launchOptions |
                                            DebugLaunchOptions.WaitForAttachComplete |
                                            DebugLaunchOptions.StopDebuggingOnEnd
            };

            return serverSettings;
        }

        private async Task<DebugLaunchSettings> GetIISExpressLaunchTargetAsync(DebugLaunchOptions launchOptions, bool isQuery)
        {
            var launchData = await _webServer.GetWebServerCommandLineAsync();
            int processID = isQuery ? 0 : await _webServer.GetWebServerProcessIdAsync();
            return new DebugLaunchSettings(launchOptions)
            {
                LaunchOperation = DebugLaunchOperation.AlreadyRunning,
                Project = _projectVsServices.VsHierarchy,
                Executable = launchData.exePath,
                Arguments = launchData.commandLine,
                ProcessId = processID,
                LaunchDebugEngineGuid = DebuggerEngines.ManagedOnlyEngine,
                LaunchOptions = launchOptions |
                                DebugLaunchOptions.WaitForAttachComplete |
                                DebugLaunchOptions.StopDebuggingOnEnd
            };
        }

        public async Task<List<DebugLaunchSettings>> GetAllBrowserLaunchTargetsAsync(string debuggingUrl, DebugLaunchOptions launchOptions, bool isQuery)
        {
            await _threadingService.SwitchToUIThread();

            // Now do browser settings.
            var launchTargets = new List<DebugLaunchSettings>();

            // Figure which browser to use. 
            // If debugging we use the debug target selection svc to figure out which one to use
            if (launchOptions.IsDebugging())
            {
                (string browserPath, string? browserArgs) = GetDefaultBrowserForDebug();
                launchTargets.Add(GetBrowserLaunchSettings(browserPath, browserArgs, debuggingUrl, launchOptions));
            }
            else
            {
                // For ctrl-F5, we launch all default browsers 
                // So get list of selected browsers. The Tuple contains browser path, browser args
                List<(string browserPath, string? browserArgs)> selectedBrowsers = GetDefaultBrowsers();
                foreach ((string browserPath, string? browserArgs) in selectedBrowsers)
                {
                    launchTargets.Add(GetBrowserLaunchSettings(browserPath, browserArgs, debuggingUrl, launchOptions));
                }
            }

            return launchTargets;
        }

        private DebugLaunchSettings GetBrowserLaunchSettings(string browserPath, string? browserArgs, string url, DebugLaunchOptions launchOptions)
        {
            var browserSettings = new DebugLaunchSettings(launchOptions)
            {
                Project = _projectVsServices.VsHierarchy
            };

            // Remove profiling option 
            launchOptions &= ~DebugLaunchOptions.Profiling;

            // if script debugging is disabled and we are debugging, add in the command line parameters (if supported) to launch a new window.
            if (launchOptions.IsDebugging() && DebugTargetObserverIsEnabled())
            {
                _debugTargetsObserver.GetNewWindowCommandLineArgument(browserPath, out string? newWindowArgs);
                if (newWindowArgs != null)
                {
                    if (browserArgs == null)
                    {
                        browserArgs = newWindowArgs;
                    }
                    else
                    {
                        browserArgs = newWindowArgs + browserArgs;
                    }
                }
            }

            browserSettings.Options = "*"; // We set the options here, because they might be overriden by the Chrome/PineZorro debugger

            // We must encode URL's for non-IE browsers.
            var encodedUrl = url;
            if (!string.Equals(Path.GetFileName(browserPath), "iexplorer.exe", StringComparison.OrdinalIgnoreCase))
            {
                encodedUrl = SafeGetEncodedUrl(url);
            }

            if (string.IsNullOrEmpty(browserArgs))
            {
                browserArgs = encodedUrl;
            }
            else
            {
                browserArgs = browserArgs + " " + encodedUrl;
            }

            browserSettings.LaunchOperation = DebugLaunchOperation.CreateProcess;
            browserSettings.Executable = browserPath;
            browserSettings.Arguments = browserArgs;
            browserSettings.LaunchOptions = launchOptions | DebugLaunchOptions.NoDebug;

            return browserSettings;
        }

        private List<(string browserPath, string? browserArgs)> GetDefaultBrowsers()
        {
            // Create the list of default browsers
            var browserList = new List<(string browserPath, string? browserArgs)>();
            var doc3 = (IVsUIShellOpenDocument3)_vsShellOpenDoc.Value;

            IVsEnumDocumentPreviewers previewersEnum = doc3.DocumentPreviewersEnum;

            IVsDocumentPreviewer[] rgPreviewers = new IVsDocumentPreviewer[1];
            while (previewersEnum.Next(1, rgPreviewers, out uint celtFetched) == HResult.OK && celtFetched == 1)
            {
                // Need to filter out the internal browser (no path)
                if (rgPreviewers[0].IsDefault && !string.IsNullOrEmpty(rgPreviewers[0].Path))
                {
                    browserList.Add((UnQuotePath(rgPreviewers[0].Path), rgPreviewers[0].Arguments));
                }
            }

            return browserList;
        }

        public (string browserPath, string? browserArgs) GetDefaultBrowserForDebug()
        {
            var hr = _browserDebugTargetSelectionSvc.Value.SelectDefaultBrowser(out IVsDocumentPreviewer selectedBrowser);
            if (ErrorHandler.Succeeded(hr))
            {
                var browserPath = selectedBrowser.Path;
                if (!string.IsNullOrEmpty(browserPath))
                {
                    // Remove quotes.
                    if (browserPath.StartsWith("\"", StringComparison.Ordinal) && browserPath.EndsWith("\"", StringComparison.Ordinal))
                    {
                        browserPath = browserPath.Substring(1, browserPath.Length - 2);
                    }

                    return (UnQuotePath(selectedBrowser.Path), selectedBrowser.Arguments);
                }
            }
            else if (hr == HResult.Abort)
            {
                // User cancel
                throw new OperationCanceledException(string.Empty);
            }

            // Failure case just default to IE
            throw new COMException(WebResources.NeedToSelectANonInternalBrowser, HResult.Fail);
        }

        /// <summary>
        /// IDeployedProjectItemMappingProvider
        /// Implementd so that we can map URL's back to local file item paths. ProjectDebuggerProvider will forward mapping requests
        /// from CPS to us if we are the active launch provider
        /// 
        /// Note that this is called via IsDocumentInProject() so there is an expectation that this function only returns success
        /// for file items. 
        /// </summary>
        public bool TryGetProjectItemPathFromDeployedPath(string deployedPath, out string? localPath)
        {
            localPath = null;
            foreach (var url in DebuggingRootUrls)
            {
                System.Diagnostics.Debug.Assert(url.EndsWith("/", StringComparison.OrdinalIgnoreCase));
                if (deployedPath.StartsWith(url, StringComparison.OrdinalIgnoreCase))
                {
                    // Figure out the part of the url after the projectUrl and append it to our path
                    // We need to remove any query string or bookmark so leverage the uri class to extract everything up to and including the path, 
                    // subtract the application url, and combine what is left with the web root path.
                    var uri = new Uri(deployedPath);
                    var relPath = uri.GetLeftPart(UriPartial.Path).Substring(url.Length).Replace('/', '\\');
                    if (relPath.StartsWith("\\", StringComparison.OrdinalIgnoreCase))
                    {
                        relPath = relPath.Substring(1);
                    }

                    localPath = Path.Combine(Path.GetDirectoryName(_projectVsServices.Project.FullPath), relPath);
                    return _fileSystem.FileExists(localPath);
                }
            }

            // Return false to allow normal processing
            return false;
        }

        public void Dispose()
        {
        }

        private static string UnQuotePath(string path)
        {
            if (path.StartsWith("\"", StringComparison.Ordinal) && path.EndsWith("\"", StringComparison.Ordinal))
            {
                return path.Substring(1, path.Length - 2);
            }

            return path;
        }

        private static string SafeGetEncodedUrl(string url)
        {
            try
            {
                var encodedUri = new Uri(url);
                return encodedUri.AbsoluteUri;
            }
            catch (UriFormatException)
            {
            }

            return url;
        }

        private bool DebugTargetObserverIsEnabled()
        {
            _vsShell.Value.GetProperty((int)__VSSPROPID.VSSPROPID_VirtualRegistryRoot, out object objProp);
            if (objProp is string regRoot)
            {
                string regKey = "HKEY_CURRENT_USER\\" + regRoot + "\\WebProjects";
                object objValue = Registry.GetValue(regKey, "EnableDebugTargetsObserver", 1);
                return objValue is int intVal && intVal == 1;
            }

            return true;
        }
    }
}
