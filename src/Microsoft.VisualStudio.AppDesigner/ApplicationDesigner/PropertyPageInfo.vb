' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

Imports System.Runtime.InteropServices

Imports Microsoft.VisualStudio.Editors.AppDesInterop
Imports Microsoft.VisualStudio.Shell.Interop

Imports Common = Microsoft.VisualStudio.Editors.AppDesCommon
Imports OleInterop = Microsoft.VisualStudio.OLE.Interop

Namespace Microsoft.VisualStudio.Editors.ApplicationDesigner

    ''' <summary>
    ''' This page encapsulates all the data for a property page
    ''' </summary>
    Public Class PropertyPageInfo
        Implements IDisposable

        Private _guid As Guid 'The GUID for the property page
        Private ReadOnly _isConfigPage As Boolean 'True if the page's properties can have different values in different configurations

        Private _comPropPageInstance As OleInterop.IPropertyPage
        Private _info As OleInterop.PROPPAGEINFO
        Private _site As PropertyPageSite
        Private _loadException As Exception 'The exception that occurred while loading the page, if any
        Private ReadOnly _parentView As ApplicationDesignerView 'The owning application designer view
        Private _loadAlreadyAttempted As Boolean 'Whether or not we've attempted to load this property page

        Private Const REGKEY_CachedPageTitles As String = "\ProjectDesigner\CachedPageTitles"

        ''' <summary>
        ''' Constructor
        ''' </summary>
        ''' <param name="Guid">The GUID to create the property page</param>
        ''' <param name="IsConfigurationDependentPage">Whether or not the page has different values for each configuration (e.g. the Debug page)</param>
        Public Sub New(ParentView As ApplicationDesignerView, Guid As Guid, IsConfigurationDependentPage As Boolean)
            Debug.Assert(Not Guid.Equals(Guid.Empty), "Empty guid?")
            Debug.Assert(ParentView IsNot Nothing)
            _parentView = ParentView
            _guid = Guid
            _isConfigPage = IsConfigurationDependentPage
        End Sub


