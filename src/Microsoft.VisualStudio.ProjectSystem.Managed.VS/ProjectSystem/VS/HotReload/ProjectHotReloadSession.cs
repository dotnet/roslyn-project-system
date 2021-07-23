// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Debugger.Contracts.HotReload;
using Microsoft.VisualStudio.HotReload.Components.DeltaApplier;

namespace Microsoft.VisualStudio.ProjectSystem.VS.HotReload
{
    internal class ProjectHotReloadSession : IManagedHotReloadAgent, IProjectHotReloadSession
    {
        private readonly string _runtimeVersion;
        private readonly Lazy<IHotReloadAgentManagerClient> _hotReloadAgentManagerClient;
        private readonly Lazy<IHotReloadDiagnosticOutputService> _hotReloadOutputService;
        private readonly Lazy<IManagedDeltaApplierCreator> _deltaApplierCreator;
        private readonly IProjectHotReloadSessionCallback _callback;

        private bool _sessionActive;
        private IDeltaApplier? _deltaApplier;

        public ProjectHotReloadSession(
            string name,
            string runtimeVersion,
            Lazy<IHotReloadAgentManagerClient> hotReloadAgentManagerClient,
            Lazy<IHotReloadDiagnosticOutputService> hotReloadOutputService,
            Lazy<IManagedDeltaApplierCreator> deltaApplierCreator,
            IProjectHotReloadSessionCallback callback)
        {
            Name = name;
            _runtimeVersion = runtimeVersion;
            _hotReloadAgentManagerClient = hotReloadAgentManagerClient;
            _hotReloadOutputService = hotReloadOutputService;
            _deltaApplierCreator = deltaApplierCreator;
            _callback = callback;
        }

        // IProjectHotReloadSession

        public string Name { get; }

        public async Task ApplyChangesAsync(CancellationToken cancellationToken)
        {
            if (_sessionActive)
            {
                await _hotReloadAgentManagerClient.Value.ApplyUpdatesAsync(cancellationToken);
            }
        }

        public async Task<bool> ApplyLaunchVariablesAsync(IDictionary<string, string> envVars, CancellationToken cancellationToken)
        {
            EnsureDeltaApplierforSession();
            if (_deltaApplier is not null)
            {
                // TODO: Simplify this once ApplyProcessEnvironmentVariablesAsync takes an IDictionary instead of a Dictionary.
                if (envVars is Dictionary<string, string> envVarsAsDictionary)
                {
                    return await _deltaApplier.ApplyProcessEnvironmentVariablesAsync(envVarsAsDictionary, cancellationToken);
                }
                else
                {
                    envVarsAsDictionary = new Dictionary<string, string>(envVars);
                    bool result = await _deltaApplier.ApplyProcessEnvironmentVariablesAsync(envVarsAsDictionary, cancellationToken);
                    foreach ((string name, string value) in envVarsAsDictionary)
                    {
                        envVars[name] = value;
                    }

                    return result;
                }
            }

            return false;
        }

        public async Task StartSessionAsync(CancellationToken cancellationToken)
        {
            if (!_sessionActive)
            {
                await _hotReloadAgentManagerClient.Value.AgentStartedAsync(this, cancellationToken);
                await WriteToOutputWindowAsync(VSResources.HotReloadStartSession);
                _sessionActive = true;
                EnsureDeltaApplierforSession();
            }
        }

        public async Task StopSessionAsync(CancellationToken cancellationToken)
        {
            if (_sessionActive)
            {
                _sessionActive = false;

                await _hotReloadAgentManagerClient.Value.AgentTerminatedAsync(this, cancellationToken);
                await WriteToOutputWindowAsync(VSResources.HotReloadStopSession);
            }
        }

        // IManagedHotReloadAgent

        public async ValueTask ApplyUpdatesAsync(ImmutableArray<ManagedHotReloadUpdate> updates, CancellationToken cancellationToken)
        {
            if (!_sessionActive)
            {
                await WriteToOutputWindowAsync($"ApplyUpdatesAsync called but the session is not active.");
                return;
            }

            if (_deltaApplier is null)
            {
                await WriteToOutputWindowAsync($"ApplyUpdatesAsync called but we have no delta applier.");
            }

            if (!_sessionActive || _deltaApplier is null)
            {
                return;
            }

            try
            {
                await WriteToOutputWindowAsync(VSResources.HotReloadSendingUpdates);

                ApplyResult result = await _deltaApplier.ApplyUpdatesAsync(updates, cancellationToken);
                if (result == ApplyResult.Success || result == ApplyResult.SuccessRefreshUI)
                {
                    await WriteToOutputWindowAsync(VSResources.HotReloadApplyUpdatesSuccessful);
                    if (_callback is not null)
                    {
                        await _callback.OnAfterChangesAppliedAsync(cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                string message = $"{ex.GetType()}: {ex.Message}";

                await WriteToOutputWindowAsync(string.Format(VSResources.HotReloadApplyUpdatesFailure, message));
                throw;
            }
        }

        public async ValueTask<ImmutableArray<string>> GetCapabilitiesAsync(CancellationToken cancellationToken)
        {
            // Delegate to the delta applier for the session
            if (_deltaApplier is not null)
            {
                return await _deltaApplier.GetCapabilitiesAsync(cancellationToken);
            }
            return ImmutableArray<string>.Empty;
        }

        public async ValueTask ReportDiagnosticsAsync(ImmutableArray<ManagedHotReloadDiagnostic> diagnostics, CancellationToken cancellationToken)
        {
            await WriteToOutputWindowAsync(VSResources.HotReloadErrorsInApplication);

            foreach (ManagedHotReloadDiagnostic diagnostic in diagnostics)
            {
                await WriteToOutputWindowAsync($"{diagnostic.FilePath}({diagnostic.Span.StartLine},{diagnostic.Span.StartColumn},{diagnostic.Span.EndLine},{diagnostic.Span.EndColumn}): {diagnostic.Message}");
            }
        }

        public async ValueTask RestartAsync(CancellationToken cancellationToken)
        {
            await WriteToOutputWindowAsync(VSResources.HotReloadRestartInProgress);

            await _callback.RestartProjectAsync(cancellationToken);

            // TODO: Should we stop the session here? Or does someone else do it?
            // TODO: Should we handle rebuilding here? Or do we expect the callback to handle it?
        }

        public async ValueTask StopAsync(CancellationToken cancellationToken)
        {
            await WriteToOutputWindowAsync(VSResources.HotReloadStoppingApplication);

            await _callback.StopProjectAsync(cancellationToken);

            // TODO: Should we stop the session here? Or does someone else do it?
        }

        public ValueTask<bool> SupportsRestartAsync(CancellationToken cancellationToken)
        {
            return new ValueTask<bool>(_callback.SupportsRestart);
        }

        private Task WriteToOutputWindowAsync(string message)
        {
            return _hotReloadOutputService.Value.WriteLineAsync($"{Name}: {message}");
        }

        private void EnsureDeltaApplierforSession()
        {
            if (_deltaApplier is null)
            {
                _deltaApplier = (IDeltaApplier)(_callback.GetDeltaApplier()
                    ?? _deltaApplierCreator.Value.CreateManagedDeltaApplier(_runtimeVersion));
            }
        }
    }
}
