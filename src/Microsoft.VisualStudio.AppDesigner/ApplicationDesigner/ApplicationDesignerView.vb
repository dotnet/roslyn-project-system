' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Imports EnvDTE

Imports Microsoft.Internal.Performance
Imports Microsoft.VisualStudio.Editors.AppDesInterop
Imports Microsoft.VisualStudio.Shell.Interop
Imports Microsoft.VisualStudio.Telemetry

Imports Common = Microsoft.VisualStudio.Editors.AppDesCommon
Imports OleInterop = Microsoft.VisualStudio.OLE.Interop
Imports VSITEMID = Microsoft.VisualStudio.Editors.VSITEMIDAPPDES

Namespace Microsoft.VisualStudio.Editors.ApplicationDesigner

    ''' <summary>
    ''' Main UI for Application Designer
    ''' </summary>
    ''' <remarks>
    '''   This class contains the actual top-level UI surface for the resource
    '''   editor.  It is created by ApplicationDesignerRootDesigner.
    '''</remarks>
    Public NotInheritable Class ApplicationDesignerView
        'Inherits UserControl
        Inherits ProjectDesignerTabControl
        Implements IServiceProvider
        Implements IVsSelectionEvents
        Implements IVsRunningDocTableEvents
        Implements IVsRunningDocTableEvents4
        Implements IPropertyPageSiteOwner

#Region " Windows Form Designer generated code "

        Public Sub New()
            MyBase.New()

            'This call is required by the Windows Form Designer.
            InitializeComponent()
        End Sub

        'Required by the Windows Form Designer
        Private ReadOnly _components As System.ComponentModel.IContainer

        'NOTE: The following procedure is required by the Windows Form Designer
        'It can be modified using the Windows Form Designer.  
        'Do not modify it using the code editor.

        <DebuggerStepThrough()> Private Sub InitializeComponent()
            '
            'ApplicationDesignerView
            '
            SuspendLayout()
            AutoScroll = False
            Name = "ApplicationDesignerView"
            ResumeLayout(False)
            PerformLayout()
        End Sub

#End Region

        ' explicitly hard-coding these strings since that's what QA's
        '   automation will look for in order to find our various tabs
        '
        Private Const PROP_PAGE_TAB_PREFIX As String = "PropPage_"
        Private Const RESOURCES_AUTOMATION_TAB_NAME As String = "Resources"
        Private Const SETTINGS_AUTOMATION_TAB_NAME As String = "Settings"

        'The designer panels hold the property pages and other designers
        Private _designerPanels As ApplicationDesignerPanel()
        Private _activePanelIndex As Integer = -1

        'App Designer data
        Private _serviceProvider As IServiceProvider
        Private _projectHierarchy As IVsHierarchy
        Private _projectFilePath As String 'Full path to the project filename
        Private _projectGuid As Guid

        '*** Project Property related data
        Private _projectObject As Object 'Project's browse object
        Private _dteProject As Project 'Project's DTE object
        Private _specialFiles As IVsProjectSpecialFiles

        'Set to true when the application designer window pane has completely initialized the application designer view
        Private _initializationComplete As Boolean

        '*** Monitor Selection
        Private _monitorSelection As IVsMonitorSelection
        Private _selectionEventsCookie As UInteger

        'Data shared by all pages hooked up to this project designer (available through GetService)
        Private _configurationState As PropPageDesigner.ConfigurationState

        'True if we have queued a delayed request to refresh the dirty indicators of any tab
        '  or the project designer.
        Private _refreshDirtyIndicatorsQueued As Boolean

        'The state of the project designer dirty indicator last time it was updated
        Private _lastProjectDesignerDirtyState As Boolean

        'True if SetFrameDirtyIndicator has already been called at least once
        Private _projectDesignerDirtyStateInitialized As Boolean

        'Cookie for IVsRunningDocumentTableEvents
        Private _rdtEventsCookie As UInteger

        ' Instance of the editors package
        Private _editorsPackage As IVBPackage

        'True if we're in the process of showing a tab
        Private _inShowTab As Boolean

        'True if we're already in the middle of showing a panel's WindowFrame
        Private _isInPanelWindowFrameShow As Boolean

        'True if it's okay for us to activate child panels on WM_SETFOCUS
        Private _okayToActivatePanelsOnFocus As Boolean

        ' Helper class to handle font change notifications...
        Private _fontChangeWatcher As Common.ShellUtil.FontChangeMonitor

        Private Const TelemetryEventRootPath As String = "vs/projectsystem/appdesigner/"
        Private Const TelemetryPropertyPrefix As String = "vs.projectsystem.appdesigner."
        ''' <summary>
        ''' Constructor for the ApplicationDesigner view
        ''' </summary>
        ''' <param name="serviceProvider">The service provider from the root designer.</param>
        Public Sub New(serviceProvider As IServiceProvider)
            MyBase.New()
            SuspendLayout()
            HostingPanel.SuspendLayout()

            SetSite(serviceProvider)

            'PERF: Set font before InitializeComponent so we don't cause unnecessary layouts (needs the site first)
            _fontChangeWatcher = New Common.ShellUtil.FontChangeMonitor(Me, Me, True)

            'This call is required by the Windows Form Designer.
            InitializeComponent()

#If DEBUG Then
            AddHandler HostingPanel.Layout, AddressOf HostingPanel_Layout
            AddHandler HostingPanel.SizeChanged, AddressOf HostingPanel_SizeChanged
#End If

            HostingPanel.ResumeLayout(False)
            ResumeLayout(False) 'Don't need to lay out yet - we'll do that at the end of AddTabs
        End Sub
        Public Sub InitView()
            Dim WindowFrame As IVsWindowFrame
            Dim Value As Object = Nothing
            Dim hr As Integer
            Common.Switches.TracePDFocus(TraceLevel.Warning, "ApplicationDesignerView.InitView()")
            Common.Switches.TracePDPerfBegin("ApplicationDesignerView.InitView")

            ' Whenever we open the project designer, we ping SQM...
            Common.TelemetryLogger.LogAppDesignerDefaultPageOpened()

            ' Store the vbpackage instance in utils to share within the assembly
            Common.VBPackageInstance = Package
            WindowFrame = Me.WindowFrame
            Debug.Assert(WindowFrame IsNot Nothing, "WindowFrame is nothing")
            If WindowFrame IsNot Nothing Then

                'Determine the hierarchy for the project that we need to show properties for.

                hr = WindowFrame.GetProperty(__VSFPROPID.VSFPROPID_Hierarchy, Value)
                If NativeMethods.Succeeded(hr) Then
                    Dim Hierarchy As IVsHierarchy = CType(Value, IVsHierarchy)
                    Dim ItemId As UInteger
                    WindowFrame.GetProperty(__VSFPROPID.VSFPROPID_ItemID, Value)
                    ItemId = Common.NoOverflowCUInt(Value)

                    'We now have the Hierarchy/ItemId that were stored in the windowframe.
                    '  But this hierarchy is not necessarily that of the project - in fact
                    '  it's generally the hierarchy of the solution (or outer project, in the
                    '  case of nested projects), with the itemid specifying which itemid in 
                    '  the solution corresponds to the project we need to view properties for.

                    'Use GetNestedHierarchy to get the hierarchy of the project within the
                    '  solution or outer project.

                    Dim NestedHierarchy As IntPtr
                    Dim NestedItemId As UInteger
                    If VSErrorHandler.Succeeded(Hierarchy.GetNestedHierarchy(ItemId, GetType(IVsHierarchy).GUID, NestedHierarchy, NestedItemId)) Then
                        'This is the project we want
                        Hierarchy = TryCast(Marshal.GetObjectForIUnknown(NestedHierarchy), IVsHierarchy)
                        Marshal.Release(NestedHierarchy)
                    End If

                    Debug.Assert(TypeOf Hierarchy Is IVsProject, "We didn't get a hierarchy to a project?")

                    _projectHierarchy = Hierarchy
                    _specialFiles = TryCast(_projectHierarchy, IVsProjectSpecialFiles)
                End If

                If _projectHierarchy Is Nothing Then
                    Debug.Fail("Failed to get project hierarchy")
                    Throw New Package.InternalException()
                End If
                Debug.Assert(_specialFiles IsNot Nothing, "Failed to get IVsProjectSpecialFiles for Hierarchy")

                Dim ExtObject As Object = Nothing
                hr = _projectHierarchy.GetProperty(VSITEMID.ROOT, __VSHPROPID.VSHPROPID_ExtObject, ExtObject)
                If NativeMethods.Succeeded(hr) Then
                    Dim DTE As DTE

                    Dim project = TryCast(ExtObject, Project)
                    If project IsNot Nothing Then
                        _dteProject = project
                        DTE = DTEProject.DTE
                    End If

                    'Set View title to allow finding designer in test suites
                    'Title should never be seen
                    Text = "AppDesigner+" & DTEProject.Name

                    _projectFilePath = DTEProject.FullName
                End If

                hr = _projectHierarchy.GetGuidProperty(VSITEMID.ROOT, __VSHPROPID.VSHPROPID_ProjectIDGuid, _projectGuid)
                If Not NativeMethods.Succeeded(hr) Then
                    _projectGuid = Guid.Empty
                End If

                If _dteProject Is Nothing Then
                    'Currently we require the DTE Project object.  In the future, if we are allowed in 
                    '  other project types, we'll need to ease this restriction.
                    Debug.Fail("Unable to retrieve DTE Project object")
                    Throw New Package.InternalException
                End If

                _monitorSelection = CType(GetService(GetType(IVsMonitorSelection)), IVsMonitorSelection)
                If _monitorSelection IsNot Nothing Then
                    _monitorSelection.AdviseSelectionEvents(Me, _selectionEventsCookie)
                End If

                'PERF: Before adding any page panels, we need to activate the main windowframe, so that it 
                '  can get its size/layout set up correctly.
                WindowFrame.Show()

                'Now add the tabs (but don't load them)
                AddTabs(GetPropertyPages())

                'We'll actually show the initial tab later (in OnInitializationComplete), don't need to do
                '  it here.

                Common.Switches.TracePDPerfEnd("ApplicationDesignerView.InitView")
            End If
        End Sub

        Public ReadOnly Property WindowFrame As IVsWindowFrame
            Get
                Return CType(GetService(GetType(IVsWindowFrame)), IVsWindowFrame)
            End Get
        End Property

        ''' <summary>
        ''' Retrieves the DTE project object associated with this project designer instance.
        ''' </summary>
        Public ReadOnly Property DTEProject As Project
            Get
                Return _dteProject
            End Get
        End Property

        Public ReadOnly Property SpecialFiles As IVsProjectSpecialFiles
            Get
                Return _specialFiles
            End Get
        End Property

        ''' <summary>
        ''' Instance of the loaded IVBPackage
        ''' </summary>
        ''' <remarks>Used to persist user data</remarks>
        Private ReadOnly Property Package As IVBPackage
            Get
                If _editorsPackage Is Nothing Then
                    Dim shell As IVsShell = DirectCast(GetService(GetType(IVsShell)), IVsShell)
                    Dim pPackage As IVsPackage = Nothing
                    If shell IsNot Nothing Then
                        Dim hr As Integer = shell.IsPackageLoaded(New Guid(My.Resources.Designer.VBPackage_GUID), pPackage)
                        Debug.Assert(NativeMethods.Succeeded(hr) AndAlso pPackage IsNot Nothing, "VB editors package not loaded?!?")
                    End If

                    _editorsPackage = TryCast(pPackage, IVBPackage)
                End If
                Return _editorsPackage
            End Get
        End Property

        ''' <summary>
        ''' Get/set the last viewed tab of for this application page...
        ''' </summary>
        Private Property LastShownTab As Integer
            Get
                Dim editorsPackage As IVBPackage = Package
                If editorsPackage IsNot Nothing Then
                    Dim result As Integer = editorsPackage.GetLastShownApplicationDesignerTab(_projectHierarchy)
                    If result >= 0 AndAlso result < _designerPanels.Length Then
                        Return result
                    End If
                End If
                Return 0
            End Get
            Set
                Dim editorsPackage As IVBPackage = Package
                If editorsPackage IsNot Nothing Then
                    editorsPackage.SetLastShownApplicationDesignerTab(_projectHierarchy, value)
                End If
            End Set
        End Property

        ''' <summary>
        ''' Should be called to let the project designer know it's shutting down and should no longer try
        '''   to activate child pages
        ''' </summary>
        Public Sub NotifyShuttingDown()
            Common.Switches.TracePDFocus(TraceLevel.Info, "NotifyShuttingDown")
            _okayToActivatePanelsOnFocus = False
        End Sub

        ''' <summary>
        ''' Helper to determine if the docdata (designated by DocCookie) is dirty
        ''' </summary>
        ''' <param name="DocCookie"></param>
        ''' <param name="Hierarchy"></param>
        ''' <param name="ItemId"></param>
        ''' <remarks>Used by view to prompt for saving changes</remarks>
        Private Function IsDocDataDirty(DocCookie As UInteger, ByRef Hierarchy As IVsHierarchy, ByRef ItemId As UInteger) As Boolean
            Dim rdt As IVsRunningDocumentTable = TryCast(GetService(GetType(IVsRunningDocumentTable)), IVsRunningDocumentTable)
            Dim hr As Integer
            Dim flags, readLocks, editLocks As UInteger
            Dim fileName As String = Nothing
            Dim localPunk As IntPtr = IntPtr.Zero

            If rdt IsNot Nothing Then
                Try
                    hr = rdt.GetDocumentInfo(DocCookie, flags, readLocks, editLocks, fileName, Hierarchy, ItemId, localPunk)
                    If NativeMethods.Succeeded(hr) Then
                        Dim obj As Object
                        Debug.Assert(localPunk <> IntPtr.Zero, "IUnknown for document is NULL")
                        obj = Marshal.GetObjectForIUnknown(localPunk)
                        If TypeOf obj Is IVsPersistDocData Then
                            Dim dirty As Integer
                            If VSErrorHandler.Succeeded(TryCast(obj, IVsPersistDocData).IsDocDataDirty(dirty)) Then
                                Return dirty <> 0
                            End If
                        ElseIf TypeOf obj Is IPersistFileFormat Then
                            Dim dirty As Integer
                            If VSErrorHandler.Succeeded(TryCast(obj, IPersistFileFormat).IsDirty(dirty)) Then
                                Return dirty <> 0
                            End If
                        Else
                            Debug.Fail("Unable to determine if DocData is dirty - doesn't support an interface we recognize")
                        End If
                    End If
                Finally
                    If localPunk <> IntPtr.Zero Then
                        Marshal.Release(localPunk)
                    End If
                End Try
            End If
            Return False
        End Function

        ''' <summary>
        ''' Populates the list of documents based on flags argument
        ''' </summary>
        ''' <param name="flags"></param>
        ''' <remarks>Used to build table of documents to save</remarks>
        Public ReadOnly Property GetSaveTreeItems(flags As __VSRDTSAVEOPTIONS) As VSSAVETREEITEM()
            Get
                Dim items As VSSAVETREEITEM() = New VSSAVETREEITEM(_designerPanels.Length - 1) {}
                Dim Count As Integer
                Dim DocCookie As UInteger
                Dim Hierarchy As IVsHierarchy = Nothing
                Dim ItemId As UInteger

                If _designerPanels IsNot Nothing Then
                    For Index As Integer = 0 To _designerPanels.Length - 1
                        'If the designer was opened, then add it to the list for saving
                        If _designerPanels(Index) IsNot Nothing AndAlso
                            _designerPanels(Index).VsWindowFrame IsNot Nothing Then
                            DocCookie = _designerPanels(Index).DocCookie
                            If IsDocDataDirty(DocCookie, Hierarchy, ItemId) Then
                                If Count >= items.Length Then
                                    ReDim Preserve items(Count)
                                End If
                                items(Count).docCookie = DocCookie
                                items(Count).grfSave = CUInt(flags)
                                items(Count).itemid = ItemId
                                items(Count).pHier = Hierarchy
                                Count += 1
                            End If
                        End If

#If False Then 'This interface is currently disabled, no clients using it, see PropPage.vb
                        'Property pages may have DocDatas that should be included in the list
                        If TypeOf m_DesignerPanels(Index).DocView Is PropPageDesigner.PropPageDesignerView Then
                            Dim PropPage As OleInterop.IPropertyPage = TryCast(m_DesignerPanels(Index).DocView, PropPageDesigner.PropPageDesignerView).PropPage
                            If TypeOf PropPage Is PropertyPages.IVsDocDataContainer Then
                                Dim DocCookies As UInteger()
                                DocCookies = TryCast(PropPage, PropertyPages.IVsDocDataContainer).GetDocDataCookies()
                                If DocCookies IsNot Nothing AndAlso DocCookies.Length > 0 Then
                                    For Each DocCookie In DocCookies
                                        If IsDocDataDirty(DocCookie, Hierarchy, ItemId) Then
                                            If Count >= items.Length Then
                                                ReDim Preserve items(Count)
                                            End If
                                            items(Count).docCookie = DocCookie
                                            items(Count).grfSave = CUInt(flags)
                                            items(Count).itemid = ItemId
                                            items(Count).pHier = Hierarchy
                                            Count += 1
                                        End If
                                    Next
                                End If
                            End If
                        End If
#End If
                    Next
                End If

                ReDim Preserve items(Count - 1)
                Return items
            End Get
        End Property

        'UserControl overrides dispose to clean up the component list.
        Protected Overloads Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
#If DEBUG Then
                RemoveHandler HostingPanel.Layout, AddressOf HostingPanel_Layout
#End If
                If _monitorSelection IsNot Nothing AndAlso _selectionEventsCookie <> 0 Then
                    _monitorSelection.UnadviseSelectionEvents(_selectionEventsCookie)
                    _monitorSelection = Nothing
                    _selectionEventsCookie = 0
                End If
                UnadviseRunningDocTableEvents()

                If _fontChangeWatcher IsNot Nothing Then
                    _fontChangeWatcher.Dispose()
                    _fontChangeWatcher = Nothing
                End If

                If _components IsNot Nothing Then
                    _components.Dispose()
                End If

                If _designerPanels IsNot Nothing Then
                    For Index As Integer = 0 To _designerPanels.Length - 1
                        If _designerPanels(Index) IsNot Nothing Then
                            Try
                                Dim Panel As ApplicationDesignerPanel = _designerPanels(Index)
                                _designerPanels(Index) = Nothing
                                Panel.Dispose()
                            Catch ex As Exception When Common.ReportWithoutCrash(ex, "Exception trying to dispose ApplicationDesignerPanel", NameOf(ApplicationDesignerView))
                            End Try
                        End If
                    Next
                End If

                If _configurationState IsNot Nothing Then
                    _configurationState.Dispose()
                    _configurationState = Nothing
                End If
            End If
            MyBase.Dispose(disposing)
        End Sub

        ''' <summary>
        ''' Creates a set of all property pages that the project wants us to display.
        ''' Does *not* load them now, but waits to load them on demand.
        ''' </summary>
        ''' <returns>An array of PropertyPageInfo with the loaded property page information.</returns>
        Public Function GetPropertyPages() As PropertyPageInfo()
            Dim LocalRegistry As ILocalRegistry
            LocalRegistry = CType(GetService(GetType(ILocalRegistry)), ILocalRegistry)

            Debug.Assert(LocalRegistry IsNot Nothing, "Unabled to obtain ILocalRegistry")

            Dim ConfigPageGuids As Guid() = GetPageGuids(GetActiveConfigBrowseObject())
            Dim CommonPageGuids As Guid() = GetPageGuids(GetProjectBrowseObject())

