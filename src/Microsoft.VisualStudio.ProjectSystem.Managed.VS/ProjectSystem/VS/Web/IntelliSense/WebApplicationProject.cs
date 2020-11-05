// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Web.Application;
using Microsoft.VisualStudio.Web.Interop;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web.IntelliSense
{
    /// <summary>
    ///     Wraps an underlying <see cref="IVsWebApplicationProject"/> instance.
    /// </summary>
    /// <remarks>
    ///     To follow COM rules, aggregated services must be based on fixed capabilities, 
    ///     however, our <see cref="IVsWebApplicationProject"/> implementation is only 
    ///     needed when the AspNet capability is present. To workaround this implementation
    ///     the outer provides an empty implementation that delegates onto the inner only
    ///     if it is applicable.
    /// </remarks>
    [ExportProjectNodeComService(typeof(IVsWebApplicationProject), typeof(IVsWebApplicationProject2), typeof(IVsWebProjectSupport), typeof(IVsWebProjectSupport2))]
    [AppliesTo(ProjectCapability.DotNet)]
    internal partial class WebApplicationProject : IVsWebApplicationProject, IVsWebApplicationProject2, IVsWebProjectSupport, IVsWebProjectSupport2, IDisposable
    {
        private UnconfiguredProject? _project;
        private Lazy<Instance, IAppliesToMetadataView>? _underlyingInstance;

        [ImportingConstructor]
        public WebApplicationProject(UnconfiguredProject project, Lazy<Instance, IAppliesToMetadataView> underlyingInstance)
        {
            _project = project;
            _underlyingInstance = underlyingInstance;
        }

        public int UpdateDesignerClass(string document, string codeBehind, string codeBehindFile, string[] publicFields, UDC_Flags flags)
        {
            return Invoke(instance => instance.UpdateDesignerClass(document, codeBehind, codeBehindFile, publicFields, flags));
        }

        public int GetDataEnvironment(out IntPtr ppvDataEnv)
        {
            IntPtr ppvDataEnvLocal = IntPtr.Zero;
            HResult result = Invoke(instance => instance.GetDataEnvironment(out ppvDataEnvLocal));

            ppvDataEnv = ppvDataEnvLocal;

            return result;
        }

        public int IsBlockingItemTypeResolver(out bool isBlockingItemTypeResolver)
        {
            bool isBlockingItemTypeResolverLocal = false;
            HResult result = Invoke(instance => instance.IsBlockingItemTypeResolver(out isBlockingItemTypeResolverLocal));

            isBlockingItemTypeResolver = isBlockingItemTypeResolverLocal!;

            return result;
        }

        public int BlockItemTypeResolver(bool blockItemTypeResolver)
        {
            return Invoke(instance => instance.BlockItemTypeResolver(blockItemTypeResolver));
        }

        public int GetIWebApplication(out IntPtr ppvDataEnv)
        {
            IntPtr ppvDataEnvLocal = IntPtr.Zero;
            HResult result = Invoke(instance => instance.GetIWebApplication(out ppvDataEnvLocal));

            ppvDataEnv = ppvDataEnvLocal;

            return result;
        }

        public int GetBrowseUrl(out string browseURL)
        {
            string? browseURLLocal = null;
            HResult result = Invoke(instance => instance.GetBrowseUrl(out browseURLLocal));

            browseURL = browseURLLocal!;

            return result;
        }

        public int GetWebUrl(out string ppWebUrl)
        {
            string? ppWebUrlLocal = null;
            HResult result = Invoke(instance => instance.GetWebUrl(out ppWebUrlLocal));

            ppWebUrl = ppWebUrlLocal!;

            return result;
        }

        public int GetWebPath(out string ppWebPath)
        {
            string? ppWebPathLocal = null;
            HResult result = Invoke(instance => instance.GetWebPath(out ppWebPathLocal));

            ppWebPath = ppWebPathLocal!;

            return result;
        }

        public int GetWebProjectUrl(out string ppWebProjectUrl)
        {
            string? ppWebProjectUrlLocal = null;
            HResult result = Invoke(instance => instance.GetWebProjectUrl(out ppWebProjectUrlLocal));

            ppWebProjectUrl = ppWebProjectUrlLocal!;

            return result;
        }

        public int GetWebProjectPath(out string ppWebProjectPath)
        {
            string? browseURLLocal = null;
            HResult result = Invoke(instance => instance.GetWebProjectPath(out browseURLLocal));

            ppWebProjectPath = browseURLLocal!;

            return result;
        }

        public int GetWebRemoteAuthoringUrl(out string? ppWebRemoteAuthoringUrl)
        {
            ppWebRemoteAuthoringUrl = null;
            return HResult.NotImplemented;
        }

        public int GetClientBuildManagerPath(out string? ppClientBuildManagerPath)
        {
            ppClientBuildManagerPath = null;
            return HResult.NotImplemented;
        }

        public int UsesIISWebServer(out bool usesIISWebServer)
        {
            bool usesIISWebServerLocal = false;
            HResult result = Invoke(instance => instance.UsesIISWebServer(out usesIISWebServerLocal));

            usesIISWebServer = usesIISWebServerLocal;

            return result;
        }

        public int OnReferenceAdded(string pszReferencePath)
        {
            return Invoke(instance => instance.OnReferenceAdded(pszReferencePath));
        }

        public int OnFileAdded(string pszFilePath, bool foldersMustBeInProject)
        {
            return Invoke(instance => instance.OnFileAdded(pszFilePath, foldersMustBeInProject));
        }

        public int GetIISMetabasePath(out string strIISMetabasePath)
        {
            string? strIISMetabasePathLocal = null;
            HResult result = Invoke(instance => instance.GetIISMetabasePath(out strIISMetabasePathLocal));

            strIISMetabasePath = strIISMetabasePathLocal!;

            return result;
        }

        public int CodeFolderAdded(string relativeFolderUrl)
        {
            return Invoke(instance => instance.CodeFolderAdded(relativeFolderUrl));
        }

        public int CodeFolderRemoved(string relativeFolderUrl)
        {
            return Invoke(instance => instance.CodeFolderRemoved(relativeFolderUrl));
        }

        public void Dispose()
        {
            // Important for ProjectNodeComServices to null out fields to reduce the amount 
            // of data we leak when extensions incorrectly holds onto the IVsHierarchy.
            _project = null;
            _underlyingInstance = null;
        }

        private HResult Invoke(Func<Instance, HResult> action)
        {
            // We need to handle:
            // 
            // 1) We already disposed, or disposed while this method is in progress
            // 2) The underlying instance isn't applicable to this project

            IProjectCapabilitiesScope? capabilities = _project?.Capabilities;
            Lazy<Instance, IAppliesToMetadataView>? instance = _underlyingInstance;
            if (capabilities == null || instance == null)
                return HResult.Unexpected;      // Disposed

            if (!instance.AppliesTo(capabilities))
                return HResult.NotImplemented;  // Not applicable

            return action(instance.Value);
        }

        // Unused members

        public int GetCodeBehindEventBinding(out IVsCodeBehindEventBinding? codeBehindEventBinding)
        {
            Assumes.Fail("Deprecated. Binders should be pulled from registry.");
            codeBehindEventBinding = null;
            return HResult.NotImplemented;
        }

        public int GetOpenedUrl(out string? openedURL)
        {
            Assumes.Fail("Deprecated. Use URL service instead.");
            openedURL = null;
            return HResult.NotImplemented;
        }

        public int GetUrlForItem(uint itemid, out string? itemURL)
        {
            Assumes.Fail("Deprecated. Use URL service instead.");

            itemURL = null;
            return HResult.NotImplemented;
        }

        public int StartWebAdminTool()
        {
            return HResult.NotImplemented;
        }

        public int GetWebAssemblyResolveService(out IVsWebAssemblyResolveService? assemblyResolveService)
        {
            assemblyResolveService = null;
            return HResult.NotImplemented;
        }

        public int GetWebDynamicMasterPageService(out IVsWebDynamicMasterPageService? dynamicMasterPageService)
        {
            dynamicMasterPageService = null;
            return HResult.NotImplemented;
        }

        public int GetWebUrlService(IVsWebUrlService pDefaultWebUrlService, out IVsWebUrlService? ppWebUrlService)
        {
            // Use default
            ppWebUrlService = null;
            return HResult.OK;
        }

        public int GetDefaultLanguage(out string? pbstrLanguage)
        {
            // Use default (WAPS don't override this)
            pbstrLanguage = null;
            return HResult.NotImplemented;
        }

        public int GetNewStyleSheetFolder(out string? styleSheetFolder)
        {
            // Use default behavior (WAPS don't override this)
            styleSheetFolder = null;
            return HResult.NotImplemented;
        }

        public int IsDesignViewDisabled(string filePath, out bool isDesignViewDisabled)
        {
            // Use default behavior (WAPS don't override this)
            isDesignViewDisabled = false;
            return HResult.OK;
        }

        public int GetUrlPicker(out object? urlPicker)
        {
            // Use default behavior (WAPS don't override this)
            urlPicker = null;
            return HResult.NotImplemented;
        }
    }
}