#Region "Dispose/IDisposable"

        ''' <summary>
        ''' Disposes of any the doc data
        ''' </summary>
        Public Overloads Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)
        End Sub

        'UserControl overrides dispose to clean up the component list.
        Protected Overloads Sub Dispose(disposing As Boolean)
            If disposing Then
                Try
                    If _comPropPageInstance IsNot Nothing Then
                        _comPropPageInstance.Deactivate()
                        _comPropPageInstance.SetPageSite(Nothing)
                        If Marshal.IsComObject(_comPropPageInstance) Then
                            Marshal.ReleaseComObject(_comPropPageInstance)
                        End If
                        _comPropPageInstance = Nothing
                    End If
                    If _site IsNot Nothing Then
                        _site.Dispose()
                        _site = Nothing
                    End If
                Catch ex As Exception When Common.ReportWithoutCrash(ex, NameOf(Dispose), NameOf(PropertyPageInfo))
                    'Ignore everything else
                End Try

            End If
        End Sub

#End Region


        ''' <summary>
        ''' The GUID for the property page
        ''' </summary>
        Public ReadOnly Property Guid As Guid
            Get
                Return _guid
            End Get
        End Property


        ''' <summary>
        ''' True if the page's properties can have different values in different configurations
        ''' </summary>
        Public ReadOnly Property IsConfigPage As Boolean
            Get
                Return _isConfigPage
            End Get
        End Property


        ''' <summary>
        ''' The exception that occurred while loading the page, if any
        ''' </summary>
        Public ReadOnly Property LoadException As Exception
            Get
                Return _loadException
            End Get
        End Property


        ''' <summary>
        ''' Returns the IPropertyPage for the property page
        ''' </summary>
        Public ReadOnly Property ComPropPageInstance As OleInterop.IPropertyPage
            Get
                TryLoadPropertyPage()
                Return _comPropPageInstance
            End Get
        End Property


        ''' <summary>
        ''' Returns the PropertyPageSite for the property page
        ''' </summary>
        Public ReadOnly Property Site As PropertyPageSite
            Get
                TryLoadPropertyPage()
                Return _site
            End Get
        End Property


        ''' <summary>
        ''' Attempts to load the COM object for the property page, if it has not already
        '''   been attempted.  Does not throw on failure, but rather sets the LoadException
        '''   field to the exception which resulted.
        ''' </summary>
        ''' <remarks>Overridable for unit testing.</remarks>
        Public Overridable Sub TryLoadPropertyPage()
            Debug.Assert(_parentView IsNot Nothing)
            If _loadAlreadyAttempted Then
                Return
            End If
            Debug.Assert(_loadException Is Nothing)
            _loadAlreadyAttempted = True

            Try
                Common.Switches.TracePDPerf("*** PERFORMANCE WARNING: Attempting to load property page " & _guid.ToString)
                Dim LocalRegistry As ILocalRegistry
                LocalRegistry = CType(_parentView.GetService(GetType(ILocalRegistry)), ILocalRegistry)

                If LocalRegistry Is Nothing Then
                    Debug.Fail("Unabled to obtain ILocalRegistry")
                    _loadException = New ArgumentNullException("ParentView")
                    Return
                End If

                'Have to use array of 1 because of interop
                Dim PageInfos As OleInterop.PROPPAGEINFO() = New OleInterop.PROPPAGEINFO(0 + 1) {}

                Dim PageObject As Object
                Dim ComPropertyPageInstance As OleInterop.IPropertyPage

                Dim ObjectPtr As IntPtr

                VSErrorHandler.ThrowOnFailure(LocalRegistry.CreateInstance(_guid, Nothing, NativeMethods.IID_IUnknown, Win32Constant.CLSCTX_INPROC_SERVER, ObjectPtr))
                Try
                    PageObject = Marshal.GetObjectForIUnknown(ObjectPtr)
                Finally
                    Marshal.Release(ObjectPtr)
                End Try

                ComPropertyPageInstance = CType(PageObject, OleInterop.IPropertyPage)

                'Save the IPropertyPage object
                _comPropPageInstance = ComPropertyPageInstance

                'Set the page site
                _site = New PropertyPageSite(_parentView, ComPropertyPageInstance)

                'Get the property page's PAGEINFO for later use
                ComPropertyPageInstance.GetPageInfo(PageInfos)
                _info = PageInfos(0)

                Common.Switches.TracePDPerf("  [Loaded property page '" & _info.pszTitle & "']")

#If DEBUG Then
                'Verify that loading the property page actually gave us the same title as the
                '  cached version.
                If _info.pszTitle IsNot Nothing AndAlso CachedTitle IsNot Nothing Then
                    Debug.Assert(_info.pszTitle.Equals(CachedTitle),
                        "The page title retrieved from cache ('" & CachedTitle & "') was not the same as that retrieved by " _
                        & "loading the page ('" & _info.pszTitle & "')")
                End If