#If DEBUG Then
            'Add the VB WPF Application property page to all projects, even non-WPF projects.  This allows for debugging
            '  this page without the new WPF flavor
            If Common.Switches.PDAddVBWPFApplicationPageToAllProjects.Enabled Then
                Dim commonList As New List(Of Guid)
                commonList.AddRange(CommonPageGuids)
                Dim wpfPage As Guid = New Guid(My.Resources.Designer.WPFApplicationWithMyPropPageComClass_GUID)
                commonList.Add(wpfPage)
                CommonPageGuids = commonList.ToArray()
            End If
#End If

            'Create a combined array list of the property page guids
            Dim PropertyPages(CommonPageGuids.Length + ConfigPageGuids.Length - 1) As PropertyPageInfo

            For Index As Integer = 0 To PropertyPages.Length - 1
                Dim PageGuid As Guid
                Dim IsConfigPage As Boolean
                With PropertyPages(Index)
                    If Index < CommonPageGuids.Length Then
                        PageGuid = CommonPageGuids(Index)
                        IsConfigPage = False
                    Else
                        PageGuid = ConfigPageGuids(Index - CommonPageGuids.Length)
                        IsConfigPage = True
                    End If
                End With
                PropertyPages(Index) = New PropertyPageInfo(Me, PageGuid, IsConfigPage)
            Next

            Return PropertyPages
        End Function

