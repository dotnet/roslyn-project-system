// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    internal class ProjectTargetChange : IProjectTargetChange
    {
        /// <summary>
        /// Creates a project target change for a prerequisite, where the target description is the CurrentTargetDescription
        /// </summary>
        public static ProjectTargetChange CreateForPrerequisite(TargetDescriptionBase targetDescription) => new ProjectTargetChange(targetDescription);

        /// <summary>
        /// Creates a project target change for a prerequisite, where the target description is the NewTargetDescription
        /// </summary>
        public static ProjectTargetChange CreateForRetarget(TargetDescriptionBase targetDescription, IProjectRetargetCheckProvider provider) => new ProjectTargetChange(targetDescription, provider);

        internal static ProjectTargetChange None = new ProjectTargetChange();

        private readonly TargetDescriptionBase? _newTargetDescription;
        private readonly TargetDescriptionBase? _currentTargetDescription;
        private readonly IProjectRetargetCheckProvider? _provider;

        private ProjectTargetChange()
        {
        }

        private ProjectTargetChange(TargetDescriptionBase currentTargetDescription)
        {
            _currentTargetDescription = currentTargetDescription;
        }

        private ProjectTargetChange(TargetDescriptionBase newTargetDescription, IProjectRetargetCheckProvider provider)
        {
            _newTargetDescription = newTargetDescription;
            _provider = provider;
        }

        public TargetDescriptionBase? NewTargetDescription => _newTargetDescription;
        public TargetDescriptionBase? CurrentTargetDescription => _currentTargetDescription;

        public IProjectRetargetCheckProvider? RetargetProvider => _provider;

        public Guid NewTargetId => _newTargetDescription?.TargetId ?? Guid.Empty;

        public Guid CurrentTargetId => _currentTargetDescription?.TargetId ?? Guid.Empty;

        public bool ReloadProjectOnSuccess => true;

        public bool UnloadOnFailure => true;

        public bool UnloadOnCancel => true;
    }
}
