' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

Imports System.Runtime.InteropServices
Imports Microsoft.VisualStudio.Shell.Interop

Namespace Microsoft.VisualStudio.Editors.PropPageDesigner

    ''' <summary>
    ''' Editor factory for property pages that opt-in to Partial Load Mode (DeferUntilIntellisenseIsReady)
    ''' </summary>
    <CLSCompliant(False),
    Guid("79d33e5a-ad5c-43ea-9b62-426b05459b2a")>
    Public NotInheritable Class DeferredPropPageDesignerEditorFactory
        Inherits PropPageDesignerEditorFactory
        Implements IVsEditorFactory4

        Private _fileName As String

        Public Function CreateDocData(<[In]> grfCreate As UInteger, <[In]> <MarshalAs(UnmanagedType.LPWStr)> pszMkDocument As String, <[In]> <MarshalAs(UnmanagedType.Interface)> pHier As IVsHierarchy, <[In]> itemid As UInteger) As <MarshalAs(UnmanagedType.IUnknown)> Object Implements IVsEditorFactory4.CreateDocData
            _fileName = pszMkDocument
            Return GetDocData()
        End Function

        Public Function CreateDocView(<[In]> grfCreate As UInteger, <[In]> <MarshalAs(UnmanagedType.LPWStr)> pszPhysicalView As String, <[In]> punkDocData As IntPtr, <[In]> itemid As UInteger, <MarshalAs(UnmanagedType.BStr)> <Out> ByRef pbstrEditorCaption As String, <Out> ByRef pguidCmdUI As Guid, <Out> ByRef pgrfCDW As Integer) As <MarshalAs(UnmanagedType.IUnknown)> Object Implements IVsEditorFactory4.CreateDocView
            Return GetDocView(_fileName)
        End Function
    End Class

End Namespace