#If 0 Then
        ''' <summary>
        ''' Get the max property page size based on the reported page infos
        ''' </summary>
        Public ReadOnly Property GetMaxPropPageSize() As Drawing.Size
            Get
                Dim MaxSize As Drawing.Size
                Dim OleSize As OleInterop.SIZE

                If m_DesignerPanels IsNot Nothing Then
                    For Index As Integer = 0 To m_DesignerPanels.Length - 1
                        If m_DesignerPanels(Index) Is Nothing AndAlso m_DesignerPanels(Index).IsPropertyPage Then
                            OleSize = m_DesignerPanels(Index).PropertyPageInfo.Info.SIZE
                            If OleSize.cx > MaxSize.Width Then
                                MaxSize.Width = OleSize.cx
                            End If
                            If OleSize.cy > MaxSize.Height Then
                                MaxSize.Height = OleSize.cy
                            End If
                        End If
                    Next
                End If
                Return MaxSize
            End Get
        End Property
#End If

        Private Function GetProjectBrowseObject() As Object
            If _projectObject Is Nothing Then
                Dim BrowseObject As Object = Nothing
                VSErrorHandler.ThrowOnFailure(_projectHierarchy.GetProperty(VSITEMID.ROOT, __VSHPROPID.VSHPROPID_BrowseObject, BrowseObject))
                _projectObject = BrowseObject
            End If
            Return _projectObject
        End Function

        Private _vsCfgProvider As IVsCfgProvider2
        Private ReadOnly Property VsCfgProvider As IVsCfgProvider2
            Get
                If _vsCfgProvider Is Nothing Then
                    Dim Value As Object = Nothing

                    VSErrorHandler.ThrowOnFailure(_projectHierarchy.GetProperty(VSITEMID.ROOT, __VSHPROPID.VSHPROPID_ConfigurationProvider, Value))

                    _vsCfgProvider = CType(Value, IVsCfgProvider2)
                End If
                Return _vsCfgProvider
            End Get
        End Property

        ''' <summary>
        ''' Obtain the current Config browse object from the project hierarchy
        ''' </summary>
        ''' <returns>The browse object for the currently selected configuration.</returns>
        Private Function GetActiveConfigBrowseObject() As Object
            Return Common.DTEUtils.GetActiveConfiguration(DTEProject, VsCfgProvider)
        End Function

        Public Property ActiveView As Guid
            Get
                If _activePanelIndex < 0 OrElse _activePanelIndex >= _designerPanels.Length OrElse _designerPanels(_activePanelIndex) Is Nothing Then
                    Return Guid.Empty
                End If
                Dim Panel As ApplicationDesignerPanel = _designerPanels(_activePanelIndex)
                'Use ActualGuid so that for property pages we return the property page's guid 
                '  instead of the PropPageDesigner's guid
                Return Panel.ActualGuid
            End Get

            Set
                Common.Switches.TracePDFocus(TraceLevel.Info, "ApplicationDesignerView: set_ActiveView")
                'Find the guid and switch to that tab
                'Keep the current tab if guid not found
                For Index As Integer = 0 To _designerPanels.Length - 1
                    'If this is a property page, check the property page guid (thus using ActualGuid)
                    If Value.Equals(_designerPanels(Index).ActualGuid) Then
                        ShowTab(Index)
                        Return
                    End If
                Next

                'Guid not found - keep current tab
                ShowTab(_activePanelIndex)
            End Set
        End Property

        ''' <summary>
        ''' Determines if the given tab should be added to the designer, and if so, returns
        '''   the ProjectItem corresponding to it.
        ''' </summary>
        ''' <param name="fileId">The FileId to use as a parameter to IVsProjectSpecialFiles</param>
        ''' <param name="tabSupported">[Out] True if the given tab is supported by the project</param>
        ''' <param name="fileExists">[Out] True if the given tab's file actually exists currently.  Always false if Not TabSupported.</param>
        ''' <param name="fullPathToProjectItem">[Out] The full path to the given tab's file.  If TabSupported is True but FileExists is False, this value indicates the preferred file and location for the project for this special file.</param>
        Private Sub CheckIfTabSupported(fileId As Integer, ByRef tabSupported As Boolean, ByRef fileExists As Boolean, ByRef fullPathToProjectItem As String)
            tabSupported = False
            fileExists = False
            fullPathToProjectItem = Nothing

            If _specialFiles Is Nothing Then
                Debug.Fail("IVsProjectSpecialFiles is Nothing - can't look for the given tab's file - tab will be hidden")
                Return
            End If

            Dim ItemId As UInteger
            Dim SpecialFilePath As String = Nothing
            Dim hr As Integer = _specialFiles.GetFile(fileId, CUInt(__PSFFLAGS.PSFF_FullPath), ItemId, SpecialFilePath)
            If VSErrorHandler.Succeeded(hr) Then
                'Yes, the tab is supported
                tabSupported = True
                fullPathToProjectItem = SpecialFilePath

                'Does the file actually exist (both in the project and on disk)?
                If ItemId <> VSITEMID.NIL AndAlso SpecialFilePath <> "" AndAlso IO.File.Exists(SpecialFilePath) Then
                    'Yes, the file exists
                    fileExists = True
                End If
            End If
        End Sub

        ''' <summary>
        ''' Adds the tab buttons for the App Designer
        ''' </summary>
        ''' <param name="PropertyPages">The list of property pages to display</param>
        Private Sub AddTabs(PropertyPages() As PropertyPageInfo)
            SuspendLayout()
            HostingPanel.SuspendLayout()

            HostingPanel.Controls.Clear()
            ClearTabs()

            'Categories are Common property pages + Config Property pages + Resources + Settings
            Dim tabCount As Integer
            Dim AppDesignerItems As New ArrayList '(Of String [path + filename]) 'Resources, Settings, etc (not property pages)

            'Add the resources tab
            Dim resourcesTabSupported, defaultResourcesExist As Boolean
            Dim defaultResourcesPath As String = Nothing
            CheckIfTabSupported(__PSFFILEID2.PSFFILEID_AssemblyResource, resourcesTabSupported, defaultResourcesExist, defaultResourcesPath)
            If resourcesTabSupported Then
                AppDesignerItems.Add(defaultResourcesPath)
            End If

            'Add the settings tab
            Dim defaultSettingsSupported, defaultSettingsExist As Boolean
            Dim defaultSettingsPath As String = Nothing
            CheckIfTabSupported(__PSFFILEID2.PSFFILEID_AppSettings, defaultSettingsSupported, defaultSettingsExist, defaultSettingsPath)
            If defaultSettingsSupported Then
                AppDesignerItems.Add(defaultSettingsPath)
            End If

            'Total tab count
            tabCount = PropertyPages.Length + AppDesignerItems.Count 'Resource Designer + Settings Designer + property pages

            _designerPanels = New ApplicationDesignerPanel(tabCount - 1) {}
            Dim HasResourcesPage As Boolean = False
            Dim HasSettingsPage As Boolean = False

            'Create the designer panels
            For Index As Integer = 0 To tabCount - 1

                Dim DesignerPanel As ApplicationDesignerPanel
                If Index < PropertyPages.Length Then
                    'This is a property page
                    Debug.Assert(PropertyPages(Index) IsNot Nothing)
                    DesignerPanel = New ApplicationDesignerPanel(Me, _projectHierarchy, CUInt(Index), PropertyPages(Index))
                Else
                    DesignerPanel = New ApplicationDesignerPanel(Me, _projectHierarchy, CUInt(Index))
                End If

                With DesignerPanel
                    .SuspendLayout()
                    .Dock = DockStyle.Fill
                    .Location = New Drawing.Point(0, 0)
                    .Name = "DesignerPanel" & Index
                    .Size = New Drawing.Size(555, 392)
                    .TabIndex = 1
                    .Dock = DockStyle.Fill
                    .Font = HostingPanel.Font 'PERF: Prepopulating with the font means you reduce the number of OnFontChanged that occur when child panels are added/removed from the parent
                    .Visible = False 'Don't make visible until that particular tab is selected

                    'Note: 
                    ' tab-titles are display names the user sees, TabAutomationNames are
                    '   for QA automation (they should not be localized)
                    '

                    If .PropertyPageInfo IsNot Nothing Then
                        'It must be a property page tab
                        .MkDocument = _projectFilePath & ";" & PropertyPages(Index).Guid.ToString()
                        .PhysicalView = PropertyPages(Index).Guid.ToString()
                        .EditFlags = CUInt(_VSRDTFLAGS.RDT_VirtualDocument Or _VSRDTFLAGS.RDT_DontAddToMRU)

                        'PERF: This property call will attempt to retrieve a cached version of the title 
                        '  to avoid having to instantiate the COM object for the property page until
                        '  the user actually browses to that page.
                        .EditorCaption = PropertyPages(Index).Title

                        .TabTitle = .EditorCaption
                        .TabAutomationName = PROP_PAGE_TAB_PREFIX & PropertyPages(Index).Guid.ToString("N")

                        ' Load fails are reported earlier
                        If (.PropertyPageInfo.LoadException Is Nothing) Then
                            Dim SpecialTabsTelemetryEvent As TelemetryEvent = New TelemetryEvent(TelemetryEventRootPath + "TabInfo")
                            SpecialTabsTelemetryEvent.Properties(TelemetryPropertyPrefix + "TabInfo.TabTitle") = New TelemetryPiiProperty(.EditorCaption)
                            SpecialTabsTelemetryEvent.Properties(TelemetryPropertyPrefix + "TabInfo.GUID") = PropertyPages(Index).Guid.ToString("B")
                            SpecialTabsTelemetryEvent.Properties(TelemetryPropertyPrefix + "Project.Extension") = IO.Path.GetExtension(_projectFilePath)
                            SpecialTabsTelemetryEvent.Properties(TelemetryPropertyPrefix + "Project.GUID") = _projectGuid.ToString("B")
                            TelemetryService.DefaultSession.PostEvent(SpecialTabsTelemetryEvent)
                        End If

                    Else
                        Dim FileName As String = DirectCast(AppDesignerItems(Index - PropertyPages.Length), String)

                        .EditFlags = CUInt(_VSRDTFLAGS.RDT_DontAddToMRU)
                        If String.Equals(VisualBasic.Right(FileName, 5), ".resx", StringComparison.OrdinalIgnoreCase) Then

                            HasResourcesPage = True

                            'Add .resx file with a known editor so user config cannot change
                            .EditorGuid = New Guid(My.Resources.Designer.ResourceEditorFactory_GUID)
                            .EditorCaption = My.Resources.Designer.APPDES_ResourceTabTitle
                            .TabAutomationName = RESOURCES_AUTOMATION_TAB_NAME

                            'If the resx file doesn't actually exist yet, we have to display the "Click here
                            '  to create it" message instead of the actual editor.
                            If defaultResourcesExist Then
                                'We can't set .MkDocument directly from FileName, because the FileName returned by 
                                '  IVsProjectSpecialFile might change before we try to open it (e.g., when a ZIP
                                '  project is saved).  Instead, delay fetching of the filename via 
                                '  SpecialFileCustomDocumentMonikerProvider).
                                .CustomMkDocumentProvider = New SpecialFileCustomDocumentMonikerProvider(Me, __PSFFILEID2.PSFFILEID_AssemblyResource)
                            Else
                                .CustomViewProvider = New SpecialFileCustomViewProvider(Me, DesignerPanel, __PSFFILEID2.PSFFILEID_AssemblyResource, My.Resources.Designer.APPDES_ClickHereCreateResx)
                            End If
                        ElseIf String.Equals(VisualBasic.Right(FileName, 9), ".settings", StringComparison.OrdinalIgnoreCase) Then

                            HasSettingsPage = True

                            'Add .settings file with a known editor so user config cannot change
                            .EditorGuid = New Guid(My.Resources.Designer.SettingsDesignerEditorFactory_GUID)
                            .EditorCaption = My.Resources.Designer.APPDES_SettingsTabTitle
                            .TabAutomationName = SETTINGS_AUTOMATION_TAB_NAME

                            'If the settings file doesn't actually exist yet, we have to display the "Click here
                            '  to create it" message instead of the actual editor.
                            If defaultSettingsExist Then
                                'We can't set .MkDocument directly from FileName, because the FileName returned by 
                                '  IVsProjectSpecialFile might change before we try to open it (e.g., when a ZIP
                                '  project is saved).  Instead, delay fetching of the filename via 
                                '  SpecialFileCustomDocumentMonikerProvider).
                                .CustomMkDocumentProvider = New SpecialFileCustomDocumentMonikerProvider(Me, __PSFFILEID2.PSFFILEID_AppSettings)
                            Else
                                .CustomViewProvider = New SpecialFileCustomViewProvider(Me, DesignerPanel, __PSFFILEID2.PSFFILEID_AppSettings, My.Resources.Designer.APPDES_ClickHereCreateSettings)
                            End If
                        Else
                            Debug.Fail("Unexpected file in list of intended tabs")
                        End If

                        .TabTitle = .EditorCaption
                    End If

                    Debug.Assert(.TabTitle <> "" OrElse (.PropertyPageInfo IsNot Nothing AndAlso .PropertyPageInfo.LoadException IsNot Nothing), "Why is the tab title text empty?")
                    Debug.Assert(.TabAutomationName <> "" OrElse (.PropertyPageInfo IsNot Nothing AndAlso .PropertyPageInfo.LoadException IsNot Nothing), "Why is the tab automation name text empty?")

                    .ResumeLayout(False) 'Controls.Add below will call PerformLayout, so no need to do it here.

                    'Don't actually add the panel to the HostingPanel yet...
                    _designerPanels(Index) = DesignerPanel
                End With
            Next

            Dim TelemetryEvent As TelemetryEvent = New TelemetryEvent(TelemetryEventRootPath + "TabInfo/SpecialTabs")
            TelemetryEvent.Properties(TelemetryPropertyPrefix + "Project.Extension") = IO.Path.GetExtension(_projectFilePath)
            TelemetryEvent.Properties(TelemetryPropertyPrefix + "Project.Guid") = _projectGuid.ToString("B")
            TelemetryEvent.Properties(TelemetryPropertyPrefix + "TabInfo.HasResourcesPage") = HasResourcesPage
            TelemetryEvent.Properties(TelemetryPropertyPrefix + "TabInfo.HasSettingsPage") = HasSettingsPage
            TelemetryService.DefaultSession.PostEvent(TelemetryEvent)

            'Order the tabs
            OrderTabs(_designerPanels)

            'PERF: Tell the tab control how many panels there are and what their titles are before
            '  adding the AppicationDesignerPanels, so that the final size of the HostingPanel is
            '  known.
            For i As Integer = 0 To _designerPanels.GetUpperBound(0)
                AddTab(_designerPanels(i).TabTitle, _designerPanels(i).TabAutomationName)
            Next

            'Now that all the tab titles have been figured out, we can go ahead and add all the 
            '  panels to the HostingPanel's control array.  This will cause PerformLayout on all
            '  the panels.  We couldn't do it before adding the tab titles because they affect the 
            '  size of the HostingPanel.  Now we should have a stable size for the hosting panel.
            For Index As Integer = 0 To tabCount - 1
                Dim DesignerPanel As ApplicationDesignerPanel = _designerPanels(Index)
                HostingPanel.Controls.Add(DesignerPanel)
            Next

            HostingPanel.ResumeLayout(False)
            ResumeLayout(True)
        End Sub

        ''' <summary>
        ''' Re-orders the given set of designer panels according to what we want.
        ''' </summary>
        ''' <param name="DesignerPanels">List of designer panels to be re-ordered in-place.</param>
        ''' <remarks>
        ''' Recognized tabs will be placed in a specific order.  All others will be placed at the end,
        '''   in the order passed in to this method.
        ''' </remarks>
        Private Sub OrderTabs(DesignerPanels() As ApplicationDesignerPanel)

            'A default list of known editor guids and the order we want when they appear.  We only
            '  use this list if we can't get the order from the IVsHierarchy for some reason.
            Dim DefaultDesiredOrder() As Guid = {
                Common.KnownPropertyPageGuids.GuidApplicationPage_VB,
                Common.KnownPropertyPageGuids.GuidApplicationPage_CS,
                Common.KnownPropertyPageGuids.GuidApplicationPage_JS,
                Common.KnownPropertyPageGuids.GuidCompilePage_VB,
                Common.KnownPropertyPageGuids.GuidBuildPage_CS,
                Common.KnownPropertyPageGuids.GuidBuildPage_JS,
                Common.KnownPropertyPageGuids.GuidBuildEventsPage,
                Common.KnownPropertyPageGuids.GuidDebugPage,
                Common.KnownPropertyPageGuids.GuidDebugPage_VSD,
                Common.KnownPropertyPageGuids.GuidReferencesPage_VB,
                New Guid(My.Resources.Designer.SettingsDesignerEditorFactory_GUID),
                Common.KnownPropertyPageGuids.GuidServicesPropPage,
                New Guid(My.Resources.Designer.ResourceEditorFactory_GUID),
                Common.KnownPropertyPageGuids.GuidReferencePathsPage,
                Common.KnownPropertyPageGuids.GuidSigningPage,
                Common.KnownPropertyPageGuids.GuidSecurityPage,
                Common.KnownPropertyPageGuids.GuidPublishPage
            }
            Dim DesiredOrder() As Guid = DefaultDesiredOrder

            'Get the requested ordering of project designer pages from the IVsHierarchy.
            Dim CLSIDListObject As Object = Nothing
            Dim hr As Integer = _projectHierarchy.GetProperty(VSITEMID.ROOT, __VSHPROPID2.VSHPROPID_PriorityPropertyPagesCLSIDList, CLSIDListObject)
            If VSErrorHandler.Succeeded(hr) AndAlso TypeOf CLSIDListObject Is String Then
                Dim CLSIDListString As String = DirectCast(CLSIDListObject, String)
                Dim CLSIDList As New List(Of Guid)
                For Each CLSID As String In CLSIDListString.Split(";"c)
                    If CLSID <> "" Then
                        CLSID = CLSID.Trim()
                        Try
                            Dim Guid As New Guid(CLSID)
                            CLSIDList.Add(Guid)
                        Catch ex As FormatException When Common.ReportWithoutCrash(ex, "VSHPROPID_PriorityPropertyPagesCLSIDList returned a string in a bad format", NameOf(ApplicationDesignerView))
                        End Try
                    End If
                Next

                DesiredOrder = CLSIDList.ToArray()
                Debug.Assert(DesiredOrder.Length > 0, "Got an empty list from VSHPROPID_PriorityPropertyPagesCLSIDList")
            Else
                Debug.Fail("Unable to get VSHPROPID_PriorityPropertyPagesCLSIDList from hierarchy")
            End If

            Dim OldOrder As New ArrayList(DesignerPanels.Length) '(Of ApplicationDesignerPanel)
            Dim NewOrder As New ArrayList(DesignerPanels.Length) '(Of ApplicationDesignerPanel)

            'Initialize OldOrder
            OldOrder.AddRange(DesignerPanels)

            'First in the new order come the pages found in DesiredOrder, in exactly that order
            For Each Guid As Guid In DesiredOrder
                For PanelIndex As Integer = 0 To OldOrder.Count - 1
                    Dim Panel As ApplicationDesignerPanel = DirectCast(OldOrder(PanelIndex), ApplicationDesignerPanel)
                    Debug.Assert(Panel IsNot Nothing)
                    If Panel IsNot Nothing AndAlso Panel.ActualGuid.Equals(Guid) Then
                        'Found one in the preferred order.
                        NewOrder.Add(Panel)
                        OldOrder.RemoveAt(PanelIndex)
                        Exit For
                    End If
                Next
            Next

            'At the end of the list, add all other panels, in the order in which they were passed in
            '  to this function
            For Each Panel As ApplicationDesignerPanel In OldOrder
                NewOrder.Add(Panel)
            Next

            'Re-order the original list
            Debug.Assert(NewOrder.Count = DesignerPanels.Length, "Ordering didn't work")
            For i As Integer = 0 To NewOrder.Count - 1
                DesignerPanels(i) = DirectCast(NewOrder(i), ApplicationDesignerPanel)
            Next
        End Sub

        ''' <summary>
        ''' Gets the guid list from the specified object
        ''' </summary>
        ''' <param name="BrowseObject"></param>
        Private Shared Function GetPageGuids(BrowseObject As Object) As Guid()
            Dim vsSpecifyProjectDesignerPages = TryCast(BrowseObject, IVsSpecifyProjectDesignerPages)
            If vsSpecifyProjectDesignerPages IsNot Nothing Then
                Dim CauuidPages() As OleInterop.CAUUID = New OleInterop.CAUUID(1) {}
                Try
                    vsSpecifyProjectDesignerPages.GetProjectDesignerPages(CauuidPages)
                    Return CAUUIDMarshaler.GetData(CauuidPages(0))
                Finally
                    If Not CauuidPages(0).pElems.Equals(IntPtr.Zero) Then
                        Marshal.FreeCoTaskMem(CauuidPages(0).pElems)
                    End If
                End Try
            End If
            Return Array.Empty(Of Guid)
        End Function

        Private Sub SetSite(serviceProvider As IServiceProvider)
            _serviceProvider = serviceProvider

            'Set the provider into the base tab control so it can get access to fonts and colors
            MyBase.ServiceProvider = _serviceProvider
        End Sub

        Public Shadows Function GetService(ServiceType As Type) As Object Implements IServiceProvider.GetService, IPropertyPageSiteOwner.GetService
            Dim Service As Object

            If ServiceType Is GetType(PropPageDesigner.ConfigurationState) Then
                If _configurationState Is Nothing Then
                    _configurationState = New PropPageDesigner.ConfigurationState(_dteProject, _projectHierarchy, Me)
                End If
                Return _configurationState
            End If

            If ServiceType Is GetType(ApplicationDesignerView) Then
                'Allows the PropPageDesignerView to access the ApplicationDesignerView
                Return Me
            End If

            Service = _serviceProvider.GetService(ServiceType)
            Return Service
        End Function

        ''' <summary>
        ''' Called by designer when changes need to be persisted
        ''' </summary>
        ''' <returns>Return true if success </returns>
        Public Function CommitAnyPendingChanges() As Boolean
            If _activePanelIndex >= 0 Then
                Dim currentPanel As ApplicationDesignerPanel = _designerPanels(_activePanelIndex)
                If currentPanel IsNot Nothing Then
                    Return currentPanel.CommitPendingEdit()
                End If
            End If
            Return True
        End Function

        Friend Sub AppDesignerAlreadyLoaded()
            Dim ActivePanel As ApplicationDesignerPanel = _designerPanels(_activePanelIndex)
            Common.TelemetryLogger.LogAppDesignerPageOpened(ActivePanel.ActualGuid, ActivePanel.TabTitle, True)
        End Sub

        ''' <summary>
        ''' Show the requested tab
        ''' </summary>
        ''' <param name="Index">Index of Designer panel to show</param>
        ''' <param name="ForceShow">Forces the Show code to go through, even if the current panel is the same as the one requested.</param>
        Private Sub ShowTab(Index As Integer, Optional ForceShow As Boolean = False, Optional ForceActivate As Boolean = False)

            Common.Switches.TracePDFocus(TraceLevel.Warning, "ApplicationDesignerView.ShowTab(" & Index & ")")
            If _inShowTab Then
                Common.Switches.TracePDFocus(TraceLevel.Warning, " ...Already in ShowTab")
                Exit Sub
            End If

            _inShowTab = True
            Try
                If _activePanelIndex = Index AndAlso Not ForceShow Then
                    'PERF: PERFORMANCE SENSITIVE CODE: No need to go through the designer creation again if we're already on the
                    '  correct page.
                    Common.Switches.TracePDFocus(TraceLevel.Warning, "  ... Ignoring because Index is already " & Index & " and ForceShow=False")
                    Return
                End If

                ' If current Page can not commit pending changes, we shouldn't go away (but only if we're actually changing tabs)
                If (Index <> _activePanelIndex) AndAlso Not CommitAnyPendingChanges() Then
                    Common.Switches.TracePDFocus(TraceLevel.Warning, "  ... Ignoring because CommitAnyPendingChanges returned False")
                    Return
                End If

                Common.Switches.TracePDPerfBegin("ApplicationDesignerView.ShowTab")
                Common.Switches.TracePDFocus(TraceLevel.Error, "CodeMarker: perfMSVSEditorsShowTabBegin")
                Common.Switches.TracePDPerf("CodeMarker: perfMSVSEditorsShowTabBegin")
                CodeMarkers.Instance.CodeMarker(RoslynCodeMarkerEvent.PerfMSVSEditorsShowTabBegin)

                Dim NewCurrentPanel As ApplicationDesignerPanel = _designerPanels(Index)
                Dim ErrorMessage As String = Nothing
                Dim DesignerAlreadyShownOnCreation As Boolean = False

#If DEBUG Then
                NewCurrentPanel.m_Debug_cWindowFrameShow = 0
                NewCurrentPanel.m_Debug_cWindowFrameBoundsUpdated = 0
#End If
                Try
                    If _activePanelIndex <> Index Then
                        LastShownTab = Index
                        Common.TelemetryLogger.LogAppDesignerPageOpened(NewCurrentPanel.ActualGuid, NewCurrentPanel.TabTitle)
                    End If

                    _activePanelIndex = Index

                    'Hide any visible panel that is not the currently selected panel
                    For Each Panel As ApplicationDesignerPanel In _designerPanels
                        If Panel IsNot NewCurrentPanel Then
                            Panel.ShowDesigner(False)
                        End If
                    Next

                    'Designer not yet created, do special handling for property pages
                    If NewCurrentPanel.DocData Is Nothing Then
                        Common.Switches.TracePDFocus(TraceLevel.Info, "  ... Designer not yet created")
                        With NewCurrentPanel
                            If .IsPropertyPage Then
                                'This is a property page.  Need to do some special handling
                                Common.Switches.TracePDFocus(TraceLevel.Info, "  ... Special property page handling")

                                If .PropertyPageInfo.LoadException IsNot Nothing Then
                                    Common.Switches.TracePDFocus(TraceLevel.Error, "  ... LoadException: " & .PropertyPageInfo.LoadException.Message)
                                    ErrorMessage = My.Resources.Designer.APPDES_ErrorLoadingPropPage & vbCrLf & .PropertyPageInfo.LoadException.Message
                                ElseIf .PropertyPageInfo.ComPropPageInstance Is Nothing OrElse .PropertyPageInfo.Site Is Nothing Then
                                    Common.Switches.TracePDFocus(TraceLevel.Info, "  ... ComPropPageInstance or the site is Nothing")
                                    ErrorMessage = My.Resources.Designer.APPDES_ErrorLoadingPropPage & vbCrLf & .PropertyPageInfo.Guid.ToString()
                                Else
                                    Common.Switches.TracePDFocus(TraceLevel.Info, "  ... Calling CreateDesigner")
                                    HostingPanel.SuspendLayout()
                                    Try
                                        .CreateDesigner()
                                    Finally
                                        HostingPanel.ResumeLayout(True)
                                    End Try

                                    'PERF: No need to call ShowDesigner 'cause the window frame's already been shown through the creation
                                    DesignerAlreadyShownOnCreation = True

                                    'ActivatePage is required because a IPropertyPage.Show will
                                    'fail if IPropertyPage.Activate has not been done first
                                    Dim PropPageView As PropPageDesigner.PropPageDesignerView
                                    PropPageView = TryCast(.DocView, PropPageDesigner.PropPageDesignerView)
                                    If PropPageView IsNot Nothing Then
                                        PropPageView.Init(DTEProject, .PropertyPageInfo.ComPropPageInstance, .PropertyPageInfo.Site, _projectHierarchy, .PropertyPageInfo.IsConfigPage)
                                    Else
                                        'Must have had error loading
                                    End If

                                End If

                                'Because we may have previously retrieved the tab title from a cache, it 
                                '  is possible (though it shouldn't generally happen) that the title
                                '  is different from the cache.  Now that the property page may have been
                                '  loaded, set the text again to its official non-cached value.
                                .TabTitle = .PropertyPageInfo.Title
                                GetTabButton(Index).Text = .PropertyPageInfo.Title
                            Else
                                'No special handling for non-property pages
                            End If

                        End With
                    ElseIf ForceActivate Then
                        ' We need to reactivate the page to ensure that focus returns to the first control, as the user
                        ' may have been using keyboard navigation to access the page and would otherwise be on the final
                        ' control in the form again (putting them right back in the DesignerTabControl on next Tab).
                        Dim PropPageView As PropPageDesigner.PropPageDesignerView
                        PropPageView = TryCast(NewCurrentPanel.DocView, PropPageDesigner.PropPageDesignerView)
                        If PropPageView IsNot Nothing Then
                            'We are looping in the same page, do not set the undo status to clean
                            PropPageView.SetControls(True)
                        Else
                            'Must have had error loading
                        End If
                    End If
                Catch ex As Exception When Common.ReportWithoutCrash(ex, NameOf(ShowTab), NameOf(ApplicationDesignerView))
                    ErrorMessage = Common.DebugMessageFromException(ex)
                End Try

                'Now make the selected design panel visible
                Try
                    If TypeOf NewCurrentPanel.CustomViewProvider Is ErrorControlCustomViewProvider Then
                        'The page is showing an error control.  Let's try again to load the real docview into it.
                        NewCurrentPanel.CustomViewProvider.CloseView()
                        NewCurrentPanel.CustomViewProvider = Nothing
                    End If
                    If Not DesignerAlreadyShownOnCreation Then
                        NewCurrentPanel.ShowDesigner(True)
                    End If

