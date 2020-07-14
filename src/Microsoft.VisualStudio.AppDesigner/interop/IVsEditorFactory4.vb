' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

'-----------------------------------------------------------------------------
' Refer to VSShellPrivate16.idl for detail.
'-----------------------------------------------------------------------------
Imports System.Runtime.InteropServices

Namespace Microsoft.VisualStudio.Shell.Interop

    <ComVisible(False),
    ComImport,
    Guid("5F149946-406A-4B77-A334-9314CDBACD2F"),
    InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Friend Interface IVsEditorFactory4
        Function CreateDocData(
            <[In]> ByVal grfCreate As UInteger,
            <[In], MarshalAs(UnmanagedType.LPWStr)> ByVal pszMkDocument As String,
            <[In], MarshalAs(UnmanagedType.[Interface])> ByVal pHier As IVsHierarchy,
            <[In]> ByVal itemid As UInteger) _
        As <MarshalAs(UnmanagedType.IUnknown)> Object

        Function CreateDocView(
            <[In]> ByVal grfCreate As UInteger,
            <[In], MarshalAs(UnmanagedType.LPWStr)> ByVal pszPhysicalView As String,
            <[In]> ByVal punkDocData As IntPtr,
            <[In]> ByVal itemid As UInteger,
            <Out, MarshalAs(UnmanagedType.BStr)> ByRef pbstrEditorCaption As String,
            <Out> ByRef pguidCmdUI As Guid,
            <Out> ByRef pgrfCDW As Integer) _
        As <MarshalAs(UnmanagedType.IUnknown)> Object
    End Interface

End Namespace
