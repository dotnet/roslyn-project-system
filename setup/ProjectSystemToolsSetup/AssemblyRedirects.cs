// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using Microsoft.VisualStudio;

// These provide code-base binding redirects for the assemblies we provide in this VSIX. For NGEN purposes,
// we also need to update src/appid/devenv/stub/devenv.urt.config.tt inside VS repo, which is done automatically 
// via the Roslyn Insertion tool when it consumes artifacts\Debug\DevDivInsertionFiles\DependentAssemblyVersions.csv.
[assembly: ProvideCodeBaseBindingRedirection("Microsoft.VisualStudio.ProjectSystemTools.BackEnd")]
[assembly: ProvideCodeBaseBindingRedirection("Microsoft.VisualStudio.ProjectSystemTools.Shared")]
