// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Web.UI.Design;
using Microsoft.VisualStudio.Web.Application;
using Microsoft.VisualStudio.Web.Interop;
using Microsoft.VisualStudio.ProjectSystem.VS.Web.Tree;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.IO;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web.IntelliSense
{
    internal partial class WebApplicationProject
    {
        /// <summary>
        ///     Provides an implementation of <see cref="IVsWebApplicationProject"/>, 
        ///     <see cref="IVsWebApplicationProject2"/>, <see cref="IVsWebProjectSupport"/> 
        ///     and <see cref="IVsWebProjectSupport2"/>.
        /// </summary>
        /// <remarks>
        ///     To avoid leaking ourselves outside of this project, this implementation deliberately 
        ///     does not implement the interfaces directly.
        /// </remarks>
        [Export]
        [AppliesTo("AspNet")] // TODO:
        internal class Instance
        {
            private readonly UnconfiguredProject _project;
            private readonly IWebProject _webProject;
            private readonly SpecialCodeFolderDataSource _specialWebFolderDataSource;

            private int _blockingItemTypeResolution;

            [ImportingConstructor]
            internal Instance(UnconfiguredProject project, IWebProject webProject, SpecialCodeFolderDataSource specialWebFolderDataSource)
            {
                _project = project;
                _webProject = webProject;
                _specialWebFolderDataSource = specialWebFolderDataSource;
            }

            // IVsWebApplicationProject/IVsWebApplicationProject2

            public int UpdateDesignerClass(string document, string codeBehind, string codeBehindFile, string[] publicFields, UDC_Flags flags)
            {
                throw new NotImplementedException();
            }

            public int GetDataEnvironment(out IntPtr ppvDataEnv)
            {
                IVsDataEnvironment? dataEnvironment = _webProject.Services?.GetContextService<IVsDataEnvironment, IVsDataEnvironment>();

                if (dataEnvironment == null)
                {
                    ppvDataEnv = IntPtr.Zero;
                    return HResult.NoInterface;
                }

                ppvDataEnv = Marshal.GetIUnknownForObject(dataEnvironment);
                return HResult.OK;
            }

            public int IsBlockingItemTypeResolver(out bool isBlockingItemTypeResolver)
            {
                isBlockingItemTypeResolver = (_blockingItemTypeResolution > 0);
                return HResult.OK;
            }

            public int BlockItemTypeResolver(bool blockItemTypeResolver)
            {
                if (blockItemTypeResolver)
                {
                    _blockingItemTypeResolution++;
                }
                else
                {
                    if (_blockingItemTypeResolution > 0)
                    {
                        _blockingItemTypeResolution--;
                    }
                    else
                    {
                        System.Diagnostics.Debug.Fail("unbalanced _blockingItemTypeResolution");
                    }
                }

                return HResult.OK;
            }

            public int GetIWebApplication(out IntPtr ppvDataEnv)
            {
                IWebApplication? application = _webProject.Services?.GetContextService<IWebApplication, IWebApplication>();

                if (application == null)
                {
                    ppvDataEnv = IntPtr.Zero;
                    return HResult.NoInterface;
                }

                ppvDataEnv = Marshal.GetIUnknownForObject(application);
                return HResult.OK;
            }

            public int GetBrowseUrl(out string? browseURL)
            {
                browseURL = _webProject.Properties?.BrowseUrl?.AbsoluteUri;

                return browseURL != null ? HResult.OK : HResult.Fail;
            }

            // IVsWebProjectSupport, IVsWebProjectSupport2

            public int GetWebUrl(out string? ppWebUrl)
            {
                ppWebUrl = _webProject.Properties?.ApplicationUrl?.AbsoluteUri;

                return ppWebUrl != null ? HResult.OK : HResult.Fail;
            }

            public int GetWebPath(out string? ppWebPath)
            {
                ppWebPath = _webProject.Properties?.ApplicationDirectory;

                return ppWebPath != null ? HResult.OK : HResult.Fail;
            }

            public int GetWebProjectUrl(out string? ppWebProjectUrl)
            {
                ppWebProjectUrl = _webProject.Properties?.ProjectUrl?.AbsoluteUri;

                return ppWebProjectUrl != null ? HResult.OK : HResult.Fail;
            }

            public int GetWebProjectPath(out string? ppWebProjectPath)
            {
                ppWebProjectPath = _webProject.Properties?.ProjectDirectory;

                return ppWebProjectPath != null ? HResult.OK : HResult.Fail;
            }

            public int UsesIISWebServer(out bool usesIISWebServer)
            {
                IWebProjectProperties? props = _webProject.Properties;
                usesIISWebServer =  props != null &&
                                    (props.ServerType == ServerType.IIS || 
                                    (props.ServerType == ServerType.IISExpress && props.IISExpressUsesGlobalAppHostCfgFile));

                return HResult.OK;
            }

            public int OnReferenceAdded(string pszReferencePath)
            {
                throw new NotImplementedException();
            }

            public int OnFileAdded(string pszFilePath, bool foldersMustBeInProject)
            {
                throw new NotImplementedException();
            }

            public int GetIISMetabasePath(out string? strIISMetabasePath)
            {
                throw new NotImplementedException();
            }

            public int CodeFolderAdded(string relativeFolderUrl)
            {
                _specialWebFolderDataSource.AddCodeFolder(MakeProjectRelative(relativeFolderUrl));

                return HResult.OK;
            }

            public int CodeFolderRemoved(string relativeFolderUrl)
            {
                _specialWebFolderDataSource.RemoveCodeFolder(MakeProjectRelative(relativeFolderUrl));
                
                return HResult.OK;
            }

            private string MakeProjectRelative(string appRelativePath)
            {
                string applicationDirectory = _webProject.Properties?.ApplicationDirectory ?? string.Empty;

                string fullPath = Path.Combine(applicationDirectory, appRelativePath);

                return _project.MakeRelative(fullPath);
            }
        }
    }
}
