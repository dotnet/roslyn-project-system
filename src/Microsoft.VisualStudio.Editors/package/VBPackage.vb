' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

Imports System.ComponentModel.Design
Imports System.Runtime.InteropServices

Imports Microsoft.VisualStudio.Editors.OptionPages
Imports Microsoft.VisualStudio.Shell
Imports Microsoft.VisualStudio.Shell.Interop
Imports Microsoft.VisualStudio.XmlEditor

<Assembly: Guid("832BFEE6-9036-423E-B90A-EA4C582DA1D2")>

Namespace Microsoft.VisualStudio.Editors


    '*
    '* This is the Visual Studio package for the Microsoft.VisualStudio.Editors assembly.  It will be CoCreated by
    '* Visual Studio during package load in response to the GUID contained below.
    '*

    '*
    '* IMPORTANT NOTE:
    '* We are not currently using RegPkg.exe to register this assembly, so those attributes have been removed 
    '*   from here for the moment.
    '*
    '* In the future, we should consider moving to a RegPkg.exe model
    <Guid(VBPackage.PackageGuid),
    ProvideOptionPage(GetType(GeneralOptionPage),
                     "Projects",
                     "NETCore",
                     0,                     ' categoryResourceID: Not used, we don't own parent category
                     1500,                  ' pageNameResourceID
                     True,                  ' supportsAutomation
                     1600,                  ' keywordListResourceId
                     IsServerAware:=True),  ' Configured to work in cloud environment scenarios
    ProvideMenuResource("Menus.ctmenu", 30),
    ProvideEditorFactory(GetType(ApplicationDesigner.ApplicationDesignerEditorFactory), 1300, True, TrustLevel:=__VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted),
    ProvideEditorFactory(GetType(SettingsDesigner.SettingsDesignerEditorFactory), 1200, True, TrustLevel:=__VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted, CommonPhysicalViewAttributes:=3),
    ProvideEditorFactory(GetType(PropPageDesigner.PropPageDesignerEditorFactory), 1400, False, TrustLevel:=__VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted),
    ProvideEditorFactory(GetType(PropPageDesigner.DeferredPropPageDesignerEditorFactory), 1400, True, TrustLevel:=__VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted),
    ProvideEditorFactory(GetType(ResourceEditor.ResourceEditorFactory), 1100, True, TrustLevel:=__VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted, CommonPhysicalViewAttributes:=3),
    ProvideService(GetType(ResourceEditor.ResourceEditorRefactorNotify), ServiceName:="ResX RefactorNotify Service"),
    ProvideService(GetType(AddImports.IVBAddImportsDialogService), ServiceName:="Add Imports Dialog Service"),
    ProvideService(GetType(XmlIntellisense.IXmlIntellisenseService), ServiceName:="Vb Xml Intellisense Service"),
    ProvideService(GetType(VBAttributeEditor.Interop.IVbPermissionSetService), ServiceName:="Vb Permission Set Service"),
    ProvideService(GetType(Interop.IVsBuildEventCommandLineDialogService), ServiceName:="Vb Build Event Command Line Dialog Service"),
    ProvideService(GetType(VBRefChangedSvc.Interop.IVbReferenceChangedService), ServiceName:="VB Project Reference Changed Service"),
    ProvideKeyBindingTable(Constants.MenuConstants.GUID_SETTINGSDESIGNER_CommandUIString, 1200, AllowNavKeyBinding:=False),
    ProvideKeyBindingTable(Constants.MenuConstants.GUID_RESXEditorCommandUIString, 1100, AllowNavKeyBinding:=False),
    CLSCompliant(False)
    >
    Friend Class VBPackage
        Inherits Shell.Package
        Implements IVBPackage

        Private _permissionSetService As VBAttributeEditor.PermissionSetService
        Private _xmlIntellisenseService As XmlIntellisense.XmlIntellisenseService
        Private _buildEventCommandLineDialogService As PropertyPages.BuildEventCommandLineDialogService
        Private _vbReferenceChangedService As VBRefChangedSvc.VBReferenceChangedService
        Private _resourceEditorRefactorNotify As ResourceEditor.ResourceEditorRefactorNotify
        Private _userConfigCleaner As UserConfigCleaner
        Private _addImportsDialogService As AddImports.AddImportsDialogService

        Private Const ProjectDesignerSUOKey As String = "ProjectDesigner"
        Public Const PackageGuid As String = "67909B06-91E9-4F3E-AB50-495046BE9A9A"
        Public Const LegacyVBPackageGuid As String = "{164B10B9-B200-11D0-8C61-00A0C91E29D5}"
        Public Const LegacyCSharpPackageGuid As String = "{FAE04EC1-301F-11d3-BF4B-00C04F79EFBC}"

        ' Map between unique project GUID and the last viewed tab in the project designer...
        Private _lastViewedProjectDesignerTab As Dictionary(Of Guid, Byte)

        Public ReadOnly Property StickyProjectResourcePaths As New Dictionary(Of Guid, Dictionary(Of String, String))

        ''' <summary>
        ''' Constructor
        ''' </summary>
        Public Sub New()

            ' Make sure we persist this 
            AddOptionKey(ProjectDesignerSUOKey)
        End Sub

        ''' <summary>
        ''' Initialize package (register editor factories, add services)
        ''' </summary>
        Protected Overrides Sub Initialize()
            Debug.Assert(s_instance Is Nothing, "VBPackage initialized multiple times?")
            s_instance = Me
            MyBase.Initialize()

            'Register editor factories
            Try
                RegisterEditorFactory(New SettingsDesigner.SettingsDesignerEditorFactory)
            Catch ex As Exception When Common.ReportWithoutCrash(ex, "Exception registering settings designer editor factory", NameOf(VBPackage))
                Throw
            End Try
            Try
                RegisterEditorFactory(New ResourceEditor.ResourceEditorFactory)
            Catch ex As Exception When Common.ReportWithoutCrash(ex, "Exception registering resource editor factory", NameOf(VBPackage))
                Throw
            End Try
            Try
                RegisterEditorFactory(New ApplicationDesigner.ApplicationDesignerEditorFactory)
            Catch ex As Exception When Common.ReportWithoutCrash(ex, "Exception registering application resource editor factory", NameOf(VBPackage))
                Throw
            End Try
            Try
                RegisterEditorFactory(New PropPageDesigner.PropPageDesignerEditorFactory)
            Catch ex As Exception When Common.ReportWithoutCrash(ex, "Exception registering property page designer editor factory", NameOf(VBPackage))
                Throw
            End Try
            Try
                RegisterEditorFactory(New PropPageDesigner.DeferredPropPageDesignerEditorFactory)
            Catch ex As Exception When Common.ReportWithoutCrash(ex, "Exception registering property page designer editor factory", NameOf(VBPackage))
                Throw
            End Try

            ' Create callback for deferred service loading
            Dim CallBack As ServiceCreatorCallback = New ServiceCreatorCallback(AddressOf OnCreateService)

            ' The VSIP package is a service container
            Dim ServiceContainer As IServiceContainer = CType(Me, IServiceContainer)

            ' Expose Permission Set Service
            ServiceContainer.AddService(GetType(VBAttributeEditor.Interop.IVbPermissionSetService), CallBack, True)

            ' Expose Xml Intellisense Service
            ServiceContainer.AddService(GetType(XmlIntellisense.IXmlIntellisenseService), CallBack, True)

            ' Expose IVsBuildEventCommandLineDialogService
            ServiceContainer.AddService(GetType(Interop.IVsBuildEventCommandLineDialogService), CallBack, True)

            ' Expose IVsRefactorNotify through the ResourceEditorFactory
            ServiceContainer.AddService(GetType(ResourceEditor.ResourceEditorRefactorNotify), CallBack, True)

            'Expose Add Imports Dialog Service
            ServiceContainer.AddService(GetType(AddImports.IVBAddImportsDialogService), CallBack, True)

            ' Expose VBReferenceChangedService
            ServiceContainer.AddService(GetType(VBRefChangedSvc.Interop.IVbReferenceChangedService), CallBack, True)

            _userConfigCleaner = New UserConfigCleaner(Me)
        End Sub 'New

        Public ReadOnly Property MenuCommandService As IMenuCommandService Implements IVBPackage.MenuCommandService
            Get
                Return TryCast(GetService(GetType(IMenuCommandService)), IMenuCommandService)
            End Get
        End Property

        ''' <summary>
        ''' Callback to expose services to the shell
        ''' </summary>
        ''' <param name="container"></param>
        ''' <param name="serviceType"></param>
        Private Function OnCreateService(container As IServiceContainer, serviceType As Type) As Object

            ' Is the Permission Set Service being requested?
            If serviceType Is GetType(VBAttributeEditor.Interop.IVbPermissionSetService) Then
                If _permissionSetService Is Nothing Then
                    _permissionSetService = New VBAttributeEditor.PermissionSetService(container)
                End If

                ' Return cached Permission Set Service
                Return _permissionSetService
            End If

            ' Is the Xml Intellisense Service being requested?
            If serviceType Is GetType(XmlIntellisense.IXmlIntellisenseService) Then
                ' Return cached Xml Intellisense Service
                Return GetXmlIntellisenseService(container)
            End If

            ' Is the IVsBuildEventCommandLineDialogService being requested?
            If serviceType Is GetType(Interop.IVsBuildEventCommandLineDialogService) Then
                If _buildEventCommandLineDialogService Is Nothing Then
                    _buildEventCommandLineDialogService = New PropertyPages.BuildEventCommandLineDialogService(container)
                End If

                ' Return cached BuildEventCommandLineDialogService
                Return _buildEventCommandLineDialogService
            End If

            If serviceType Is GetType(ResourceEditor.ResourceEditorRefactorNotify) Then
                If _resourceEditorRefactorNotify Is Nothing Then
                    _resourceEditorRefactorNotify = New ResourceEditor.ResourceEditorRefactorNotify()
                End If

                ' return cached refactor-notify implementer
                Return _resourceEditorRefactorNotify
            End If

            If serviceType Is GetType(AddImports.IVBAddImportsDialogService) Then
                If _addImportsDialogService Is Nothing Then
                    _addImportsDialogService = New AddImports.AddImportsDialogService(Me)
                End If

                Return _addImportsDialogService
            End If

            ' Lazy-init VBReferenceChangedService and return the cached service.
            If serviceType Is GetType(VBRefChangedSvc.Interop.IVbReferenceChangedService) Then
                If _vbReferenceChangedService Is Nothing Then
                    _vbReferenceChangedService = New VBRefChangedSvc.VBReferenceChangedService()
                End If

                Return _vbReferenceChangedService
            End If

            Debug.Fail("VBPackage was requested to create a package it has no knowledge about: " & serviceType.ToString())
            Return Nothing
        End Function

        ''' <summary>
        ''' Get or Create an XmlIntellisenseService object
        ''' </summary>
        ''' <param name="container"></param>
        ''' <remarks>
        ''' This code is factored out of OnCreateService in order to delay loading Microsoft.VisualStudio.XmlEditor.dll
        ''' </remarks>
        Private Function GetXmlIntellisenseService(container As IServiceContainer) As XmlIntellisense.XmlIntellisenseService
            If _xmlIntellisenseService Is Nothing Then
                ' Xml Intellisense Service is only available if the Xml Editor Schema Service is available as well
                Dim schemaService As XmlSchemaService = DirectCast(container.GetService(GetType(XmlSchemaService)), XmlSchemaService)

                If schemaService IsNot Nothing Then
                    _xmlIntellisenseService = New XmlIntellisense.XmlIntellisenseService(container, schemaService)
                End If
            End If

            ' Return cached Xml Intellisense Service
            Return _xmlIntellisenseService
        End Function

        ''' <summary>
        ''' Dispose our resources....
        ''' </summary>
        ''' <param name="disposing"></param>
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                If _userConfigCleaner IsNot Nothing Then
                    _userConfigCleaner.Dispose()
                    _userConfigCleaner = Nothing
                End If
            End If
            MyBase.Dispose(disposing)
        End Sub

        Private Shared s_instance As VBPackage

        Public Shared ReadOnly Property Instance As VBPackage
            Get
                Return s_instance
            End Get
        End Property

        'Used for accessing global services before a component in this assembly gets sited
        Public Shadows ReadOnly Property GetService(serviceType As Type) As Object Implements IVBPackage.GetService
            Get
                Return MyBase.GetService(serviceType)
            End Get
        End Property

