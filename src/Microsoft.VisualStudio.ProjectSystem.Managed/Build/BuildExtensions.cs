// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace Microsoft.VisualStudio.Build
{
    internal static class BuildExtensions
    {
        /// <summary>
        ///     Gets the unescaped, unevaluated value.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="element"/> is <see langword="null" />.
        /// </exception>
        public static string GetUnescapedValue(this ProjectPropertyElement element)
        {
            Requires.NotNull(element, nameof(element));

            return ProjectCollection.Unescape(element.Value);
        }

        /// <summary>
        ///     Returns a value indicating if the specified <see cref="ProjectItemInstance"/>
        ///     originated in an imported file.
        /// </summary>
        /// <returns>
        ///     <see langword="true"/> if <paramref name="item"/> originated in an imported file;
        ///     otherwise, <see langword="false"/> if it was defined in the project being built.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="item"/> is <see langword="null"/>.
        /// </exception>
        public static bool IsImported(this ProjectItemInstance item)
        {
            Requires.NotNull(item, nameof(item));

            string definingProjectFullPath = item.GetMetadataValue("DefiningProjectFullPath");
            string projectFullPath = item.Project.FullPath; // NOTE: This returns project being built, not owning target

            return !StringComparers.Paths.Equals(definingProjectFullPath, projectFullPath);
        }
    }
}
