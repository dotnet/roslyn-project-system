// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.VisualStudio.IO;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.ProjectSystem.References;
using Microsoft.VisualStudio.ProjectSystem.VS.Interop;
using Microsoft.VisualStudio.ProjectSystem.VS.UI;
using Microsoft.VisualStudio.ProjectSystem.VS.Utilities;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Setup.Configuration;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.ProjectSystem.VS
{
    /// <inheritdoc cref="IDotNetCoreProjectCompatibilityDetector"/>
    [Export(typeof(IDotNetCoreProjectCompatibilityDetector))]
    internal partial class DotNetCoreProjectCompatibilityDetector : IDotNetCoreProjectCompatibilityDetector, IVsSolutionEvents, IVsSolutionLoadEvents, IDisposable
    {
        private const string SupportedLearnMoreFwlink = "https://go.microsoft.com/fwlink/?linkid=868064";
        private const string UnsupportedLearnMoreFwlink = "https://go.microsoft.com/fwlink/?linkid=866797";
        private const string SuppressDotNewCoreWarningKey = @"ManagedProjectSystem\SuppressDotNewCoreWarning";
        private const string UsePreviewSdkSettingKey = @"ManagedProjectSystem\UsePreviewSdk";
        private const string VersionCompatibilityDownloadFwlink = "https://go.microsoft.com/fwlink/?linkid=866798";
        private const string VersionDataFilename = "DotNetVersionCompatibility.json";
        private const int CacheFileValidHours = 24;

        private readonly Lazy<IProjectServiceAccessor> _projectServiceAccessor;
        private readonly Lazy<IDialogServices> _dialogServices;
        private readonly Lazy<IProjectThreadingService> _threadHandling;
        private readonly Lazy<IVsShellUtilitiesHelper> _shellUtilitiesHelper;
        private readonly Lazy<IFileSystem> _fileSystem;
        private readonly Lazy<IHttpClient> _httpClient;
        private readonly IVsService<ISettingsManager> _settingsManagerService;
        private readonly IVsService<IVsUIShell> _vsUIShellService;
        private readonly IVsService<IVsSolution> _vsSolutionService;
        private readonly IVsService<IVsAppId> _vsAppIdService;
        private readonly IVsService<IVsShell> _vsShellService;
        private readonly IUnconfiguredProjectCommonServices _commonServices;
        private RemoteCacheFile? _versionDataCacheFile;
        private uint _solutionCookie = VSConstants.VSCOOKIE_NIL;
        private DateTime _timeCurVersionDataLastUpdatedUtc = DateTime.MinValue; // Tracks how often we need to look for new data
        private IVsSolution? _vsSolution;

        // These are internal for unit testing
        internal bool SolutionOpen { get; private set; }
        internal Version? VisualStudioVersion { get; private set; }
        internal CompatibilityLevel CompatibilityLevelWarnedForCurrentSolution { get; set; } = CompatibilityLevel.Recommended;
        internal VersionCompatibilityData? CurrentVersionCompatibilityData { get; private set; }

        [ImportingConstructor]
        public DotNetCoreProjectCompatibilityDetector(Lazy<IProjectServiceAccessor> projectAccessor,
                                                      Lazy<IDialogServices> dialogServices,
                                                      Lazy<IProjectThreadingService> threadHandling,
                                                      Lazy<IVsShellUtilitiesHelper> vsShellUtilitiesHelper,
                                                      Lazy<IFileSystem> fileSystem, Lazy<IHttpClient> httpClient,
                                                      IVsService<SVsUIShell, IVsUIShell> vsUIShellService,
                                                      IVsService<SVsSettingsPersistenceManager, ISettingsManager> settingsManagerService,
                                                      IVsService<SVsSolution, IVsSolution> vsSolutionService,
                                                      IVsService<SVsAppId, IVsAppId> vsAppIdService,
                                                      IVsService<SVsShell, IVsShell> vsShellService,
                                                      IUnconfiguredProjectCommonServices commonServices)
        {
            _projectServiceAccessor = projectAccessor;
            _dialogServices = dialogServices;
            _threadHandling = threadHandling;
            _shellUtilitiesHelper = vsShellUtilitiesHelper;
            _fileSystem = fileSystem;
            _httpClient = httpClient;
            _vsUIShellService = vsUIShellService;
            _settingsManagerService = settingsManagerService;
            _vsSolutionService = vsSolutionService;
            _vsAppIdService = vsAppIdService;
            _vsShellService = vsShellService;
            _commonServices = commonServices;
        }

        public async Task InitializeAsync()
        {
            await _threadHandling.Value.SwitchToUIThread();

            // Initialize our cache file
            string? appDataFolder = await _shellUtilitiesHelper.Value.GetLocalAppDataFolderAsync(_vsShellService);
            if (appDataFolder != null)
            {
                _versionDataCacheFile = new RemoteCacheFile(Path.Combine(appDataFolder, VersionDataFilename), VersionCompatibilityDownloadFwlink,
                                                            TimeSpan.FromHours(CacheFileValidHours), _fileSystem.Value, _httpClient);
            }

            VisualStudioVersion = await _shellUtilitiesHelper.Value.GetVSVersionAsync(_vsAppIdService);

            _vsSolution = await _vsSolutionService.GetValueAsync();

            Verify.HResult(_vsSolution.AdviseSolutionEvents(this, out _solutionCookie));

            // Check to see if a solution is already open. If so we set _solutionOpened to true so that subsequent projects added to 
            // this solution are processed.
            if (ErrorHandler.Succeeded(_vsSolution.GetProperty((int)__VSPROPID4.VSPROPID_IsSolutionFullyLoaded, out object isFullyLoaded)) &&
                isFullyLoaded is bool isFullyLoadedBool &&
                isFullyLoadedBool)
            {
                SolutionOpen = true;
                // do not block package initialization on this
                _threadHandling.Value.RunAndForget(async () =>
                {
                    // First make sure that the cache file exists
                    if (_versionDataCacheFile != null && _versionDataCacheFile.ReadCacheFile() is null)
                    {
                        await _versionDataCacheFile.TryToUpdateCacheFileAsync();
                    }

                    // check if the project is compatible
                    await CheckCompatibilityAsync();
                }, unconfiguredProject: null);
            }
        }

        public void Dispose()
        {
            _threadHandling.Value.VerifyOnUIThread();

            if (_solutionCookie != VSConstants.VSCOOKIE_NIL)
            {
                if (_vsSolution != null)
                {
                    Verify.HResult(_vsSolution.UnadviseSolutionEvents(_solutionCookie));
                    _solutionCookie = VSConstants.VSCOOKIE_NIL;
                    _vsSolution = null;
                }
            }
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            // Only check this project if the solution is opened and we haven't already warned at the maximum level. Note that fAdded
            // is true for both add and a reload of an unloaded project
            if (SolutionOpen && fAdded == 1 && CompatibilityLevelWarnedForCurrentSolution != CompatibilityLevel.NotSupported)
            {
                UnconfiguredProject? project = pHierarchy.AsUnconfiguredProject();
                if (project != null)
                {
                    _threadHandling.Value.RunAndForget(async () =>
                    {
                        // Run on the background
                        await TaskScheduler.Default;

                        VersionCompatibilityData compatData = GetVersionCompatibilityData();

                        // We need to check if this project has been newly created. Our projects will implement IProjectCreationState -we can 
                        // skip any that don't
                        if (IsNewlyCreated(project))
                        {
                            bool isPreviewSDKInUse = await IsPreviewSDKInUseAsync();
                            CompatibilityLevel compatLevel = await GetProjectCompatibilityAsync(project, compatData, isPreviewSDKInUse);
                            if (compatLevel != CompatibilityLevel.Recommended)
                            {
                                await WarnUserOfIncompatibleProjectAsync(compatLevel, compatData, isPreviewSDKInUse);
                            }
                        }
                    }, unconfiguredProject: null);
                }
            }

            return HResult.OK;
        }

        // This method is overridden in test code
        protected virtual async Task<bool> IsPreviewSDKInUseAsync()
        {
            if (await IsPrereleaseAsync())
            {
                return true;
            }

            if (_settingsManagerService == null)
            {
                return false;
            }

            ISettingsManager settings = await _settingsManagerService.GetValueAsync();

            return settings.GetValueOrDefault<bool>(UsePreviewSdkSettingKey);
        }

        private async Task<bool> IsPrereleaseAsync()
        {
            await _threadHandling.Value.SwitchToUIThread();
            ISetupConfiguration setupConfiguration = new SetupConfiguration();
            ISetupInstance setupInstance = setupConfiguration.GetInstanceForCurrentProcess();
            // NOTE: this explicit cast is necessary for the subsequent COM QI to succeed. 
            var setupInstanceCatalog = (ISetupInstanceCatalog)setupInstance;
            return setupInstanceCatalog.IsPrerelease();
        }

        // This method is overridden in test code
        protected virtual bool IsNewlyCreated(UnconfiguredProject project)
        {
            IProjectCreationState? projectCreationState = project.Services.ExportProvider.GetExportedValueOrDefault<IProjectCreationState>();
            return projectCreationState?.WasNewlyCreated ?? false;
        }

        public int OnAfterCloseSolution(object? pUnkReserved)
        {
            // Clear state flags
            CompatibilityLevelWarnedForCurrentSolution = CompatibilityLevel.Recommended;
            SolutionOpen = false;
            return HResult.OK;
        }

        private async Task CheckCompatibilityAsync()
        {
            // Run on the background
            await TaskScheduler.Default;

            VersionCompatibilityData compatDataToUse = GetVersionCompatibilityData();
            CompatibilityLevel finalCompatLevel = CompatibilityLevel.Recommended;
            IProjectService projectService = _projectServiceAccessor.Value.GetProjectService();
            IEnumerable<UnconfiguredProject> projects = projectService.LoadedUnconfiguredProjects;
            bool isPreviewSDKInUse = await IsPreviewSDKInUseAsync();
            foreach (UnconfiguredProject project in projects)
            {
                ConfiguredProject? configuredProject = await project.GetSuggestedConfiguredProjectAsync();
                if (_commonServices.CacheApplicable && configuredProject?.ProjectVersion.CompareTo(DataflowUtilities.CacheModeVersion) != 0)
                {
                    continue;
                }

                // Track the most severe compatibility level if not in cached read mode
                CompatibilityLevel compatLevel = await GetProjectCompatibilityAsync(project, compatDataToUse, isPreviewSDKInUse);
                if (compatLevel != CompatibilityLevel.Recommended && compatLevel > finalCompatLevel)
                {
                    finalCompatLevel = compatLevel;
                }
            }

            if (finalCompatLevel != CompatibilityLevel.Recommended)
            {
                // Warn the user.
                await WarnUserOfIncompatibleProjectAsync(finalCompatLevel, compatDataToUse, isPreviewSDKInUse);
            }

            // Used so we know when to process newly added projects
            SolutionOpen = true;
        }

        /// <summary>
        /// Fired when the solution load process is fully complete, including all background loading
        /// of projects. This event always fires after the initial opening of a solution
        /// </summary>
        public int OnAfterBackgroundSolutionLoadComplete()
        {
            // Schedule this to run on idle
            _threadHandling.Value.RunAndForget(() => CheckCompatibilityAsync(), unconfiguredProject: null);
            return HResult.OK;
        }

        private async Task WarnUserOfIncompatibleProjectAsync(CompatibilityLevel compatLevel, VersionCompatibilityData compatData, bool isPreviewSDKInUse)
        {
            if (!_threadHandling.Value.IsOnMainThread)
            {
                await _threadHandling.Value.SwitchToUIThread();
            }

            // Check if already warned - this could happen in the off chance two projects are added very quickly since the detection work is 
            // scheduled on idle.
            if (CompatibilityLevelWarnedForCurrentSolution < compatLevel)
            {
                // Only want to warn once per solution
                CompatibilityLevelWarnedForCurrentSolution = compatLevel;

                IVsUIShell uiShell = await _vsUIShellService.GetValueAsync();
                uiShell.GetAppName(out string caption);

                if (compatLevel == CompatibilityLevel.Supported)
                {
                    // Get current dontShowAgain value
                    ISettingsManager settingsManager = await _settingsManagerService.GetValueAsync();
                    bool suppressPrompt = settingsManager.GetValueOrDefault(SuppressDotNewCoreWarningKey, defaultValue: false);

                    if (compatData.OpenSupportedPreviewMessage is null && isPreviewSDKInUse)
                    {
                        // There is no message to show the user in this case so we return
                        return;
                    }

                    if (!suppressPrompt)
                    {
                        string msg;
                        if (compatData.OpenSupportedPreviewMessage is object && isPreviewSDKInUse)
                        {
                            msg = string.Format(compatData.OpenSupportedPreviewMessage, compatData.SupportedVersion!.Major, compatData.SupportedVersion.Minor);
                        }
                        else
                        {
                            msg = string.Format(compatData.OpenSupportedMessage, compatData.SupportedVersion!.Major, compatData.SupportedVersion.Minor);
                        }

                        suppressPrompt = _dialogServices.Value.DontShowAgainMessageBox(caption, msg, VSResources.DontShowAgain, false, VSResources.LearnMore, SupportedLearnMoreFwlink);
                        if (suppressPrompt && settingsManager != null)
                        {
                            await settingsManager.SetValueAsync(SuppressDotNewCoreWarningKey, suppressPrompt, isMachineLocal: true);
                        }
                    }
                }
                else
                {
                    string msg;
                    if (compatData.UnsupportedVersion != null)
                    {
                        msg = string.Format(compatData.OpenUnsupportedMessage, compatData.UnsupportedVersion.Major, compatData.UnsupportedVersion.Minor);
                    }
                    else
                    {
                        msg = string.Format(compatData.OpenUnsupportedMessage, compatData.SupportedVersion!.Major, compatData.SupportedVersion.Minor);
                    }

                    _dialogServices.Value.DontShowAgainMessageBox(caption, msg, null, false, VSResources.LearnMore, UnsupportedLearnMoreFwlink);
                }
            }
        }

        private static async Task<CompatibilityLevel> GetProjectCompatibilityAsync(UnconfiguredProject project, VersionCompatibilityData compatData, bool isPreviewSDKInUse)
        {
            if (project.Capabilities.AppliesTo($"{ProjectCapability.DotNet} & {ProjectCapability.PackageReferences}"))
            {
                Assumes.Present(project.Services.ActiveConfiguredProjectProvider);
                ConfiguredProject? activeConfiguredProject = project.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject;
                Assumes.NotNull(activeConfiguredProject);
                Assumes.Present(activeConfiguredProject.Services.ProjectPropertiesProvider);
                IProjectProperties properties = activeConfiguredProject.Services.ProjectPropertiesProvider.GetCommonProperties();
                string tfm = await properties.GetEvaluatedPropertyValueAsync("TargetFrameworkMoniker");
                if (!string.IsNullOrEmpty(tfm))
                {
                    var fw = new FrameworkName(tfm);
                    if (fw.Identifier.Equals(".NETCoreApp", StringComparisons.FrameworkIdentifiers))
                    {
                        return GetCompatibilityLevelFromVersion(fw.Version, compatData, isPreviewSDKInUse);
                    }
                    else if (fw.Identifier.Equals(".NETFramework", StringComparisons.FrameworkIdentifiers))
                    {
                        // The interesting case here is Asp.Net Core on full framework
                        Assumes.Present(activeConfiguredProject.Services.PackageReferences);
                        IImmutableSet<IUnresolvedPackageReference> pkgReferences = await activeConfiguredProject.Services.PackageReferences.GetUnresolvedReferencesAsync();

                        // Look through the package references
                        foreach (IUnresolvedPackageReference pkgRef in pkgReferences)
                        {
                            if (string.Equals(pkgRef.EvaluatedInclude, "Microsoft.AspNetCore.All", StringComparisons.ItemNames) ||
                                string.Equals(pkgRef.EvaluatedInclude, "Microsoft.AspNetCore", StringComparisons.ItemNames))
                            {
                                string verString = await pkgRef.Metadata.GetEvaluatedPropertyValueAsync("Version");
                                if (!string.IsNullOrWhiteSpace(verString))
                                {
                                    // This is a semantic version string. We only care about the non-semantic version part
                                    int index = verString.IndexOfAny(Delimiter.PlusAndMinus);
                                    if (index != -1)
                                    {
                                        verString = verString.Substring(0, index);
                                    }

                                    if (Version.TryParse(verString, out Version aspnetVersion))
                                    {
                                        return GetCompatibilityLevelFromVersion(aspnetVersion, compatData, isPreviewSDKInUse);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return CompatibilityLevel.Recommended;
        }

        /// <summary>
        /// Compares the passed in version to the compatibility data to determine the compat level
        /// </summary>
        private static CompatibilityLevel GetCompatibilityLevelFromVersion(Version version, VersionCompatibilityData compatData, bool isPreviewSDKInUse)
        {
            // Only compare major, minor. The presence of build with change the comparison. ie: 2.0 != 2.0.0
            if (version.Build != -1)
            {
                version = new Version(version.Major, version.Minor);
            }

            if (compatData.SupportedPreviewVersion is null &&
                compatData.SupportedVersion is null &&
                compatData.UnsupportedVersion is null)
            {
                // No restrictions
                return CompatibilityLevel.Recommended;
            }

            // Version is less than the supported preview version and the user wants to use preview SDKs
            if (compatData.SupportedPreviewVersion is object && isPreviewSDKInUse && version <= compatData.SupportedPreviewVersion)
            {
                return CompatibilityLevel.Recommended;
            }

            // A supported version exists and the version is less than the supported version
            if (compatData.SupportedVersion is object && version < compatData.SupportedVersion)
            {
                return CompatibilityLevel.Recommended;
            }

            // The version is not unsupported and exactly matches the supported version
            if (compatData.SupportedVersion is object && compatData.UnsupportedVersion is object &&
                version == compatData.SupportedVersion && version < compatData.UnsupportedVersion)
            {
                return CompatibilityLevel.Supported;
            }

            // Supported version is null or not recommended check unsupported version
            if (version < compatData.UnsupportedVersion)
            {
                return CompatibilityLevel.Recommended;
            }

            // Unsupported version is not recommended
            return CompatibilityLevel.NotSupported;
        }

        /// <summary>
        /// Pings the server to download version compatibility information and stores this in a cached file in the users app data. If the cached file is
        /// less than 24 hours old, it uses that data. Otherwise it downloads from the server. If the download fails it will use the previously cached
        /// file, or if that file doesn't not exist, it uses the data baked into this class
        /// </summary>
        private VersionCompatibilityData GetVersionCompatibilityData()
        {
            // Do we need to update our cached data? Note that since the download could take a long time like tens of seconds we don't really want to
            // start showing messages to the user well after their project is opened and they are interacting with it. Thus we start a task to update the 
            // file, so that the next time we come here, we have updated data.
            if (CurrentVersionCompatibilityData != null && _timeCurVersionDataLastUpdatedUtc.AddHours(CacheFileValidHours) > DateTime.UtcNow)
            {
                return CurrentVersionCompatibilityData;
            }

            try
            {
                // Try the cache file
                Dictionary<Version, VersionCompatibilityData>? versionCompatData = GetCompatibilityDataFromCacheFile();

                // See if the cache file needs refreshing and if so, kick off a task to do so
                if (_versionDataCacheFile?.CacheFileIsStale() == true)
                {
                    _ = _versionDataCacheFile.TryToUpdateCacheFileAsync(() =>
                    {
                        // Invalidate the in-memory cached data on success
                        _timeCurVersionDataLastUpdatedUtc = DateTime.MinValue;
                    });
                }

                if (versionCompatData != null && VisualStudioVersion != null)
                {
                    // First try to match exactly on our VS version and if that fails, match on just major, minor
                    if (versionCompatData.TryGetValue(VisualStudioVersion, out VersionCompatibilityData compatData) || versionCompatData.TryGetValue(new Version(VisualStudioVersion.Major, VisualStudioVersion.Minor), out compatData))
                    {
                        // Now fix up missing data
                        if (string.IsNullOrEmpty(compatData.OpenSupportedMessage))
                        {
                            compatData.OpenSupportedMessage = VSResources.PartialSupportedDotNetCoreProject;
                        }

                        if (string.IsNullOrEmpty(compatData.OpenUnsupportedMessage))
                        {
                            compatData.OpenUnsupportedMessage = VSResources.NotSupportedDotNetCoreProject;
                        }

                        CurrentVersionCompatibilityData = compatData;
                        _timeCurVersionDataLastUpdatedUtc = DateTime.UtcNow;
                    }
                }
            }
            catch
            {
            }

            if (CurrentVersionCompatibilityData == null)
            {
                // Something failed or no remote file,  use the compatibility data we shipped with which does not have any warnings
                CurrentVersionCompatibilityData = new VersionCompatibilityData
                {
                    OpenSupportedMessage = VSResources.PartialSupportedDotNetCoreProject,
                    OpenUnsupportedMessage = VSResources.NotSupportedDotNetCoreProject
                };
                _timeCurVersionDataLastUpdatedUtc = DateTime.UtcNow;
            }

            return CurrentVersionCompatibilityData;
        }

        /// <summary>
        /// If the cached file exists reads the data and returns it
        /// </summary>
        private Dictionary<Version, VersionCompatibilityData>? GetCompatibilityDataFromCacheFile()
        {
            try
            {
                string? data = _versionDataCacheFile?.ReadCacheFile();
                if (data != null)
                {
                    return VersionCompatibilityData.DeserializeVersionData(data);
                }
            }
            catch
            {
            }
            return null;
        }

        #region Unused

        // Unused IVsSolutionEvents

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            return HResult.OK;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return HResult.OK;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            return HResult.OK;
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            return HResult.OK;
        }

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return HResult.OK;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            return HResult.OK;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            return HResult.OK;
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            return HResult.OK;
        }

        // Unused IVsSolutionLoadEvents

        public int OnAfterLoadProjectBatch(bool fIsBackgroundIdleBatch)
        {
            return HResult.OK;
        }

        public int OnBeforeBackgroundSolutionLoadBegins()
        {
            return HResult.OK;
        }

        public int OnBeforeLoadProjectBatch(bool fIsBackgroundIdleBatch)
        {
            return HResult.OK;
        }

        public int OnBeforeOpenSolution(string pszSolutionFilename)
        {
            return HResult.OK;
        }

        public int OnQueryBackgroundLoadProjectBatch(out bool pfShouldDelayLoadToNextIdle)
        {
            pfShouldDelayLoadToNextIdle = false;
            return HResult.OK;
        }

        #endregion
    }
}