#Region "Load/save package options"
        ''' <summary>
        ''' Load options
        ''' </summary>
        ''' <param name="key">Added in the constructor using AddOptionKey </param>
        ''' <param name="stream">Stream to read from</param>
        Protected Overrides Sub OnLoadOptions(key As String, stream As IO.Stream)
            If String.Equals(key, ProjectDesignerSUOKey, StringComparison.Ordinal) Then
                Dim reader As New IO.BinaryReader(stream)
                Dim buf(15) As Byte ' Space enough for a GUID - 16 bytes...
                Try
                    While reader.Read(buf, 0, buf.Length) = buf.Length
                        Dim projGuid As Guid
                        projGuid = New Guid(buf)
                        Dim tab As Byte = reader.ReadByte()
                        If _lastViewedProjectDesignerTab Is Nothing Then
                            _lastViewedProjectDesignerTab = New Dictionary(Of Guid, Byte)
                        End If
                        _lastViewedProjectDesignerTab(projGuid) = tab
                    End While
                Catch ex As Exception When Common.ReportWithoutCrash(ex, "Failed to read settings", NameOf(VBPackage))
                End Try
            Else
                MyBase.OnLoadOptions(key, stream)
            End If
        End Sub

        ''' <summary>
        ''' Save settings for this package
        ''' </summary>
        ''' <param name="key">Added in the constructor using AddOptionKey</param>
        ''' <param name="stream">Stream to read data from</param>
        Protected Overrides Sub OnSaveOptions(key As String, stream As IO.Stream)
            If String.Equals(key, ProjectDesignerSUOKey, StringComparison.Ordinal) Then
                ' This is the project designer's last active tab
                If _lastViewedProjectDesignerTab IsNot Nothing Then
                    Dim hier As IVsHierarchy = Nothing
                    Dim sol As IVsSolution = TryCast(GetService(GetType(IVsSolution)), IVsSolution)
                    Debug.Assert(sol IsNot Nothing, "No solution!? We won't persist the last active tab in the project designer")
                    If sol IsNot Nothing Then
                        For Each projectGuid As Guid In _lastViewedProjectDesignerTab.Keys
                            ' We check all current projects to see what the last active tab was
                            If Interop.NativeMethods.Succeeded(sol.GetProjectOfGuid(projectGuid, hier)) Then
                                Dim tab As Byte = _lastViewedProjectDesignerTab(projectGuid)
                                If tab <> 0 Then
                                    ' We only need to persist this if the last tab was different than the 
                                    ' default value...
                                    Dim projGuidBytes() As Byte = projectGuid.ToByteArray()
                                    stream.Write(projGuidBytes, 0, projGuidBytes.Length)
                                    stream.WriteByte(tab)
                                End If
                            End If
                        Next
                    End If
                End If
            Else
                MyBase.OnSaveOptions(key, stream)
            End If
        End Sub