#End If

                'Cache the title for future use
                CachedTitle = _info.pszTitle

            Catch Ex As Exception When Common.ReportWithoutCrash(Ex, NameOf(TryLoadPropertyPage), NameOf(PropertyPageInfo))
                If _comPropPageInstance IsNot Nothing Then
                    'IPropertyPage.GetPageInfo probably failed - if that didn't 
                    ' succeed, then nothing much else will likely work on the page either
                    If Marshal.IsComObject(_comPropPageInstance) Then
                        Marshal.ReleaseComObject(_comPropPageInstance)
                    End If
                    _comPropPageInstance = Nothing
                Else
                    'Page failed to load
                End If

                _loadException = Ex 'Save this to display later
            End Try
        End Sub


        ''' <summary>
        ''' Retrieves the title for the property page.  This title is cached on the
        '''   machine after the first time we've loaded the property page, so calling 
        '''   this property tries *not* load the property page if it's not
        '''   already loaded.
        ''' </summary>
        ''' <remarks>
        ''' PERF: This property used a cached version of the title to avoid having to
        '''   instantiate the COM object for the property page.
        ''' </remarks>
        Public ReadOnly Property Title As String
            Get
                If _loadAlreadyAttempted Then
                    If _loadException Is Nothing AndAlso _info.pszTitle <> "" Then
                        Debug.Assert(_loadAlreadyAttempted AndAlso _loadException Is Nothing)
                        Common.Switches.TracePDPerf("PropertyPageInfo.Title: Property page was already loaded, returning from m_Info: '" & _info.pszTitle & "'")
                        Return _info.pszTitle
                    Else
                        Common.Switches.TracePDPerf("PropertyPageInfo.Title: Previously attempted to load property page and failed, returning empty title")
                        Return String.Empty
                    End If
                Else
                    'Do we have a cached version?
                    Dim Cached As String = CachedTitle
                    If Cached <> "" Then
                        Common.Switches.TracePDPerf("PropertyPageInfo.Title: Retrieved page title from cache: " & Cached)
                        Return CachedTitle
                    End If

                    'No cache, we have no choice but to load the property page and ask it for the title
                    TryLoadPropertyPage() 'This will cache the newly-obtained title.
                    Return _info.pszTitle
                End If
            End Get
        End Property


        ''' <summary>
        ''' Gets the current locale ID that's being used by the project designer.
        ''' </summary>
        Private ReadOnly Property CurrentLocaleID As UInteger
            Get
                Return CType(_parentView, IPropertyPageSiteOwner).GetLocaleID()
            End Get
        End Property


        ''' <summary>
        ''' Retrieves the name of the registry value name to place into the
        '''   registry for this property page.
        ''' </summary>
        Private ReadOnly Property CachedTitleValueName As String
            Get
                'We must include both the property page GUID and the locale ID
                '  so that we react properly to user language changes.
                Return _guid.ToString() & "," & CurrentLocaleID.ToString()
            End Get
        End Property


        ''' <summary>
        ''' Attempts to retrieve or set the cached title of this page from the registry.
        ''' </summary>
        Private Property CachedTitle As String
            Get
                Dim KeyPath As String = _parentView.DTEProject.DTE.RegistryRoot & REGKEY_CachedPageTitles
                Dim Key As Win32.RegistryKey = Nothing
                Try
                    Key = Win32.Registry.CurrentUser.OpenSubKey(KeyPath)
                    If Key IsNot Nothing Then
                        Dim ValueObject As Object = Key.GetValue(CachedTitleValueName)
                        Dim ValueString = TryCast(ValueObject, String)
                        If ValueString IsNot Nothing Then
                            'Found a cached version
                            Return ValueString
                        End If
                    End If
                Catch ex As Exception When Common.ReportWithoutCrash(ex, NameOf(CachedTitle), NameOf(PropertyPageInfo))
                Finally
                    If Key IsNot Nothing Then
                        Key.Close()
                    End If
                End Try

                'No cached title yet.
                Return Nothing
            End Get
            Set
                If value Is Nothing Then
                    value = String.Empty
                End If
                Dim Key As Win32.RegistryKey = Nothing
                Try
                    Key = Win32.Registry.CurrentUser.CreateSubKey(_parentView.DTEProject.DTE.RegistryRoot & REGKEY_CachedPageTitles)
                    If Key IsNot Nothing Then
                        Key.SetValue(CachedTitleValueName, value, Win32.RegistryValueKind.String)
                    End If
                Catch ex As Exception When Common.ReportWithoutCrash(ex, NameOf(CachedTitle), NameOf(PropertyPageInfo))
                Finally
                    If Key IsNot Nothing Then
                        Key.Close()
                    End If
                End Try
            End Set
        End Property

        Friend ReadOnly Property DeferUntilIntellisenseIsReady As Boolean
            Get
                Dim KeyPath As String = _parentView.DTEProject.DTE.RegistryRoot & "\PropertyPages\" & _guid.ToString("B")
                Dim Key As Win32.RegistryKey = Nothing
                Try
                    Key = Win32.Registry.LocalMachine.OpenSubKey(KeyPath)
                    If Key IsNot Nothing Then
                        Dim ValueObject As Object = Key.GetValue(NameOf(DeferUntilIntellisenseIsReady))
                        Dim ValueBoolean = CBool(ValueObject)
                        Return ValueBoolean
                    End If
                Catch ex As Exception When Common.ReportWithoutCrash(ex, NameOf(DeferUntilIntellisenseIsReady), NameOf(PropertyPageInfo))
                Finally
                    If Key IsNot Nothing Then
                        Key.Close()
                    End If
                End Try

                Return False
            End Get
        End Property
    End Class

End Namespace