#If DEBUG Then
                    If NewCurrentPanel.CustomViewProvider IsNot Nothing Then
                        'New panel has a custom view provider, so IVsWindowFrame.Show won�t have been called.
                    Else
                        If NewCurrentPanel.PropertyPageInfo IsNot Nothing AndAlso NewCurrentPanel.PropertyPageInfo.LoadException IsNot Nothing Then
                            'There was an error loading the page, so IVsWindowFrame.Show() would not have been called
                        Else
                            'IVsWindowFrame.Show() should have been called
                            Debug.Assert(NewCurrentPanel.m_Debug_cWindowFrameShow > 0, "New page panel didn't get activated?")
                        End If
                    End If

                    Debug.Assert(NewCurrentPanel.m_Debug_cWindowFrameShow <= 1, "PERFORMANCE/FLICKER WARNING: More than one IVsWindowFrame.Activate() occurred")
                    Debug.Assert(NewCurrentPanel.m_Debug_cWindowFrameBoundsUpdated <= 1, "PERFORMANCE/FLICKER WARNING: Window frame bounds were updated more than once")
#End If

                Catch ex As Exception When Common.ReportWithoutCrash(ex, NameOf(ShowTab), NameOf(ApplicationDesignerView))
                    If ErrorMessage = "" Then
                        ErrorMessage = My.Resources.Designer.APPDES_ErrorLoadingPropPage & vbCrLf & Common.DebugMessageFromException(ex)
                    End If
                End Try

                SelectedIndex = Index

                If ErrorMessage <> "" Then
                    Try
                        'Display the error control if there was a problem
                        NewCurrentPanel.CloseFrame()
                        NewCurrentPanel.CustomViewProvider = New ErrorControlCustomViewProvider(ErrorMessage)
                        NewCurrentPanel.ShowDesigner()
                    Catch ex As Exception When Common.ReportWithoutCrash(ex, NameOf(ShowTab), NameOf(ApplicationDesignerView))
                        'If there's an error showing the error control, it's time to give up
                    End Try
                End If

                'We may have opened a new page, need to verify all dirty states
                DelayRefreshDirtyIndicators()

                CodeMarkers.Instance.CodeMarker(RoslynCodeMarkerEvent.PerfMSVSEditorsShowTabEnd)
                Common.Switches.TracePDFocus(TraceLevel.Error, "CodeMarker: perfMSVSEditorsShowTabEnd")
                Common.Switches.TracePDPerf("CodeMarker: perfMSVSEditorsShowTabEnd")
                Common.Switches.TracePDPerfEnd("ApplicationDesignerView.ShowTab")
            Finally
                _inShowTab = False
            End Try
        End Sub

        'Standard title for messageboxes, etc.
        Private ReadOnly _messageBoxCaption As String = My.Resources.Designer.APPDES_Title

        ''' <summary>
        ''' Displays a message box using the Visual Studio-approved manner.
        ''' </summary>
        ''' <param name="Message">The message text.</param>
        ''' <param name="Buttons">Which buttons to show</param>
        ''' <param name="Icon">the icon to show</param>
        ''' <param name="DefaultButton">Which button should be default?</param>
        ''' <param name="HelpLink">The help link</param>
        ''' <returns>One of the DialogResult values</returns>
        Public Function DsMsgBox(Message As String,
                Buttons As MessageBoxButtons,
                Icon As MessageBoxIcon,
                Optional DefaultButton As MessageBoxDefaultButton = MessageBoxDefaultButton.Button1,
                Optional HelpLink As String = Nothing) As DialogResult

            Debug.Assert(_serviceProvider IsNot Nothing)
            Return AppDesDesignerFramework.DesignerMessageBox.Show(_serviceProvider, Message, _messageBoxCaption,
                Buttons, Icon, DefaultButton, HelpLink)
        End Function

        ''' <summary>
        ''' Displays a message box using the Visual Studio-approved manner.
        ''' </summary>
        ''' <param name="ex">The exception whose text should be displayed.</param>
        ''' <param name="HelpLink">The help link</param>
        Public Sub DsMsgBox(ex As Exception,
                Optional HelpLink As String = Nothing) Implements IPropertyPageSiteOwner.DsMsgBox

            Debug.Assert(_serviceProvider IsNot Nothing)
            AppDesDesignerFramework.DesignerMessageBox.Show(_serviceProvider, ex, _messageBoxCaption, HelpLink:=HelpLink)
        End Sub

        ''' <summary>
        ''' Moves to the next or previous tab in the project designer
        ''' </summary>
        ''' <param name="forward">If true, moves forward a tab.  If false, moves back a tab.</param>
        Public Sub SwitchTab(forward As Boolean)
            Dim Index As Integer = _activePanelIndex
            If forward Then
                Index += 1
            Else
                Index -= 1
            End If
            If Index < 0 Then
                Index = _designerPanels.Length - 1
            ElseIf Index >= _designerPanels.Length Then
                Index = 0
            End If
            ShowTab(Index)
        End Sub

        ''' <summary>
        ''' Occurs when the user clicks on one of the tab buttons.  Switch to that tab.
        ''' </summary>
        ''' <param name="item"></param>
        Public Overrides Sub OnItemClick(item As ProjectDesignerTabButton)
            OnItemClick(item, reactivatePage:=False)
        End Sub

        Public Overrides Sub OnItemClick(item As ProjectDesignerTabButton, reactivatePage As Boolean)
            Common.Switches.TracePDFocus(TraceLevel.Warning, "ApplicationDesignerView.OnItemClick")
            MyBase.OnItemClick(item, reactivatePage)
            ShowTab(SelectedIndex, ForceShow:=True)

            ' we need set back the tab, if we failed to switch...
            If SelectedIndex <> _activePanelIndex Then
                SelectedIndex = _activePanelIndex
            End If
        End Sub

        Friend Overrides Sub SetControl(firstControl As Boolean)
            Dim NewCurrentPanel As ApplicationDesignerPanel = _designerPanels(SelectedIndex)
            Dim PropPageView As PropPageDesigner.PropPageDesignerView
            PropPageView = TryCast(NewCurrentPanel.DocView, PropPageDesigner.PropPageDesignerView)
            If PropPageView IsNot Nothing Then
                'We are looping in the same page, do not set the undo status to clean
                PropPageView.SetControls(firstControl)
            End If
        End Sub

        Protected Overrides Sub OnThemeChanged()
            Dim VsUIShell5 = VsUIShell5Service
            BackColor = Common.ShellUtil.GetProjectDesignerThemeColor(VsUIShell5, "Background", __THEMEDCOLORTYPE.TCT_Background, Drawing.SystemColors.Window)

            MyBase.OnThemeChanged()
        End Sub

        ''' <summary>
        ''' WndProc for the project designer.
        ''' </summary>
        ''' <param name="m"></param>
        Protected Overrides Sub WndProc(ByRef m As Message)
            If m.Msg = Win32Constant.WM_SETFOCUS AndAlso Not _isInPanelWindowFrameShow Then 'in MDI mode this can get hit recursively
                'We need to intercept WM_SETFOCUS on the project designer to keep WinForms from setting focus to the
                '  current control (one of the tab buttons).  Instead, we want to keep the tab buttons from getting
                '  focus (unless they're clicked on directly), and instead activate the current page directly.
                '  This circumvents lots of back-and-forth swapping between the project designer and the current page
                '  as the active designer, and helps us handle the on-click case correctly.
                'Note: Handling OnGotFocus would not be good enough - we need to keep WinForms from doing their default
                '  processing on WM_SETFOCUS, and we can't do that by handling OnGotFocus.
                Common.Switches.TracePDFocus(TraceLevel.Warning, "Preprocess: Stealing ApplicationDesignerView.WM_SETFOCUS handling")
                Common.Switches.TracePDFocus(TraceLevel.Verbose, New StackTrace().ToString)

                If Not _inShowTab AndAlso _okayToActivatePanelsOnFocus Then
                    If _activePanelIndex >= 0 AndAlso _activePanelIndex < _designerPanels.Length Then
                        Dim Panel As ApplicationDesignerPanel = _designerPanels(_activePanelIndex)
                        If Panel IsNot Nothing Then
                            If Panel.VsWindowFrame IsNot Nothing Then
                                'Activate the currently-active panel's window frame, give it focus, and ensure that 
                                '  the active document is updated.
                                Common.Switches.TracePDFocus(TraceLevel.Warning, "... VsWindowFrame.Show()")
                                Try
                                    _isInPanelWindowFrameShow = True
                                    Panel.VsWindowFrame.Show()
                                Finally
                                    _isInPanelWindowFrameShow = False
                                End Try
                            ElseIf Panel.CustomViewProvider IsNot Nothing Then
                                MyBase.WndProc(m)
                            End If
                        End If
                    End If
                Else
                    Common.Switches.TracePDFocus(TraceLevel.Warning, "  ... Ignoring")
                End If

                'Return without calling in to base functionality.  This keeps the application designer's WinForms code
                '  from automatically setting focus to the currently-active tab.
                Return
            End If

            MyBase.WndProc(m)
        End Sub

        ''' <summary>
        ''' Calls when the application designer window pane has completely initialized the application designer view (the
        '''   ApplicationDesignerWindowPane controls initialization and population of the view).
        ''' </summary>
        Public Sub OnInitializationComplete()
            Common.Switches.TracePDFocus(TraceLevel.Warning, "OnInitializationComplete")
            _initializationComplete = True

            'UI initialization is complete.  Now we need to now show the first page.
            ShowTab(LastShownTab, True)

            'Queue a request to update the dirty indicators
            DelayRefreshDirtyIndicators()

            '... and start listening to when the dirty state might change
            AdviseRunningDocTableEvents()

            _okayToActivatePanelsOnFocus = True
        End Sub

        ''' <summary>
        ''' Returns true if initialization is complete for the project designer.  This is used
        '''   by ApplicationDesignerPanel to delay any window frame activations until after
        '''   initialization.
        ''' </summary>
        Public ReadOnly Property InitializationComplete As Boolean
            Get
                Return _initializationComplete
            End Get
        End Property

#Region "Dirty indicators"

        ''' <summary>
        ''' Queues up a request (via PostMessage) to refresh all of our dirty indicators.
        ''' </summary>
        Public Sub DelayRefreshDirtyIndicators() Implements IPropertyPageSiteOwner.DelayRefreshDirtyIndicators
            If Not _initializationComplete Then
                Exit Sub
            End If

            If Not _refreshDirtyIndicatorsQueued AndAlso IsHandleCreated Then
                BeginInvoke(New MethodInvoker(AddressOf RefreshDirtyIndicatorsHelper))
                _refreshDirtyIndicatorsQueued = True
            End If
        End Sub

        ''' <summary>
        ''' Used by DelayRefreshDirtyIndicators, do not call directly.  Updates the dirty
        '''   indicators for the project designer and all tabs.
        ''' </summary>
        Private Sub RefreshDirtyIndicatorsHelper()
            Try
                'First, update all tab dirty indicators
                If _designerPanels IsNot Nothing Then
                    For i As Integer = 0 To _designerPanels.Length - 1
                        GetTabButton(i).DirtyIndicator = _designerPanels(i) IsNot Nothing AndAlso _designerPanels(i).IsDirty()
                    Next
                End If

                'Should the project designer as a whole look dirty or not?
                'We show the dirty state for the project designer if:
                '  a) the project file is dirty
                '    or
                '  b) any of the tabs is dirty
                Dim ProjectDesignerIsDirty As Boolean = False

                Dim AnyTabIsDirty As Boolean = False
                For i As Integer = 0 To _designerPanels.Length - 1
                    AnyTabIsDirty = AnyTabIsDirty OrElse GetTabButton(i).DirtyIndicator
                Next
                ProjectDesignerIsDirty = AnyTabIsDirty OrElse IsProjectFileDirty(DTEProject)

                'Update the project designer's dirty status
                SetFrameDirtyIndicator(ProjectDesignerIsDirty)

            Catch ex As Exception When Common.ReportWithoutCrash(ex, NameOf(RefreshDirtyIndicatorsHelper), NameOf(ApplicationDesignerView))
                ' VsVhidbey 446720 - if we have messed up the UNDO stack, the m_designerPanels.IsDirty call may 
                ' throw an exception (when trying to enumerate the UNDO units)
            Finally
                'Allow us to queue refresh requests again
                _refreshDirtyIndicatorsQueued = False
            End Try
        End Sub

        ''' <summary>
        ''' Returns true if the project file is dirty
        ''' </summary>
        Private Function IsProjectFileDirty(Project As Project) As Boolean
            Debug.Assert(Project IsNot Nothing)

            If Project IsNot Nothing Then
                Dim ProjectFullName As String = Project.FullName
                Dim rdt As IVsRunningDocumentTable = TryCast(GetService(GetType(IVsRunningDocumentTable)), IVsRunningDocumentTable)
                If rdt IsNot Nothing Then
                    Dim punkDocData As New IntPtr(0)
                    Try
                        Dim Hierarchy As IVsHierarchy = Nothing
                        Dim ItemId As UInteger
                        Dim dwCookie As UInteger
                        Dim hr As Integer = rdt.FindAndLockDocument(CUInt(_VSRDTFLAGS.RDT_NoLock), ProjectFullName, Hierarchy, ItemId, punkDocData, dwCookie)

                        If VSErrorHandler.Succeeded(hr) Then
                            Return IsDocDataDirty(dwCookie, Hierarchy, ItemId)
                        End If
                    Finally
                        If punkDocData <> IntPtr.Zero Then
                            Marshal.Release(punkDocData)
                        End If
                    End Try
                End If
            End If

            Return False
        End Function

        ''' <summary>
        ''' Gets the cookie for the project file
        ''' </summary>
        Private Function GetProjectFileCookie(Project As Project) As UInteger
            Debug.Assert(Project IsNot Nothing)

            If Project IsNot Nothing Then
                Dim ProjectFullName As String = Project.FullName
                Dim rdt As IVsRunningDocumentTable = TryCast(GetService(GetType(IVsRunningDocumentTable)), IVsRunningDocumentTable)
                If rdt IsNot Nothing Then
                    Dim punkDocData As New IntPtr(0)
                    Try
                        Dim Hierarchy As IVsHierarchy = Nothing
                        Dim ItemId As UInteger
                        Dim dwCookie As UInteger
                        Dim hr As Integer = rdt.FindAndLockDocument(CUInt(_VSRDTFLAGS.RDT_NoLock), ProjectFullName, Hierarchy, ItemId, punkDocData, dwCookie)

                        If VSErrorHandler.Succeeded(hr) Then
                            Return dwCookie
                        End If
                    Finally
                        If punkDocData <> IntPtr.Zero Then
                            Marshal.Release(punkDocData)
                        End If
                    End Try
                End If
            End If

            Return 0
        End Function

        ''' <summary>
        ''' Sets the frame's dirty indicator.  This causes an asterisk to appear or disappear
        '''   from the project designer's MDI tab title (i.e., represents the project designer's dirty 
        '''   state as a whole).
        ''' </summary>
        ''' <param name="Dirty">If true, the asterisk is added, if false, it is removed.</param>
        Private Sub SetFrameDirtyIndicator(Dirty As Boolean)
            If Not _projectDesignerDirtyStateInitialized OrElse _lastProjectDesignerDirtyState <> Dirty Then
                Dim Frame As IVsWindowFrame = WindowFrame
                If Frame IsNot Nothing Then
                    'VSFPROPID_OverrideDirtyState - this is a tri-state property.  If Empty, we get default behavior.  True/False
                    '  overrides the state.
                    Dim newState As Object = Dirty
                    Frame.SetProperty(__VSFPROPID2.VSFPROPID_OverrideDirtyState, newState)
                    _lastProjectDesignerDirtyState = Dirty
                    _projectDesignerDirtyStateInitialized = True
                End If
            End If
        End Sub