#End Region

#Region "Load/save project designer's last active tab"
        ''' <summary>
        ''' Get the project guid (VSHPROPID_ProjectIDGuid) from a IVsHierarchy
        ''' </summary>
        ''' <param name="hierarchy"></param>
        Public Shared Function ProjectGUID(hierarchy As IVsHierarchy) As Guid
            Dim projGuid As Guid = Guid.Empty
            Try
                If hierarchy IsNot Nothing Then
                    VSErrorHandler.ThrowOnFailure(hierarchy.GetGuidProperty(VSITEMID.ROOT, __VSHPROPID.VSHPROPID_ProjectIDGuid, projGuid))
                End If
            Catch ex As Exception When Common.ReportWithoutCrash(ex, "Failed to get project guid", NameOf(VBPackage))
                ' This is a non-vital function - ignore if we fail to get the GUID...
            End Try
            Return projGuid
        End Function

        ''' <summary>
        ''' Helper function for the project designer to get the last active tab for a project
        ''' </summary>
        ''' <param name="projectHierarchy"></param>
        ''' <returns>Last active tab number</returns>
        Public Function GetLastShownApplicationDesignerTab(projectHierarchy As IVsHierarchy) As Integer Implements IVBPackage.GetLastShownApplicationDesignerTab
            Dim value As Byte
            If _lastViewedProjectDesignerTab IsNot Nothing AndAlso _lastViewedProjectDesignerTab.TryGetValue(ProjectGUID(projectHierarchy), value) Then
                Return value
            Else
                ' Default to tab 0
                Return 0
            End If
        End Function

        ''' <summary>
        ''' Helper function for the project designer to scribble down the last active tab
        ''' </summary>
        ''' <param name="projectHierarchy">Hierarchy</param>
        ''' <param name="tab">Tab number</param>
        Public Sub SetLastShownApplicationDesignerTab(projectHierarchy As IVsHierarchy, tab As Integer) Implements IVBPackage.SetLastShownApplicationDesignerTab
            If _lastViewedProjectDesignerTab Is Nothing Then
                _lastViewedProjectDesignerTab = New Dictionary(Of Guid, Byte)
            End If
            ' Make sure we don't under/overflow...
            If tab > Byte.MaxValue OrElse tab < Byte.MinValue Then
                tab = 0
            End If
            _lastViewedProjectDesignerTab(ProjectGUID(projectHierarchy)) = CByte(tab)
        End Sub
