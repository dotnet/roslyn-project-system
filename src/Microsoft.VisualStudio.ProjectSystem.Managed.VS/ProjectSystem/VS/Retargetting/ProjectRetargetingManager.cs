// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Threading.Tasks;

using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Retargeting
{
    [Export(typeof(IProjectRetargetingManager))]
    [AppliesTo(ProjectCapabilities.AlwaysApplicable)]
    internal class ProjectRetargetingManager : IProjectRetargetingManager, IVsSolutionEvents, IDisposable
    {
        private readonly IVsService<SVsSolution, IVsSolution> _solutionService;
        private readonly IVsService<SVsTrackProjectRetargeting, IVsTrackProjectRetargeting2> _retargetingService;
        private readonly IProjectThreadingService _threadingService;
        private readonly ITaskDelayScheduler _taskDelayScheduler;
        private IVsSolution? _solution;
        private bool _solutionOpened;
        private __RETARGET_CHECK_OPTIONS _retargetCheckOption = __RETARGET_CHECK_OPTIONS.RCO_FIRST_SOLUTION_LOAD;

        private ImmutableHashSet<IVsProjectTargetDescription> _registeredDescriptions = ImmutableHashSet<IVsProjectTargetDescription>.Empty;
        private ImmutableList<string> _projectsToWaitFor = ImmutableList<string>.Empty;
        private bool _needsRetarget = false;
        private uint _cookie;

        [ImportingConstructor]
        public ProjectRetargetingManager(IVsService<SVsSolution, IVsSolution> solutionService,
                                         IVsService<SVsTrackProjectRetargeting, IVsTrackProjectRetargeting2> retargetingService,
                                         IProjectThreadingService threadingService)
        {
            _solutionService = solutionService;
            _retargetingService = retargetingService;
            _threadingService = threadingService;

            _taskDelayScheduler = new TaskDelayScheduler(TimeSpan.FromMilliseconds(250), threadingService, default);
        }

        public void Dispose()
        {
            if (_cookie != VSConstants.VSCOOKIE_NIL && _solution != null)
            {
                Verify.HResult(_solution.UnadviseSolutionEvents(_cookie));

                _cookie = VSConstants.VSCOOKIE_NIL;
            }
        }

        public void ReportProjectMightRetarget(string projectFile)
        {
            if (_solution == null)
            {
                _threadingService.ExecuteSynchronously(async () =>
                {
                    await _threadingService.SwitchToUIThread();
                    if (_solution != null) return;

                    _solution = await _solutionService.GetValueAsync();

                    Verify.HResult(_solution.AdviseSolutionEvents(this, out _cookie));
                });
            }

            ThreadingTools.ApplyChangeOptimistically(ref _projectsToWaitFor, col =>
            {
                if (!col.Contains(projectFile))
                {
                    col = col.Add(projectFile);
                }

                return col;
            });
        }

        public void ReportProjectNeedsRetargeting(string projectFile, ProjectTargetChange change)
        {
            ThreadingTools.ApplyChangeOptimistically(ref _projectsToWaitFor, projectFile, (col, file) =>
            {
                if (col.Contains(file))
                {
                    col = col.Remove(file);
                }

                return col;
            });

            if (change != ProjectTargetChange.None)
            {
                // Most of the time all target descriptions will already be registered, so avoid changing to the UI thread if we can
                if (GetAllTargetDescriptions(change).Except(_registeredDescriptions).Any())
                {
                    _threadingService.ExecuteSynchronously(async () =>
                    {
                        await _threadingService.SwitchToUIThread();

                        IVsTrackProjectRetargeting2 service = await _retargetingService.GetValueAsync();

                        if (change.NewTargetDescription != null)
                        {
                            InitializeTargetDescription(service, change.NewTargetDescription);
                        }
                        if (change.CurrentTargetDescription != null)
                        {
                            InitializeTargetDescription(service, change.CurrentTargetDescription);
                        }
                    });
                }

                _needsRetarget = true;
            }

            TryTriggerRetarget();

            void InitializeTargetDescription(IVsTrackProjectRetargeting2 service, TargetDescriptionBase description)
            {
                if (ThreadingTools.ApplyChangeOptimistically(ref _registeredDescriptions, description, (col, d) => col.Add(d)))
                {
                    ErrorHandler.ThrowOnFailure(service.RegisterProjectTarget(description));
                }

                if (description.SetupDriver != Guid.Empty && description.ActualSetupDriver == null)
                {
                    ErrorHandler.ThrowOnFailure(service.GetSetupDriver(description.SetupDriver, out IVsProjectAcquisitionSetupDriver driver));

                    description.ActualSetupDriver = driver;
                }
            }

            static IEnumerable<TargetDescriptionBase> GetAllTargetDescriptions(ProjectTargetChange change)
            {
                if (change.NewTargetDescription != null)
                {
                    yield return change.NewTargetDescription;
                }
                if (change.CurrentTargetDescription != null)
                {
                    yield return change.CurrentTargetDescription;
                }
            }
        }

        private void TryTriggerRetarget()
        {
            if (_projectsToWaitFor.Count == 0 && _needsRetarget && _solutionOpened)
            {
                _ = _taskDelayScheduler.ScheduleAsyncTask(AllProjectsDoneLoading, default);
            }
        }

        public async Task AllProjectsDoneLoading(CancellationToken cancellationToken)
        {
            await _threadingService.SwitchToUIThread();

            _needsRetarget = false;

            IVsTrackProjectRetargeting2 service = await _retargetingService.GetValueAsync(cancellationToken);

            ErrorHandler.ThrowOnFailure(service.CheckSolutionForRetarget((uint)_retargetCheckOption));

            // The next time we call the platform to retarget, its just going to be a normal retarget
            _retargetCheckOption = __RETARGET_CHECK_OPTIONS.RCO_PROJECT_RETARGET;
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            _solutionOpened = true;
            TryTriggerRetarget();
            return HResult.OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            // Reset as next retarget will be for solution load
            _retargetCheckOption = __RETARGET_CHECK_OPTIONS.RCO_FIRST_SOLUTION_LOAD;
            _solutionOpened = false;
            return HResult.OK;
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) => HResult.OK;
        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => HResult.OK;
        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => HResult.OK;
        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => HResult.OK;
        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => HResult.OK;
        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => HResult.OK;
        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => HResult.OK;
        public int OnBeforeCloseSolution(object pUnkReserved) => HResult.OK;
    }
}
