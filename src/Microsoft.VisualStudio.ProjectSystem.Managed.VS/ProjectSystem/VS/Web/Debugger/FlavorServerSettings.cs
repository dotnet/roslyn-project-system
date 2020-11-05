// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Xml.Linq;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Web
{
    /// <summary>
    /// Represents the settings stored in the FlavorProperties section of the project file
    /// </summary>
    internal class FlavorServerSettings
    {
        private static class PropertyNames
        {
            // project file tag names
            public const string RootNode = "WebProjectProperties";
            public const string UsingIIS = "UseIIS";
            public const string ServerPort = "DevelopmentServerPort";
            public const string ServerVPath = "DevelopmentServerVPath";
            public const string IISUrl = "IISUrl";
            public const string IISAppRootUrl = "IISAppRootUrl";
            public const string OverrideIISAppRootUrl = "OverrideIISAppRootUrl";
            public const string StartPageUrl = "StartPageUrl";
            public const string DebugCurrentPage = "DebugCurrentPage";
            public const string AutoAssignPort = "AutoAssignPort";
            public const string EnableWcfTestClientForSVC = "EnableWcfTestClientForSVC";
            public const string EnableWcfTestClientForSVCDefaultValue = "EnableWcfTestClientForSVCDefaultValue";
            public const string SaveServerSettingsInUserFile = "SaveServerSettingsInUserFile";
            public const string UseCustomServer = "UseCustomServer";
            public const string CustomServerUrl = "CustomServerUrl";

            // User File only settings which map to debug options
            public const string AspNetDebugging = "AspNetDebugging";
            public const string SilverlightDebugging = "SilverlightDebugging";
            public const string NativeDebugging = "NativeDebugging";
            public const string SQLDebugging = "SQLDebugging";
            public const string NTLMAuth = "NTLMAuthentication";
            public const string ServerDirectoryListing = "ServerDirectoryListing";
            public const string UseClassLibDebugging = "UseClassLibraryDebugging";
            public const string HideWebConfig = "HideWebConfig";
            public const string AlwaysStartWebServerOnDebug = "AlwaysStartWebServerOnDebug";
        };

        public FlavorServerSettings() { }
        public FlavorServerSettings(FlavorServerSettings other)
        {
            UseIIS = other.UseIIS;
            SslPort = other.SslPort;
            DevelopmentServerPort = other.DevelopmentServerPort;
            DevelopmentServerVPath = other.DevelopmentServerVPath;
            IISUrl = other.IISUrl;
            IISAppRootOverrideUrl = other.IISAppRootOverrideUrl;
            UseIISAppRootOverrideUrl = other.UseIISAppRootOverrideUrl;
            CustomServerUrl = other.CustomServerUrl;
            UseCustomServer = other.UseCustomServer;
        }

        public static FlavorServerSettings CreateFromXml(XElement flavorRoot)
        {
            var settings = new FlavorServerSettings();

            var webPropertiesRoot = flavorRoot.Element(PropertyNames.RootNode);
            if (webPropertiesRoot == null)
            {
                return settings;
            }

            if (TryReadBoolValue(webPropertiesRoot, PropertyNames.UsingIIS, out bool boolVal))
            {
                settings.UseIIS = boolVal;
            }

            if (TryReadIntValue(webPropertiesRoot, PropertyNames.ServerPort, out int intVal))
            {
                settings.DevelopmentServerPort = intVal;
            }

            if (TryReadStringValue(webPropertiesRoot, PropertyNames.ServerVPath, out string? strVal) && strVal != null)
            {
                settings.DevelopmentServerVPath = strVal;
            }

            if (TryReadStringValue(webPropertiesRoot, PropertyNames.IISUrl, out strVal))
            {
                settings.IISUrl = strVal;
            }

            if (TryReadStringValue(webPropertiesRoot, PropertyNames.IISAppRootUrl, out strVal))
            {
                settings.IISAppRootOverrideUrl = strVal;
            }

            if (TryReadBoolValue(webPropertiesRoot, PropertyNames.OverrideIISAppRootUrl, out boolVal))
            {
                settings.UseIISAppRootOverrideUrl = boolVal;
            }

            if (TryReadBoolValue(webPropertiesRoot, PropertyNames.UseCustomServer, out boolVal))
            {
                settings.UseCustomServer = boolVal;
            }

            if (TryReadStringValue(webPropertiesRoot, PropertyNames.CustomServerUrl, out strVal))
            {
                settings.CustomServerUrl = strVal;
            }

            return settings;
        }

        public XElement ToXml()
        {
            if (UseIIS)
            {
            }

            return new XElement(PropertyNames.RootNode);
        }

        public bool UseIIS { get; private set; }
        public int SslPort { get; private set; }
        public int DevelopmentServerPort { get; private set; }
        public string DevelopmentServerVPath { get; private set; } = "/";
        public string? IISUrl { get; private set; }
        public string? IISAppRootOverrideUrl { get; private set; }
        public bool UseIISAppRootOverrideUrl { get; private set; }
        public bool UseCustomServer { get; private set; }
        public string? CustomServerUrl { get; private set; }

        private static bool TryReadBoolValue(XElement root, string propertyName, out bool boolValue)
        {
            boolValue = false;
            string? elementValue = root.Element(propertyName)?.Value;
            if (bool.TryParse(elementValue, out bool value))
            {
                boolValue = value;
                return true;
            }

            return false;
        }

        private static bool TryReadIntValue(XElement root, string propertyName, out int intValue)
        {
            intValue = 0;
            string? elementValue = root.Element(propertyName)?.Value;
            if (int.TryParse(elementValue, out int value))
            {
                intValue = value;
                return true;
            }

            return false;
        }

        private static bool TryReadStringValue(XElement root, string propertyName, out string? stringValue)
        {
            stringValue = null;
            string? elementValue = root.Element(propertyName)?.Value;
            if (!string.IsNullOrEmpty(elementValue))
            {
                stringValue = elementValue;
                return true;
            }

            return false;
        }
    }
}