#End Region

#Region "Clean up user.config files that may have been scattered around in a ZIP project"
        ''' <summary>
        ''' Helper class that monitors solution close events and cleans up any user.config files that
        ''' may have been by the Client Configuration API when the application is runs. 
        '''
        ''' The User.config files are created by the ClientConfig API, which is used by the runtime to
        ''' save user scoped settings created by the settings designer.
        ''' </summary>
        Private Class UserConfigCleaner
            Implements IVsSolutionEvents, IDisposable

            ' Our solution events cookie.
            Private _cookie As UInteger

            ' A handle to the IVsSolution service providing the events
            Private ReadOnly _solution As IVsSolution

            ' List of files to clean up when a ZIP project is discarded
            Private ReadOnly _filesToCleanUp As New List(Of String)

            ''' <summary>
            ''' Create a new instance of this class
            ''' </summary>
            ''' <param name="sp"></param>
            Public Sub New(sp As IServiceProvider)
                _solution = TryCast(sp.GetService(GetType(IVsSolution)), IVsSolution)
                Debug.Assert(_solution IsNot Nothing, "Failed to get IVsSolution - clean up of user config files in ZIP projects will not work...")
                If _solution IsNot Nothing Then
                    Dim hr As Integer = _solution.AdviseSolutionEvents(Me, _cookie)
#If DEBUG Then
                    Debug.Assert(Interop.NativeMethods.Succeeded(hr), "Failed to advise solution events - we won't clean up user config files in ZIP projects...")
#End If
                    If Not Interop.NativeMethods.Succeeded(hr) Then
                        _cookie = 0
                    End If
                End If
            End Sub

            ''' <summary>
            ''' Unadvise solution events
            ''' </summary>
            Private Sub UnadviseSolutionEvents()
                If _cookie <> 0 AndAlso _solution IsNot Nothing Then
                    Dim hr As Integer = _solution.UnadviseSolutionEvents(_cookie)
