' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

Imports System.Runtime.InteropServices

Imports Microsoft.VisualStudio.Designer.Interfaces
Imports Microsoft.VisualStudio.OLE.Interop
Imports Microsoft.VisualStudio.Shell
Imports Microsoft.VisualStudio.Shell.Interop

Imports Common = Microsoft.VisualStudio.Editors.AppDesCommon

Namespace Microsoft.VisualStudio.Editors.PropPageDesigner

    '**************************************************************************
    ';PropPageDesignerEditorFactory
    '
    'Remarks:
    '   The editor factory for the resource editor.  The job of this class is
    '   simply to create a new resource editor designer when requested by the
    '   shell.
    '**************************************************************************
    <CLSCompliant(False),
    Guid("b270807c-d8c6-49eb-8ebe-8e8d566637a1")>
    Public Class PropPageDesignerEditorFactory
        Implements IVsEditorFactory

        'The all important GUIDs 
        Private Shared ReadOnly s_editorGuid As New Guid("{b270807c-d8c6-49eb-8ebe-8e8d566637a1}")
        Private Shared ReadOnly s_commandUIGuid As New Guid("{86670efa-3c28-4115-8776-a4d5bb1f27cc}")

        'Exposing the GUID for the rest of the assembly to see
        Public Shared ReadOnly Property EditorGuid As Guid
            Get
                Return s_editorGuid
            End Get
        End Property

        'Exposing the GUID for the rest of the assembly to see
        Public Shared ReadOnly Property CommandUIGuid As Guid
            Get
                Return s_commandUIGuid
            End Get
        End Property

        Private _site As Object 'The site that owns this editor factory
        Private _siteProvider As ServiceProvider 'The service provider from m_Site

        ''' <summary>
        ''' Creates a new editor for the given pile of flags.  Helper function for the overload
        ''' which implements IVsEditorFactory.CreateEditorInstance
        ''' </summary>
        ''' <param name="FileName">[In] Filename being opened</param>
        ''' <param name="ExistingDocData">[In] Existing DocData if any</param>
        ''' <param name="DocView">Returns the IVsWindowPane object</param>
        ''' <param name="DocData">Returns DocData object</param>
        ''' <param name="Caption">Returns caption for document window</param>
        ''' <param name="CmdUIGuid">Returns guid for CMDUI</param>
        ''' <param name="Canceled">Returns True if user canceled</param>
        Private Sub InternalCreateEditorInstance(FileName As String,
                ExistingDocData As Object,
                ByRef DocView As Object,
                ByRef DocData As Object,
                ByRef Caption As String,
                ByRef CmdUIGuid As Guid,
                ByRef Canceled As Boolean)
            Canceled = False
            CmdUIGuid = Guid.Empty

            Dim DesignerLoader As PropPageDesignerLoader = Nothing

            Try
                Using New Common.WaitCursor

                    DocView = Nothing
                    DocData = Nothing
                    Caption = Nothing


                    If ExistingDocData Is Nothing Then
                        DocData = GetDocData()
                    ElseIf TypeOf ExistingDocData Is PropPageDesignerDocData Then
                        DocData = ExistingDocData
                    Else
                        Throw New COMException(My.Resources.Designer.DFX_IncompatibleBuffer, AppDesInterop.NativeMethods.VS_E_INCOMPATIBLEDOCDATA)
                    End If

                    'Gets the object that can support IVsWindowPane
                    DocView = GetDocView(FileName)

                    'Site the TextStream
                    If TypeOf DocData Is IObjectWithSite Then
                        CType(DocData, IObjectWithSite).SetSite(_site)
                    Else
                        Debug.Fail("DocData does not implement IObjectWithSite")
                    End If

                    Caption = "" ' Leave empty - The property page Title will appear as the caption 'Application|References|Debug etc.'

                    'Set the command UI
                    CmdUIGuid = s_commandUIGuid
                End Using

            Catch ex As Exception

                If DesignerLoader IsNot Nothing Then
                    'We need to let the DesignerLoader disconnect from events
                    DesignerLoader.Dispose()
                End If

                ' If we created the doc data then we should dispose it for a failure
                If ExistingDocData Is Nothing Then
                    DirectCast(DocData, PropPageDesignerDocData).Dispose()
                End If

                Throw New Exception(My.Resources.Designer.GetString(My.Resources.Designer.DFX_CreateEditorInstanceFailed_Ex, ex.Message))
            End Try
        End Sub

        Protected Function GetDocView(fileName As String) As Object
            Dim DesignerService As IVSMDDesignerService = CType(_siteProvider.GetService(GetType(IVSMDDesignerService)), IVSMDDesignerService)
            If DesignerService Is Nothing Then
                Throw New Exception(My.Resources.Designer.GetString(My.Resources.Designer.DFX_EditorNoDesignerService, fileName))
            End If
            Dim DesignerLoader = CType(DesignerService.CreateDesignerLoader(GetType(PropPageDesignerLoader).AssemblyQualifiedName), PropPageDesignerLoader)

            Dim OleProvider As IServiceProvider = CType(_siteProvider.GetService(GetType(IServiceProvider)), IServiceProvider)
            Dim Designer = DesignerService.CreateDesigner(OleProvider, DesignerLoader)

            Debug.Assert(Designer IsNot Nothing, "Designer service should have thrown if it had a problem.")

            Return Designer.View
        End Function

        Protected Function GetDocData() As Object
            Return New PropPageDesignerDocData(_siteProvider)
        End Function

        ''' <summary>
        ''' Disconnect from the owning site
        ''' </summary>
        Public Function Close() As Integer Implements IVsEditorFactory.Close
            _siteProvider = Nothing
            _site = Nothing
        End Function

        ''' <summary>
        ''' Wrapper of COM interface which delegates to Internal
        ''' </summary>
        Private Function IVsEditorFactory_CreateEditorInstance(
                vscreateeditorflags As UInteger,
                FileName As String,
                PhysicalView As String,
                Hierarchy As IVsHierarchy,
                Itemid As UInteger,
                ExistingDocDataPtr As IntPtr,
                ByRef DocViewPtr As IntPtr,
                ByRef DocDataPtr As IntPtr,
                ByRef Caption As String,
                ByRef CmdUIGuid As Guid,
                ByRef FCanceled As Integer) As Integer _
        Implements IVsEditorFactory.CreateEditorInstance

            Dim ExistingDocData As Object = Nothing
            Dim DocView As Object = Nothing
            Dim DocData As Object = Nothing
            Dim CanceledAsBoolean As Boolean = False

            DocViewPtr = IntPtr.Zero
            DocDataPtr = IntPtr.Zero

            If Not ExistingDocDataPtr.Equals(IntPtr.Zero) Then
                ExistingDocData = Marshal.GetObjectForIUnknown(ExistingDocDataPtr)
            End If

            Caption = Nothing

            InternalCreateEditorInstance(FileName, ExistingDocData,
                DocView, DocData, Caption, CmdUIGuid, CanceledAsBoolean)

            If CanceledAsBoolean Then
                FCanceled = 1
            Else
                FCanceled = 0
            End If

            If DocView IsNot Nothing Then
                DocViewPtr = Marshal.GetIUnknownForObject(DocView)
            End If
            If DocData IsNot Nothing Then
                DocDataPtr = Marshal.GetIUnknownForObject(DocData)
            End If
        End Function

        ''' <summary>
        ''' We only support the default view
        ''' </summary>
        ''' <param name="rguidLogicalView"></param>
        ''' <param name="pbstrPhysicalView"></param>
        Public Function MapLogicalView(ByRef rguidLogicalView As Guid, ByRef pbstrPhysicalView As String) As Integer Implements IVsEditorFactory.MapLogicalView
            pbstrPhysicalView = Nothing
        End Function

        ''' <summary>
        ''' Called by owning site after creation
        ''' </summary>
        ''' <param name="Site"></param>
        Public Function SetSite(Site As IServiceProvider) As Integer Implements IVsEditorFactory.SetSite
            'This same Site already set?  Or Site not yet initialized (= Nothing)?  If so, NOP.
            If _site Is Site Then
                Exit Function
            End If
            'Site is different - set it
            _site = Site
            If TypeOf Site Is IServiceProvider Then
                _siteProvider = New ServiceProvider(CType(Site, IServiceProvider))
            Else
                Debug.Fail("Site IsNot OLE.Interop.IServiceProvider")
            End If
        End Function
    End Class

End Namespace