#End Region

#Region "IVsSelectionEvents"

        ''' <summary>
        '''     Called by the shell when the UI context changes.  We don't care about this.
        '''
        ''' </summary>
        ''' <param name='dwCmdUICookie'>
        '''     A cookie representing the area of UI that has changed.
        ''' </param>
        ''' <param name='fActive'>
        '''     Nonzero if the context is now active.
        '''
        ''' </param>
        ''' <seealso cref='IVsSelectionEvents'/>
        Public Function OnCmdUIContextChanged(dwCmdUICookie As UInteger, fActive As Integer) As Integer Implements IVsSelectionEvents.OnCmdUIContextChanged
            Return NativeMethods.S_OK
        End Function

        ''' <summary>
        '''     Called by the shell when the the document or other part of the active UI changes.
        '''
        ''' </summary>
        ''' <param name='elementid'>
        '''     A tag indicating the type of element that has changed.
        ''' </param>
        ''' <param name='varValueOld'>
        '''     The old value of the element.
        ''' </param>
        ''' <param name='varValueNew'>
        '''     The new value of the element.
        '''
        ''' </param>
        ''' <seealso cref='IVsSelectionEvents'/>
        Public Function OnElementValueChanged(elementid As UInteger, varValueOld As Object, varValueNew As Object) As Integer Implements IVsSelectionEvents.OnElementValueChanged
            If elementid = 1 AndAlso _designerPanels IsNot Nothing AndAlso varValueOld IsNot varValueNew Then ' WindowFrame changed
                For Each panel As ApplicationDesignerPanel In _designerPanels
                    If panel.VsWindowFrame Is varValueOld Then
                        panel.OnWindowActivated(False)
                        Exit For
                    End If
                Next
                For Each panel As ApplicationDesignerPanel In _designerPanels
                    If panel.VsWindowFrame Is varValueNew Then
                        panel.OnWindowActivated(True)
                        Exit For
                    End If
                Next
            End If
            Return NativeMethods.S_OK
        End Function

        ''' <summary>
        '''     Called by the shell when a new selection container is available.  We broadcast this to
        '''     anyone listening.
        '''
        ''' </summary>
        ''' <param name='pHierOld'>
        '''     The previous IVsHierarchy.  We ignore this.
        ''' </param>
        ''' <param name='itemidOld'>
        '''     The previous hierarchies ITEMID.  We ignore this.
        ''' </param>
        ''' <param name='pMISOld'>
        '''     A MultiItemSelection pointer, which we ignore.
        ''' </param>
        ''' <param name='pSCOld'>
        '''     The old selection container.
        ''' </param>
        ''' <param name='pHierNew'>
        '''     The new IVsHierarchy. We ignore this.
        ''' </param>
        ''' <param name='itemidNew'>
        '''     The new hierarchies ITEMID.  We ignore this.
        ''' </param>
        ''' <param name='pMISNew'>
        '''     The new MultiItemSelection pointer, which we ignore.
        ''' </param>
        ''' <param name='pSCNew'>
        '''     The new selection container.  We do use this.
        '''
        ''' </param>
        ''' <seealso cref='IVsSelectionEvents'/>
        Public Function OnSelectionChanged(pHierOld As IVsHierarchy, itemidOld As UInteger, pMISOld As IVsMultiItemSelect, pSCOld As ISelectionContainer, pHierNew As IVsHierarchy, itemidNew As UInteger, pMISNew As IVsMultiItemSelect, pSCNew As ISelectionContainer) As Integer Implements IVsSelectionEvents.OnSelectionChanged
            Return NativeMethods.S_OK
        End Function