#If DEBUG Then
                    Debug.Assert(Interop.NativeMethods.Succeeded(hr), "Failed to unadvise solution events - we may leak..")
#End If
                    If Interop.NativeMethods.Succeeded(hr) Then
                        _cookie = 0
                    End If
                End If
            End Sub

            ''' <summary>
            ''' If we found any files to clean up in the OnBeforeCloseSolution, we better do so now that the
            ''' solution is actually closed...
            ''' </summary>
            ''' <param name="pUnkReserved"></param>
            Public Function OnAfterCloseSolution(pUnkReserved As Object) As Integer Implements IVsSolutionEvents.OnAfterCloseSolution
                SettingsDesigner.SettingsDesigner.DeleteFilesAndDirectories(_filesToCleanUp, Nothing)
                _filesToCleanUp.Clear()
                Instance.StickyProjectResourcePaths.Clear()
                Return Interop.NativeMethods.S_OK
            End Function

            ''' <summary>
            ''' Before the solution is closed, we check if this is a ZIP project, and if so make a list of all files
            ''' we'll delete when the solution is closed
            ''' </summary>
            ''' <param name="pUnkReserved"></param>
            Public Function OnBeforeCloseSolution(pUnkReserved As Object) As Integer Implements IVsSolutionEvents.OnBeforeCloseSolution
                Try
                    _filesToCleanUp.Clear()

                    Dim hr As Integer
                    ' Check if this is a deferred save project & there is only one project in the solution
                    Dim oBool As Object = Nothing
                    hr = _solution.GetProperty(__VSPROPID2.VSPROPID_DeferredSaveSolution, oBool)
#If DEBUG Then
                    Debug.Assert(Interop.NativeMethods.Succeeded(hr), "Failed to get VSPROPID_DeferredSaveSolution - we will not clean up user.config files...")
