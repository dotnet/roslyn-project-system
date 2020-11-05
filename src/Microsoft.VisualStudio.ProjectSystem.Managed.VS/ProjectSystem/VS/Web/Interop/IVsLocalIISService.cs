// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    internal enum ManagedPipelineMode
    {
        Integrated = 0,
        Classic = 1
    };

    [Flags]
    internal enum IISAuthentications
    {
        Anonymous = 1,
        Basic = 2,
        Windows = 4,
    };
    [Flags]

    internal enum ScriptMapInfo
    {
        NoInfo = 0,
        NotRegistered = 1,    // ASP.NET is not registered (aspnet_regiis has not been run)
        NotApplication = 2,    // The website is not marked as an application
        UpdateNotSupported = 4, // Update is not supported (remote webs)
        NotEnabled = 8,         // IIS6.0 - aspnet has not been enabled on the server.
    };

    internal static class ErrorCodes
    {
        public const int

            ERROR_FILE_NOT_FOUND = unchecked((int)0x80070002),
            ERROR_PATH_NOT_FOUND = unchecked((int)0x80070003),
            ERROR_INVALID_DATA = unchecked((int)0x8007000d),
            ERROR_NOT_FOUND = unchecked((int)0x80070490),
            E_OUTOFMEMORY = unchecked((int)0x8007000E),
            E_INVALIDARG = unchecked((int)0x80070057),
            E_FAIL = unchecked((int)0x80004005),
            E_NOINTERFACE = unchecked((int)0x80004002),
            E_POINTER = unchecked((int)0x80004003),
            E_NOTIMPL = unchecked((int)0x80004001),
            E_UNEXPECTED = unchecked((int)0x8000FFFF),
            E_HANDLE = unchecked((int)0x80070006),
            E_ABORT = unchecked((int)0x80004004),
            E_ACCESSDENIED = unchecked((int)0x80070005),
            E_PENDING = unchecked((int)0x8000000A),
            ERROR_NOT_SUPPORTED = unchecked((int)0x80070032),
            ILD_NORMAL = 0x0000,
            ILD_TRANSPARENT = 0x0001,
            ILD_MASK = 0x0010,
            ILD_ROP = 0x0040,
            BUFFER_E_RELOAD_OCCURRED = unchecked((int)0x80041009),
            DIRPRJ_E_SITEEXISTS_IIS_AND_IISEXPRESS = unchecked((int)0x80040406),
            DIRPRJ_E_NOTFOUND_IISEXPRESS_ACCESSDENIED_IIS = unchecked((int)0x80040408),
            DIRPRJ_E_ASPNETNOTREGISTERED = unchecked((int)0x80040405),
            DV_E_FORMATETC = unchecked((int)0x80040064),
            WEBSERVER_E_NOTRUNNING = unchecked((int)0x80040600);    
      }

    [ComImport()]
    [Guid("97A96122-0634-4ca3-9EB2-6AA127723EE5")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVsIISSite
    {
        [PreserveSig]
        int IsIISExpress([Out][MarshalAs(UnmanagedType.VariantBool)] out bool isIISExpress);

        [PreserveSig]
        int GetPathForRelativeUrl(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszRelativeUrl,
            [Out][MarshalAs(UnmanagedType.BStr)] out string bstrDiskPath,
            [Out][MarshalAs(UnmanagedType.VariantBool)] out bool pbIsVdir,
            [Out][MarshalAs(UnmanagedType.VariantBool)] out bool pbIsApp);

        [PreserveSig]
        int GetAlternateUrlForUrl(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
            [Out][MarshalAs(UnmanagedType.BStr)] out string bstrAlternateUrl);

        [PreserveSig]
        int AddServerBinding(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszHostHeader,
            [In] [MarshalAs(UnmanagedType.U2)] short uPort,
            [In][MarshalAs(UnmanagedType.VariantBool)] bool addSecureBinding,
            [Out][MarshalAs(UnmanagedType.BStr)] out string bstrSecureUrl); // The resulting url

        [PreserveSig]
        int RemoveServerBinding(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszHostHeader,
            [In][MarshalAs(UnmanagedType.VariantBool)] bool isSecureBinding,
            [In] [MarshalAs(UnmanagedType.U2)] short uPort);

        [PreserveSig]
        int GetApplicationPipelineMode(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszRelativeUrl,
            [Out] [MarshalAs(UnmanagedType.U4)] out ManagedPipelineMode mode);

        [PreserveSig]
        int SetApplicationPipelineMode(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszRelativeUrl,
            [In] [MarshalAs(UnmanagedType.U4)] ManagedPipelineMode mode);

        [PreserveSig]
        int GetAuthentication(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszRelativeUrl,
            [Out] [MarshalAs(UnmanagedType.U4)] out IISAuthentications curAuthModes,
            [Out] [MarshalAs(UnmanagedType.U4)] out IISAuthentications validAuthModeMask);    // Indicates which ones are valid in case some are unknown

        [PreserveSig]
        int SetAuthentication(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszRelativeUrl,
            [In] [MarshalAs(UnmanagedType.U4)] IISAuthentications authModesToSet,
            [In] [MarshalAs(UnmanagedType.U4)] IISAuthentications authModesMask,
            [Out] [MarshalAs(UnmanagedType.U4)] out IISAuthentications newAuthModes,
            [Out] [MarshalAs(UnmanagedType.U4)] out IISAuthentications validNewAuthModeMask);    // Indicates which ones are valid in case some are unknown

        [PreserveSig]
        int GetMetabasePathForRelativeUrl(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszRelativeUrl,
            [In] [MarshalAs(UnmanagedType.VariantBool)] bool fIncludeLM,
            [Out] [MarshalAs(UnmanagedType.BStr)] out string metabasePath);

        [PreserveSig]
        int GetUrlMatchingPath(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszDiskPath,
            [Out] [MarshalAs(UnmanagedType.BStr)] out string bstrMatchingUrl);

        [PreserveSig]
        int GetScriptMapInfo(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszRelUrl,
            [Out][MarshalAs(UnmanagedType.BStr)] out string pbstrVersion,
            [In][Out][MarshalAs(UnmanagedType.I4)] ref ScriptMapInfo pScriptMapInfo);

        [PreserveSig]
        int UpdateScriptMaps(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszRelUrl,
            [In] [MarshalAs(UnmanagedType.I4)] uint dwTgtMajorVersion,
            [In] [MarshalAs(UnmanagedType.I4)] uint dwTgtMinorVersion,
            [In] [MarshalAs(UnmanagedType.Bool)] bool isUpgrade);

        [PreserveSig]
        int GetApplicationPoolIdentity(
           [In] [MarshalAs(UnmanagedType.LPWStr)] string pszRelUrl,
           [Out] [MarshalAs(UnmanagedType.BStr)] out string pbstrApplicationPoolIdentity);

        [PreserveSig]
        int EnableLoadUserProfile(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszRelUrl);

        [PreserveSig]
        int CreateVirtualDirectory(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszRelUrl,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszDiskPath,
            [In] [MarshalAs(UnmanagedType.Bool)] bool overwriteExisting);

        [PreserveSig]
        int GetSiteDisplayName(
           [Out] [MarshalAs(UnmanagedType.BStr)] out string pbstrSiteName);
    };

    [ComImport()]
    [Guid("441F8B2D-28E3-41d2-A0EE-A91E45B502F2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVsLocalIISService
    {
        [PreserveSig]
        int IsIISInstalled([Out][MarshalAs(UnmanagedType.VariantBool)] out bool isInstalled);

        [PreserveSig]
        int IsIISExpressInstalled([Out][MarshalAs(UnmanagedType.VariantBool)] out bool isInstalled);

        [PreserveSig]
        int IsUrlHostedInIIsOrIISExpress(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
            [Out][MarshalAs(UnmanagedType.VariantBool)] out bool pIsHostedOnIIS,
            [Out][MarshalAs(UnmanagedType.VariantBool)] out bool pIsHostedOnIISExpress);

        [PreserveSig]
        int GetRunningIISExpressProcessForSite(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszConfigPath,
            [Out][MarshalAs(UnmanagedType.U4)] out uint pdwPid);

        [PreserveSig]
        int GetIISExpressCommandLineForSite(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszConfigPath,
            [Out][MarshalAs(UnmanagedType.BStr)] out string pbstrCommandLine);

        [PreserveSig]
        int StopIISExpressProcess(
            [In][MarshalAs(UnmanagedType.U4)] uint dwPid);

        [PreserveSig]
        int OpenIISSite(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszLocalUrl,
            [In] [MarshalAs(UnmanagedType.VariantBool)] bool bPreferIISExpress,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string? pszConfigPath,
            [Out][MarshalAs(UnmanagedType.Interface)] out IVsIISSite vsIISSite);

        [PreserveSig]
        int CreateNewIISSite(
            [In] [MarshalAs(UnmanagedType.VariantBool)] bool bCreateOnIISExpress,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszSiteName,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszDiskPath,
            [In] [MarshalAs(UnmanagedType.U2)] ushort dwPort,
            [In] [MarshalAs(UnmanagedType.VariantBool)] bool bAddSecureBindings,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszConfigPath,
            [Out][MarshalAs(UnmanagedType.Interface)] out IVsIISSite vsIISSite);

        [PreserveSig]
        int GetUniqueNewSiteName(
            [In] [MarshalAs(UnmanagedType.VariantBool)] bool bCreateOnIISExpress,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszSuggestedSiteName,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszConfigPath,
            [Out][MarshalAs(UnmanagedType.BStr)] out string bstrUniqueName);

        [PreserveSig]
        int GetPathForLocalUrl(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszLocalUrl,
            [Out][MarshalAs(UnmanagedType.BStr)] out string bstrDiskPath,
            [Out][MarshalAs(UnmanagedType.VariantBool)] out bool pbIsVdir,
            [Out][MarshalAs(UnmanagedType.VariantBool)] out bool pbIsApp,
            [Out][MarshalAs(UnmanagedType.VariantBool)] out bool pbIsHostedOnIISExpress);

        [PreserveSig]
        int GetIISExpressApplicationHostFilePath(
            [Out] [MarshalAs(UnmanagedType.BStr)] out string appHostConfigPath);

        [PreserveSig]
        int GetApplicationUrlOfDiskPath(
            [In] [MarshalAs(UnmanagedType.VariantBool)] bool bCheckIISExpress,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszDiskPath,
            [Out] [MarshalAs(UnmanagedType.BStr)] out string applicationUrl);
    };

    [ComImport()]
    [Guid("C191FDF1-D98E-4861-A270-160704A2463F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVsIISExpressService
    {
        [PreserveSig]
        int EnsureApplicationHostConfigFileIsCreated();

        [PreserveSig]
        int GetRunningIISExpressProcessForSite(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
            [Out][MarshalAs(UnmanagedType.U4)] out uint pdwPid);

        [PreserveSig]
        int GetIISExpressCommandLineForSite(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
            [Out][MarshalAs(UnmanagedType.BStr)] out string pbstrCommandLine);

        [PreserveSig]
        int StopIISExpressProcess(
            [In][MarshalAs(UnmanagedType.U4)] uint dwPid);

        [PreserveSig]
        int OpenIISSite(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszLocalUrl,
            [Out][MarshalAs(UnmanagedType.Interface)] out IVsIISSite vsIISSite);

        [PreserveSig]
        int CreateNewIISSite(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszSiteName,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string? pszDiskPath,
            [In] [MarshalAs(UnmanagedType.U2)] ushort dwPort,
            [In] [MarshalAs(UnmanagedType.VariantBool)] bool bAddSecureBindings,
            [Out][MarshalAs(UnmanagedType.Interface)] out IVsIISSite vsIISSite);

        [PreserveSig]
        int GetUniqueNewSiteName(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszSuggestedSiteName,
            [Out][MarshalAs(UnmanagedType.BStr)] out string bstrUniqueName);

        [PreserveSig]
        int GetPathForLocalUrl(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszLocalUrl,
            [Out][MarshalAs(UnmanagedType.BStr)] out string bstrDiskPath,
            [Out][MarshalAs(UnmanagedType.VariantBool)] out bool pbIsVdir,
            [Out][MarshalAs(UnmanagedType.VariantBool)] out bool pbIsApp);

        [PreserveSig]
        int GetIISExpressApplicationHostFilePath(
            [Out] [MarshalAs(UnmanagedType.BStr)] out string appHostConfigPath);

        [PreserveSig]
        int GetApplicationUrlOfDiskPath(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszDiskPath,
            [Out] [MarshalAs(UnmanagedType.BStr)] out string applicationUrl);
    }

    [ComImport()]
    [Guid("2328A33C-B56F-4D9A-A405-0CCE425A1815")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVsLocalIISService2
    {
        [PreserveSig]
        int GetIISExpressServiceForConfigFile(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string? pszPathToApplicationHostConfig, 
            [Out] [MarshalAs(UnmanagedType.Interface)] out IVsIISExpressService  ppIISExpressSvc);

        [PreserveSig]
        int GetDefaultConfigFileForSolution(
            [Out] [MarshalAs(UnmanagedType.BStr)] out string strPathToApplicationHostConfig);
    }

    [ComImport()]
    [Guid("60446251-7C2A-4EB1-B85F-E171D860F624")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVsIISServiceEvents
    {
        void BeforeMoveApplicationHostConfigFile();
        void AfterMoveApplicationHostConfigFile();
    }

    [ComImport()]
    [Guid("4B3BDB78-805D-4904-83EB-FDF931B570CC")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVsLocalIISService3
    {
        [PreserveSig]
        int AdviseIISServiceEvents(
            [In][MarshalAs(UnmanagedType.Interface)] IVsIISServiceEvents pEventSink,
            [Out] out uint pdwAdviseCookie);

        [PreserveSig]
        int UnadviseIISServiceEvents(
            [In] uint  dwAdviseCookie);
    }
}