#End Region

#Region "IVsRunningDocTableEvents"

        'We sync these events to be notified of when our DocData might have been dirtied/undirtied

        ''' <summary>
        ''' Start listening to IVsRunningDocTableEvents events
        ''' </summary>
        Private Sub AdviseRunningDocTableEvents()
            Dim rdt As IVsRunningDocumentTable = TryCast(GetService(GetType(IVsRunningDocumentTable)), IVsRunningDocumentTable)
            Debug.Assert(rdt IsNot Nothing, "Couldn't get running document table")
            If rdt IsNot Nothing Then
                VSErrorHandler.ThrowOnFailure(rdt.AdviseRunningDocTableEvents(Me, _rdtEventsCookie))
            End If
        End Sub

        ''' <summary>
        ''' Stop listening to IVsRunningDocTableEvents events
        ''' </summary>
        Private Sub UnadviseRunningDocTableEvents()
            If _rdtEventsCookie <> 0 Then
                Dim rdt As IVsRunningDocumentTable = TryCast(GetService(GetType(IVsRunningDocumentTable)), IVsRunningDocumentTable)
                Debug.Assert(rdt IsNot Nothing, "Couldn't get running document table")
                If rdt IsNot Nothing Then
                    VSErrorHandler.ThrowOnFailure(rdt.UnadviseRunningDocTableEvents(_rdtEventsCookie))
                End If
                _rdtEventsCookie = 0
            End If
        End Sub

        ''' <summary>
        ''' Fires after an attribute of a document in the RDT is changed.
        ''' </summary>
        ''' <param name="docCookie"></param>
        ''' <param name="grfAttribs"></param>
        Public Function OnAfterAttributeChange(docCookie As UInteger, grfAttribs As UInteger) As Integer Implements IVsRunningDocTableEvents.OnAfterAttributeChange
            Const InterestingFlags As Long = __VSRDTATTRIB.RDTA_DocDataIsDirty Or __VSRDTATTRIB.RDTA_DocDataIsNotDirty Or __VSRDTATTRIB.RDTA_NOTIFYDOCCHANGEDMASK
            If (grfAttribs And InterestingFlags) <> 0 Then
                'CONSIDER: better would be to check it against all of our DocData's.  But we don't have a simple, static list
                '  lying around), we'll just queue up a request to refresh all of our states.  This shouldn't be a performance
                '  problem since we do this via PostMessage.
                DelayRefreshDirtyIndicators()
            End If

            Return NativeMethods.S_OK
        End Function

        ''' <summary>
        ''' Fires after a document window is placed in the Hide state.
        ''' </summary>
        ''' <param name="docCookie"></param>
        ''' <param name="pFrame"></param>
        Public Function OnAfterDocumentWindowHide(docCookie As UInteger, pFrame As IVsWindowFrame) As Integer Implements IVsRunningDocTableEvents.OnAfterDocumentWindowHide
            Return NativeMethods.S_OK
        End Function

        ''' <summary>
        ''' Fires after the first document in the RDT is locked.
        ''' </summary>
        ''' <param name="docCookie"></param>
        ''' <param name="dwRDTLockType"></param>
        ''' <param name="dwReadLocksRemaining"></param>
        ''' <param name="dwEditLocksRemaining"></param>
        Public Function OnAfterFirstDocumentLock(docCookie As UInteger, dwRDTLockType As UInteger, dwReadLocksRemaining As UInteger, dwEditLocksRemaining As UInteger) As Integer Implements IVsRunningDocTableEvents.OnAfterFirstDocumentLock
            Return NativeMethods.S_OK
        End Function

        ''' <summary>
        ''' Fires after a document in the RDT is saved.
        ''' </summary>
        ''' <param name="docCookie"></param>
        Public Function OnAfterSave(docCookie As UInteger) As Integer Implements IVsRunningDocTableEvents.OnAfterSave
            Debug.Assert(_designerPanels IsNot Nothing, "m_DesignerPanels should not be Nothing")
            If _designerPanels IsNot Nothing Then
                'Was the project file saved?
                If docCookie = GetProjectFileCookie(DTEProject) Then
                    'Yes.  Need to reset the undo/redo clean state of all property pages
                    SetUndoRedoCleanStateOnAllPropertyPages()
                End If
                DelayRefreshDirtyIndicators()
            End If

            Return NativeMethods.S_OK
        End Function

        ''' <summary>
        ''' Fires before a document window is placed in the Show state.
        ''' </summary>
        ''' <param name="docCookie"></param>
        ''' <param name="fFirstShow"></param>
        ''' <param name="pFrame"></param>
        Public Function OnBeforeDocumentWindowShow(docCookie As UInteger, fFirstShow As Integer, pFrame As IVsWindowFrame) As Integer Implements IVsRunningDocTableEvents.OnBeforeDocumentWindowShow
            Debug.Assert(_designerPanels IsNot Nothing, "m_DesignerPanels should not be Nothing")
            If _designerPanels IsNot Nothing Then
                If Not _inShowTab Then
                    ' If the window frame passed to us belongs to any of our panels,
                    ' we better set that as the active tab...
                    For Index As Integer = 0 To _designerPanels.Length - 1
                        Dim panel As ApplicationDesignerPanel
                        panel = _designerPanels(Index)
                        Debug.Assert(panel IsNot Nothing, "m_DesignerPanels(Index) should not be Nothing")
                        If ReferenceEquals(panel.VsWindowFrame, pFrame) Then
                            ShowTab(Index)
                            Exit For
                        End If
                    Next
                End If
            End If

            Return NativeMethods.S_OK
        End Function

        ''' <summary>
        ''' Fires before the last document in the RDT is unlocked
        ''' </summary>
        ''' <param name="docCookie"></param>
        ''' <param name="dwRDTLockType"></param>
        ''' <param name="dwReadLocksRemaining"></param>
        ''' <param name="dwEditLocksRemaining"></param>
        Public Function OnBeforeLastDocumentUnlock(docCookie As UInteger, dwRDTLockType As UInteger, dwReadLocksRemaining As UInteger, dwEditLocksRemaining As UInteger) As Integer Implements IVsRunningDocTableEvents.OnBeforeLastDocumentUnlock
            Return NativeMethods.S_OK
        End Function
        ''' <summary>
        ''' Fires after a document is added to the running document table but before it is locked for the first time.
        ''' </summary>
        ''' <param name="Hierarchy"></param>
        ''' <param name="ItemId"></param>
        ''' <param name="MkDocument"></param>
        Public Function OnBeforeFirstDocumentLock(Hierarchy As IVsHierarchy, ItemId As UInteger, MkDocument As String) As Integer Implements IVsRunningDocTableEvents4.OnBeforeFirstDocumentLock
            Return NativeMethods.S_OK
        End Function
        ''' <summary>
        ''' Fires after all documents are saved (some of the documents saved may not be in the running document table).
        ''' </summary>
        Public Function OnAfterSaveAll() As Integer Implements IVsRunningDocTableEvents4.OnAfterSaveAll
            'A Save All operation just occurred.  Need to reset the undo/redo clean state of all property pages
            SetUndoRedoCleanStateOnAllPropertyPages()
        End Function

        ''' <summary>
        ''' Calls SetUndoRedoCleanState() on each property page
        ''' </summary>
        Public Sub SetUndoRedoCleanStateOnAllPropertyPages()
            For i As Integer = 0 To _designerPanels.Length - 1
                Debug.Assert(_designerPanels(i) IsNot Nothing, "m_DesignerPanels(Index) should not be Nothing")
                If _designerPanels(i) IsNot Nothing AndAlso _designerPanels(i).IsPropertyPage Then
                    Dim PropPageView As PropPageDesigner.PropPageDesignerView = TryCast(_designerPanels(i).DocView, PropPageDesigner.PropPageDesignerView)
                    If PropPageView IsNot Nothing Then
                        PropPageView.SetUndoRedoCleanState()
                    End If
                End If
            Next

            DelayRefreshDirtyIndicators()
        End Sub

        ''' <summary>
        ''' Fires after a document is unlocked and removed from the running document table.
        ''' </summary>
        ''' <param name="Hierarchy"></param>
        ''' <param name="ItemId"></param>
        ''' <param name="MkDocument"></param>
        ''' <param name="ClosedWithoutSaving"></param>
        Public Function OnAfterLastDocumentUnlock(Hierarchy As IVsHierarchy, ItemId As UInteger, MkDocument As String, ClosedWithoutSaving As Integer) As Integer Implements IVsRunningDocTableEvents4.OnAfterLastDocumentUnlock
            Return NativeMethods.S_OK
        End Function

#End Region

        ''' <summary>
        ''' Gets the locale ID from the shell
        ''' </summary>
        Private Function GetLocaleID() As UInteger Implements IPropertyPageSiteOwner.GetLocaleID
            Dim LocaleId As UInteger
            Dim UIHostLocale As IUIHostLocale = DirectCast(GetService(GetType(IUIHostLocale)), IUIHostLocale)
            If UIHostLocale IsNot Nothing Then
                UIHostLocale.GetUILocale(LocaleId)
                Return LocaleId
            End If

            'Fallback
            Return NativeMethods.GetUserDefaultLCID()
        End Function

#Region "Debug tracing for OnLayout/Size events..."

        Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
            Common.Switches.TracePDPerfBegin(levent, "ApplicationDesignerView.OnLayout()")
            MyBase.OnLayout(levent)
            Common.Switches.TracePDPerfEnd("ApplicationDesignerView.OnLayout()")
        End Sub

        Private Sub HostingPanel_Layout(sender As Object, levent As LayoutEventArgs)
            Common.Switches.TracePDPerf(levent, "ApplicationDesignerView.HostingPanel_Layout()")
        End Sub

        Private Sub HostingPanel_SizeChanged(sender As Object, e As EventArgs)
            Common.Switches.TracePDPerf("ApplicationDesignerView.HostingPanel_SizeChanged: " & HostingPanel.Size.ToString())
        End Sub

        Private Sub ApplicationDesignerView_SizeChanged(sender As Object, e As EventArgs) Handles Me.SizeChanged
            Common.Switches.TracePDPerf("ApplicationDesignerView.SizeChanged: " & Size.ToString())
        End Sub

#End Region

    End Class

End Namespace