#End If
                    ErrorHandler.ThrowOnFailure(hr)

                    If oBool IsNot Nothing AndAlso CBool(oBool) Then
                        ' This is a ZIP project - let's find the projects and list all configuration files associated with it...
                        Dim projEnum As IEnumHierarchies = Nothing
                        ErrorHandler.ThrowOnFailure(_solution.GetProjectEnum(CUInt(__VSENUMPROJFLAGS.EPF_ALLINSOLUTION), Guid.Empty, projEnum))
                        Dim hiers(0) As IVsHierarchy
                        Dim fetched As UInteger

                        Do While projEnum.Next(CUInt(hiers.Length), hiers, fetched) = Interop.NativeMethods.S_OK AndAlso fetched > 0
                            If hiers(0) IsNot Nothing Then
                                Dim dirs As List(Of String) = SettingsDesigner.SettingsDesigner.FindUserConfigDirectories(hiers(0))
                                _filesToCleanUp.AddRange(SettingsDesigner.SettingsDesigner.FindUserConfigFiles(dirs))
                            End If
                        Loop
                    End If
                Catch ex As Exception When Common.ReportWithoutCrash(ex, "Failed when trying to clean up user.config files", NameOf(VBPackage))
                End Try
                Return Interop.NativeMethods.S_OK
            End Function



#Region "IVsSolutionEvents methods that simply return S_OK"

            Public Function OnAfterLoadProject(pStubHierarchy As IVsHierarchy, pRealHierarchy As IVsHierarchy) As Integer Implements IVsSolutionEvents.OnAfterLoadProject
                Return Interop.NativeMethods.S_OK
            End Function

            Public Function OnAfterOpenProject(pHierarchy As IVsHierarchy, fAdded As Integer) As Integer Implements IVsSolutionEvents.OnAfterOpenProject
                Return Interop.NativeMethods.S_OK
            End Function

            Public Function OnAfterOpenSolution(pUnkReserved As Object, fNewSolution As Integer) As Integer Implements IVsSolutionEvents.OnAfterOpenSolution
                Return Interop.NativeMethods.S_OK
            End Function

            Public Function OnBeforeCloseProject(pHierarchy As IVsHierarchy, fRemoved As Integer) As Integer Implements IVsSolutionEvents.OnBeforeCloseProject
                Instance.StickyProjectResourcePaths.Remove(ProjectGUID(pHierarchy))
                Return Interop.NativeMethods.S_OK
            End Function

            Public Function OnBeforeUnloadProject(pRealHierarchy As IVsHierarchy, pStubHierarchy As IVsHierarchy) As Integer Implements IVsSolutionEvents.OnBeforeUnloadProject
                Instance.StickyProjectResourcePaths.Remove(ProjectGUID(pRealHierarchy))
                Return Interop.NativeMethods.S_OK
            End Function

            Public Function OnQueryCloseProject(pHierarchy As IVsHierarchy, fRemoving As Integer, ByRef pfCancel As Integer) As Integer Implements IVsSolutionEvents.OnQueryCloseProject
                Return Interop.NativeMethods.S_OK
            End Function

            Public Function OnQueryCloseSolution(pUnkReserved As Object, ByRef pfCancel As Integer) As Integer Implements IVsSolutionEvents.OnQueryCloseSolution
                Return Interop.NativeMethods.S_OK
            End Function

            Public Function OnQueryUnloadProject(pRealHierarchy As IVsHierarchy, ByRef pfCancel As Integer) As Integer Implements IVsSolutionEvents.OnQueryUnloadProject
                Return Interop.NativeMethods.S_OK
            End Function
#End Region

            Private _disposed As Boolean = False

            ' IDisposable
            Private Overloads Sub Dispose(disposing As Boolean)
                If Not _disposed Then
                    If disposing Then
                        UnadviseSolutionEvents()
                    End If
                End If
                Debug.Assert(_cookie = 0, "We didn't unadvise solution events")
                _disposed = True
            End Sub

#Region " IDisposable Support "
            ' This code added by Visual Basic to correctly implement the disposable pattern.
            Public Overloads Sub Dispose() Implements IDisposable.Dispose
                ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
                Dispose(True)
                GC.SuppressFinalize(Me)
            End Sub

            Protected Overrides Sub Finalize()
                ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
                Dispose(False)
                MyBase.Finalize()
            End Sub
#End Region
        End Class 'UserConfigCleaner
#End Region

    End Class 'VBPackage

End Namespace
