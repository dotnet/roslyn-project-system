﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Properties.VisualBasic
{
    /// <summary>
    ///     Provides common well-known Visual Basic project property pages.
    /// </summary>
    internal static class VisualBasicProjectDesignerPage
    {
        public static readonly ProjectDesignerPageMetadata Application = new ProjectDesignerPageMetadata(new Guid("{8998E48E-B89A-4034-B66E-353D8C1FDC2E}"), pageOrder: 0, hasConfigurationCondition: false);
        public static readonly ProjectDesignerPageMetadata Compile = new ProjectDesignerPageMetadata(new Guid("{EDA661EA-DC61-4750-B3A5-F6E9C74060F5}"), pageOrder: 0, hasConfigurationCondition: true);
        public static readonly ProjectDesignerPageMetadata Package = new ProjectDesignerPageMetadata(new Guid("{21b78be8-3957-4caa-bf2f-e626107da58e}"), pageOrder: 0, hasConfigurationCondition: false);
        public static readonly ProjectDesignerPageMetadata References = new ProjectDesignerPageMetadata(new Guid("{4E43F4AB-9F03-4129-95BF-B8FF870AF6AB}"), pageOrder: 1, hasConfigurationCondition: false);
        public static readonly ProjectDesignerPageMetadata Debug = new ProjectDesignerPageMetadata(new Guid("{0273C280-1882-4ED0-9308-52914672E3AA}"), pageOrder: 2, hasConfigurationCondition: false);
        public static readonly ProjectDesignerPageMetadata Security = new ProjectDesignerPageMetadata(new Guid("{DF8F7042-0BB1-47D1-8E6D-DEB3D07698BD}"), pageOrder: 6, hasConfigurationCondition: false);
        public static readonly ProjectDesignerPageMetadata Signing = new ProjectDesignerPageMetadata(new Guid("{F8D6553F-F752-4DBF-ACB6-F291B744A792}"), pageOrder: 7, hasConfigurationCondition: false);
        public static readonly ProjectDesignerPageMetadata WPFApplication = new ProjectDesignerPageMetadata(new Guid("{00AA1F44-2BA3-4EAA-B54A-CE18000E6C5D}"), pageOrder: 0, hasConfigurationCondition: false);
        public static readonly ProjectDesignerPageMetadata XBAPSecurity = new ProjectDesignerPageMetadata(new Guid("{00A2C8FE-3844-41BE-9637-167454A7F1A7}"), pageOrder: 6, hasConfigurationCondition: false);
        public static readonly ProjectDesignerPageMetadata XBAPSigning = new ProjectDesignerPageMetadata(new Guid("{E4B54061-084D-4484-9D21-ECAE95A78A56}"), pageOrder: 7, hasConfigurationCondition: false);
    }
}
