﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Microsoft.VisualStudio.ProjectSystem.LanguageServices
{
    public class VisualBasicParseCommandLineArguments : IParseCommandLineArguments
    {
        [ImportingConstructor]
        public VisualBasicParseCommandLineArguments()
        {
        }

        public CommandLineArguments Parse(IEnumerable<string> args, string baseDirectory)
        {
            return CommandLineArguments.FromCommonCommandLineArguments(
                VisualBasicCommandLineParser.Default.Parse(args, baseDirectory, sdkDirectory: null, additionalReferenceDirectories: null));
        }

        [Export(typeof(IParseCommandLineArguments))]
        [AppliesTo(ProjectCapability.VisualBasic)]
        public IParseCommandLineArguments Parser => this;
    }
}
