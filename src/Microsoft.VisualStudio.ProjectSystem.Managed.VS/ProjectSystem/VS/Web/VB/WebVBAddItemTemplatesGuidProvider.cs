// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.ComponentModel.Composition;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    /// <summary>
    /// Required by CPS in Dev15+ to specify the add item guid of the Web project system for C# tempaltes
    /// </summary>
    [Export(typeof(IAddItemTemplatesGuidProvider))]
    [AppliesTo(ProjectCapability.AspNet + " & " + ProjectCapability.VisualBasic)]
    [Order(10000)]
    internal class WebVBAddItemTemplatesGuidProvider : IAddItemTemplatesGuidProvider
    {
        private const string WebVBAddItemTemplateGuidString = "349C5854-65DF-11DA-9384-00065B846F21";
        private static Guid s_itemTemplateGuid = new Guid(WebVBAddItemTemplateGuidString);

        [ImportingConstructor]
        public WebVBAddItemTemplatesGuidProvider(UnconfiguredProject unconfiguredProject)
        {
            UnconfiguredProject = unconfiguredProject;
        }

        private UnconfiguredProject UnconfiguredProject { get; }

        public Guid AddItemTemplatesGuid => s_itemTemplateGuid;
    }
}
