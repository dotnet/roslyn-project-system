﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.ProjectSystem.LanguageServices;
using Microsoft.VisualStudio.ProjectSystem.VS.Tree.Dependencies.CrossTarget;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Tree.Dependencies
{
    /// <summary>
    /// Model for creating telemetry events when dependency tree is updated.
    /// It maintains some light state for each Target Framework to keep track
    /// of the status of Rule processing so we can heuristically determine
    /// when the dependency tree is "ready"
    /// </summary>
    [Export(typeof(IDependencyTreeTelemetryService))]
    [AppliesTo(ProjectCapability.DependenciesTree)]
    internal class DependencyTreeTelemetryService : IDependencyTreeTelemetryService
    {
        private const string TelemetryEventName = "TreeUpdated";
        private const string UnresolvedLabel = "Unresolved";
        private const string ResolvedLabel = "Resolved";
        private const string ProjectProperty = "Project";

        /// <summary>
        /// Maintain state for each target framework
        /// </summary>
        public class TelemetryState
        {
            public Dictionary<string, bool> ObservedRuleChanges { get; } = new Dictionary<string, bool>();
            public bool ObservedDesignTime { get; set; } = false;
            public bool StopTelemetry { get; set; } = false;

            public bool IsReadyToResolve()
            {
                return ObservedDesignTime 
                    && ObservedRuleChanges.Any() 
                    && ObservedRuleChanges.All(entry => entry.Value);
            }
        }

        private readonly ITelemetryService _telemetryService;
        private readonly Dictionary<ITargetFramework, TelemetryState> telemetryStates = 
            new Dictionary<ITargetFramework, TelemetryState>();
        private readonly string projectId;
        private bool stopTelemetry = false;
        private bool isReadyToResolve = false;

        [ImportingConstructor]
        public DependencyTreeTelemetryService(
            UnconfiguredProject project,
            ITelemetryService telemetryService)
        {
            _telemetryService = telemetryService;

            projectId = GetUniqueId(project);
        }

        private string GetUniqueId(UnconfiguredProject project)
        {
            using (var sha1 = SHA256.Create())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(project.FullPath));
                return BitConverter.ToString(hash);
            }
        }

        /// <summary>
        /// Fire a telemetry event when the dependency tree update is complete, 
        /// conditioned on three heuristics:
        ///  1. Telemetry is fired with 'Unresolved' label if any target frameworks are
        ///     yet to process Rules from a Design Time build. This is tracked by 
        ///     TelemetryState.ObservedDesignTime
        ///  2. Telemetry is fired with 'Unresolved' label if any relevant Rules from design
        ///     time builds are yet to report any changes. Relevant Rules are determined
        ///     based on the 'ICrossTargetRuleHandler's that have processed. This is tracked
        ///     by TelemetryState.ObservedRuleChanges
        ///  3. Telemetry fires with 'Resolved' label when 1. and 2. are no longer true
        ///  4. Telemetry stops firing once all target frameworks observe an Evaluation following
        ///     the first design time build, or once it has fired once with 'Resolved' label. 
        ///     This prevents telemetry events caused by project changes and tree updates after initial load has completed.
        /// </summary>
        public void ObserveTreeUpdateCompleted()
        {
            if (stopTelemetry) return;

            stopTelemetry = isReadyToResolve ||
                (telemetryStates.Any() && telemetryStates.Values.All(t => t.StopTelemetry));

            _telemetryService.PostProperty($"{TelemetryEventName}/{(isReadyToResolve ? ResolvedLabel : UnresolvedLabel)}",
                ProjectProperty, projectId);
        }

        public void ObserveUnresolvedRules(ITargetFramework targetFramework, IEnumerable<string> rules)
        {
            if (stopTelemetry) return;

            if (!telemetryStates.TryGetValue(targetFramework, out var telemetryState))
            {
                telemetryState = new TelemetryState();
                telemetryStates[targetFramework] = telemetryState;
            }

            foreach (var rule in rules)
            {
                // observe all unresolved rules by default ignoring whether they have actual changes
                UpdateObservedRuleChanges(telemetryState, rule);
            }
        }

        public void ObserveHandlerRulesChanges(
            ITargetFramework targetFramework,
            IEnumerable<string> handlerRules, 
            IImmutableDictionary<string, IProjectChangeDescription> projectChanges)
        {
            if (stopTelemetry) return;

            if (telemetryStates.TryGetValue(targetFramework, out var telemetryState))
            {
                foreach (var rule in handlerRules)
                {
                    var hasChanges = projectChanges.ContainsKey(rule)
                        && projectChanges[rule].Difference.AnyChanges;

                    UpdateObservedRuleChanges(telemetryState, rule, hasChanges);
                }
            }
        }

        public void ObserveCompleteHandlers(ITargetFramework targetFramework, RuleHandlerType handlerType)
        {
            if (stopTelemetry) return;

            if (telemetryStates.TryGetValue(targetFramework, out var telemetryState))
            {
                telemetryState.ObservedDesignTime |= handlerType == RuleHandlerType.DesignTimeBuild;
                telemetryState.StopTelemetry |= telemetryState.ObservedDesignTime && handlerType == RuleHandlerType.Evaluation;
            }

            isReadyToResolve = telemetryStates.Any() && telemetryStates.Values.All(s => s.IsReadyToResolve());
        }

        private void UpdateObservedRuleChanges(TelemetryState telemetryState, string rule, bool hasChanges = true)
        {
            if (telemetryState.ObservedRuleChanges.TryGetValue(rule, out var anyChanges))
            {
                telemetryState.ObservedRuleChanges[rule] = anyChanges || hasChanges;
            }
            else
            {
                telemetryState.ObservedRuleChanges.Add(rule, hasChanges);
            }
        }
    }
}
