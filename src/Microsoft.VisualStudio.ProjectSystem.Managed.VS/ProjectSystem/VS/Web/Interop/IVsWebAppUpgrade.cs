// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    [ComImport()]
    [Guid("97422BAE-2144-4207-AB16-B4C3B7A2D9A3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVsWebAppUpgrade
    {
        void SetProjPathAndHierarchy(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszProjectDirectory,
            [In] [MarshalAs(UnmanagedType.Interface)]IVsHierarchy pIVsHier);

        void UpgradeEveAspxFileToWebApp(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

        void UpgradeEveProjToWebApp(
            [In] [MarshalAs(UnmanagedType.Interface)]IVsHierarchy pIVsHier,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszProjectName,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszFolder,
            [In] [MarshalAs(UnmanagedType.Bool)] bool bProjFileUpgrade);

        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsUpgradingProject(
            [In] [MarshalAs(UnmanagedType.LPWStr)]string pszProjectName);

        [PreserveSig]
        int CreateResourceDocData(
            [In] [MarshalAs(UnmanagedType.LPWStr)]string pszFileName,
            [Out][MarshalAs(UnmanagedType.SysInt)] out IntPtr punkDocData);

        [PreserveSig]
        int ChangePageAttribute(
                 [In] [MarshalAs(UnmanagedType.LPWStr)] string filename,
                 [In] [MarshalAs(UnmanagedType.LPWStr)] string attributeName,
                 [In] [MarshalAs(UnmanagedType.LPWStr)] string oldValue,
                 [In] [MarshalAs(UnmanagedType.LPWStr)] string newValue);

        [PreserveSig]
        int GetScriptMapInfo(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
            [Out][MarshalAs(UnmanagedType.BStr)] out string pbstrVersion,
            [In][Out][MarshalAs(UnmanagedType.I4)] ref ScriptMapInfo pScriptMapInfo);

        [PreserveSig]
        int UpdateScriptMaps(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
            [In] [MarshalAs(UnmanagedType.I4)] uint dwTgtMajorVersion,
            [In] [MarshalAs(UnmanagedType.I4)] uint dwTgtMinorVersion,
            [In] [MarshalAs(UnmanagedType.Bool)] bool isUpgrade);

        [PreserveSig]
        int CreateVirtualDirectory(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszVirtualRootPath,
            [In] bool bSetScriptMaps,
            [In] [MarshalAs(UnmanagedType.I4)] uint dwTgtMajorVersion,
            [In] [MarshalAs(UnmanagedType.I4)] uint dwTgtMinorVersion);

        [PreserveSig]
        int GetUrlMatchingPath(
           [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
           [In] [MarshalAs(UnmanagedType.LPWStr)] string pszDiskPath,
           [Out] [MarshalAs(UnmanagedType.BStr)] out string strMatchingUrl);

        [PreserveSig]
        int SetASPNETPermissionsForPath(
           [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrlorPath,
           [In] [MarshalAs(UnmanagedType.Bool)] bool bReadAccessOnly);

        [PreserveSig]
        int GetAppOfflineFile(
            [Out] [MarshalAs(UnmanagedType.BStr)] out string strOffLineFile);

        [PreserveSig]
        int GetPathForLocalUrl(
           [In] [MarshalAs(UnmanagedType.LPWStr)] string pszLocalUrl,
           [Out] [MarshalAs(UnmanagedType.BStr)] out string strDiskPath);

        [PreserveSig]
        int WrapServiceProvider(
            [In] [MarshalAs(UnmanagedType.Interface)]IVsProject pIVsProj,
            [In] [MarshalAs(UnmanagedType.Interface)]Microsoft.VisualStudio.OLE.Interop.IServiceProvider pSP,
            [Out] [MarshalAs(UnmanagedType.Interface)] out Microsoft.VisualStudio.OLE.Interop.IServiceProvider pWrappedSP);

        [PreserveSig]
        int EnsureLocalPathProperty(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszLocalPath);

        [PreserveSig]
        int GetPathForLocalUrlEx(
           [In] [MarshalAs(UnmanagedType.LPWStr)] string pszLocalUrl,
           [Out] [MarshalAs(UnmanagedType.BStr)] out string strDiskPath,
           [Out] [MarshalAs(UnmanagedType.VariantBool)] out bool bIsHostedOnIISExpress);

        [PreserveSig]
        int CreateVirtualDirectoryEx(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszUrl,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszVirtualRootPath,
            [In] [MarshalAs(UnmanagedType.VariantBool)]bool bSetScriptMaps,
            [In] [MarshalAs(UnmanagedType.I4)] uint dwTgtMajorVersion,
            [In] [MarshalAs(UnmanagedType.I4)] uint dwTgtMinorVersion,
            [In] [MarshalAs(UnmanagedType.VariantBool)] bool bOverwriteExisting,
            [Out] [MarshalAs(UnmanagedType.VariantBool)] out bool bIsHostedOnIISExpress);

        [PreserveSig]
        int GetPageAttribute(
                 [In] [MarshalAs(UnmanagedType.LPWStr)] string filename,
                 [In] [MarshalAs(UnmanagedType.LPWStr)] string attributeName,
                 [Out] [MarshalAs(UnmanagedType.BStr)] out string value);

        [PreserveSig]
        int SetASPNETDataDirectoryPermissions(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszSiteUrl,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszDataDirectory);
        
        [PreserveSig]
        int SetDirectoryPermissions(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszSiteUrl,
            [In] [MarshalAs(UnmanagedType.LPWStr)] string pszDataDirectory,
            [In] [MarshalAs(UnmanagedType.VariantBool)] bool bIncludeWrite,
            [In] [MarshalAs(UnmanagedType.VariantBool)] bool bIncludeExecute);
    }
}
