// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio
{
    /// <summary>
    ///     Contains commonly used <see cref="IEqualityComparer{String}"/> instances.
    /// </summary>
    /// <remarks>
    ///     Mirrors values in <see cref="StringComparisons"/>.
    /// </remarks>
    internal static class StringComparers
    {
        public static StringComparer WorkspaceProjectContextIds => StringComparer.Ordinal;
        public static StringComparer Paths => StringComparer.OrdinalIgnoreCase;
        public static StringComparer PropertyNames => StringComparer.OrdinalIgnoreCase;
        public static StringComparer PropertyLiteralValues => StringComparer.OrdinalIgnoreCase;
        public static StringComparer PropertyValues => StringComparer.Ordinal;
        public static StringComparer RuleNames => StringComparer.OrdinalIgnoreCase;
        public static StringComparer CategoryNames => StringComparer.OrdinalIgnoreCase;
        public static StringComparer ConfigurationDimensionNames => StringComparer.Ordinal;
        public static StringComparer ConfigurationDimensionValues => StringComparer.Ordinal;
        public static StringComparer DependencyProviderTypes => StringComparer.OrdinalIgnoreCase;
        public static StringComparer DependencyTreeIds => StringComparer.OrdinalIgnoreCase;
        public static StringComparer ItemNames => StringComparer.OrdinalIgnoreCase;
        public static StringComparer ItemTypes => StringComparer.OrdinalIgnoreCase;
        public static StringComparer TargetNames => StringComparer.OrdinalIgnoreCase;
        public static StringComparer FrameworkIdentifiers => StringComparer.OrdinalIgnoreCase;
        public static StringComparer LibraryNames => StringComparer.Ordinal;
        public static StringComparer EnvironmentVariables => StringComparer.OrdinalIgnoreCase;
        public static StringComparer TelemetryEventNames => StringComparer.Ordinal;
        public static StringComparer NamedExports => StringComparer.OrdinalIgnoreCase;
        public static StringComparer UIPropertyNames => StringComparer.OrdinalIgnoreCase;
        public static StringComparer LaunchSettingsPropertyNames => StringComparer.Ordinal;
        public static StringComparer LaunchProfileNames => StringComparer.Ordinal;
        public static StringComparer LaunchProfileProperties => StringComparer.Ordinal;
        public static StringComparer LaunchProfileCommandNames => StringComparer.Ordinal;
        public static StringComparer UserEnteredSearchTermIgnoreCase => StringComparer.CurrentCultureIgnoreCase;
        public static StringComparer ProjectTreeCaptionIgnoreCase => StringComparer.OrdinalIgnoreCase;
        public static StringComparer LanguageIdentifiers => StringComparer.Ordinal;
        public static StringComparer LanguageIdentifiersIgnoreCase => StringComparer.OrdinalIgnoreCase;
    }

    /// <summary>
    ///     Contains commonly used <see cref="StringComparison"/> instances.
    /// </summary>
    /// <remarks>
    ///     Mirrors values in <see cref="StringComparers"/>.
    /// </remarks>
    internal static class StringComparisons
    {
        public static StringComparison WorkspaceProjectContextIds => StringComparison.Ordinal;
        public static StringComparison Paths => StringComparison.OrdinalIgnoreCase;
        public static StringComparison PropertyNames => StringComparison.OrdinalIgnoreCase;
        public static StringComparison PropertyLiteralValues => StringComparison.OrdinalIgnoreCase;
        public static StringComparison PropertyValues => StringComparison.Ordinal;
        public static StringComparison RuleNames => StringComparison.OrdinalIgnoreCase;
        public static StringComparison ConfigurationDimensionNames => StringComparison.Ordinal;
        public static StringComparison ConfigurationDimensionValues => StringComparison.Ordinal;
        public static StringComparison DependencyProviderTypes => StringComparison.OrdinalIgnoreCase;
        public static StringComparison DependencyTreeIds => StringComparison.OrdinalIgnoreCase;
        public static StringComparison ItemNames => StringComparison.OrdinalIgnoreCase;
        public static StringComparison ItemTypes => StringComparison.OrdinalIgnoreCase;
        public static StringComparison TargetNames => StringComparison.OrdinalIgnoreCase;
        public static StringComparison FrameworkIdentifiers => StringComparison.OrdinalIgnoreCase;
        public static StringComparison LibraryNames => StringComparison.Ordinal;
        public static StringComparison EnvironmentVariables => StringComparison.OrdinalIgnoreCase;
        public static StringComparison TelemetryEventNames => StringComparison.Ordinal;
        public static StringComparison NamedExports => StringComparison.OrdinalIgnoreCase;
        public static StringComparison UIPropertyNames => StringComparison.OrdinalIgnoreCase;
        public static StringComparison LaunchSettingsPropertyNames => StringComparison.Ordinal;
        public static StringComparison LaunchProfileNames => StringComparison.Ordinal;
        public static StringComparison LaunchProfileProperties => StringComparison.Ordinal;
        public static StringComparison LaunchProfileCommandNames => StringComparison.Ordinal;
        public static StringComparison UserEnteredSearchTermIgnoreCase => StringComparison.CurrentCultureIgnoreCase;
        public static StringComparison ProjectTreeCaptionIgnoreCase => StringComparison.OrdinalIgnoreCase;
        public static StringComparison LanguageIdentifiers => StringComparison.Ordinal;
        public static StringComparison LanguageIdentifiersIgnoreCase => StringComparison.OrdinalIgnoreCase;
    }
}
