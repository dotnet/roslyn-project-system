// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.IO;
using Microsoft.VisualStudio.ProjectSystem.Build;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Telemetry;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.ProjectSystem.UpToDate
{
    public sealed class BuildUpToDateCheckTests : IDisposable
    {
        private const string _projectFullPath = "C:\\Dev\\Solution\\Project\\Project.csproj";
        private const string _msBuildProjectFullPath = "NewProjectFullPath";
        private const string _msBuildProjectDirectory = "NewProjectDirectory";
        private const string _msBuildAllProjects = "Project1;Project2";
        private const string _outputPath = "NewOutputPath";

        private readonly IImmutableList<IItemType> _itemTypes = ImmutableList<IItemType>.Empty
            .Add(new ItemType("None", true))
            .Add(new ItemType("Content", true))
            .Add(new ItemType("Compile", true))
            .Add(new ItemType("Resource", true));

        private readonly List<ITelemetryServiceFactory.TelemetryParameters> _telemetryEvents = new();
        private readonly BuildUpToDateCheck _buildUpToDateCheck;
        private readonly ITestOutputHelper _output;
        private readonly IFileSystemMock _fileSystem;

        // Values returned by mocks that may be modified in test cases as needed
        private int _projectVersion = 1;
        private bool _isTaskQueueEmpty = true;
        private bool _isFastUpToDateCheckEnabled = true;

        public BuildUpToDateCheckTests(ITestOutputHelper output)
        {
            _output = output;

            // NOTE most of these mocks are only present to prevent NREs in Initialize

            // Enable "Info" log level, as we assert logged messages in tests
            var projectSystemOptions = new Mock<IProjectSystemOptions>();
            projectSystemOptions.Setup(o => o.GetFastUpToDateLoggingLevelAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(LogLevel.Info);
            projectSystemOptions.Setup(o => o.GetIsFastUpToDateCheckEnabledAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _isFastUpToDateCheckEnabled);

            var projectCommonServices = IProjectCommonServicesFactory.CreateWithDefaultThreadingPolicy();
            var jointRuleSource = new ProjectValueDataSource<IProjectSubscriptionUpdate>(projectCommonServices);
            var sourceItemsRuleSource = new ProjectValueDataSource<IProjectSubscriptionUpdate>(projectCommonServices);
            var projectSnapshotSource = new ProjectValueDataSource<IProjectSnapshot>(projectCommonServices);
            var projectCatalogSource = new ProjectValueDataSource<IProjectCatalogSnapshot>(projectCommonServices);

            var projectSubscriptionService = new Mock<IProjectSubscriptionService>();
            projectSubscriptionService.SetupGet(o => o.JointRuleSource).Returns(jointRuleSource);
            projectSubscriptionService.SetupGet(o => o.ProjectSource).Returns(projectSnapshotSource);
            projectSubscriptionService.SetupGet(o => o.SourceItemsRuleSource).Returns(sourceItemsRuleSource);
            projectSubscriptionService.SetupGet(o => o.ProjectCatalogSource).Returns(projectCatalogSource);

            var configuredProjectServices = ConfiguredProjectServicesFactory.Create(projectSubscriptionService: projectSubscriptionService.Object);

            var configuredProject = new Mock<ConfiguredProject>();
            configuredProject.SetupGet(c => c.ProjectVersion).Returns(() => _projectVersion);
            configuredProject.SetupGet(c => c.Services).Returns(configuredProjectServices);
            configuredProject.SetupGet(c => c.UnconfiguredProject).Returns(UnconfiguredProjectFactory.Create(fullPath: _projectFullPath));

            var projectAsynchronousTasksService = new Mock<IProjectAsynchronousTasksService>();
            projectAsynchronousTasksService.SetupGet(s => s.UnloadCancellationToken).Returns(CancellationToken.None);
            projectAsynchronousTasksService.Setup(s => s.IsTaskQueueEmpty(ProjectCriticalOperation.Build)).Returns(() => _isTaskQueueEmpty);

            var lastWriteTimeUtc = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            _fileSystem = new IFileSystemMock();
            _fileSystem.AddFile(_msBuildProjectFullPath, lastWriteTimeUtc);
            _fileSystem.AddFile("Project1", lastWriteTimeUtc);
            _fileSystem.AddFile("Project2", lastWriteTimeUtc);
            _fileSystem.AddFolder(_msBuildProjectDirectory);
            _fileSystem.AddFolder(_outputPath);

            _buildUpToDateCheck = new BuildUpToDateCheck(
                projectSystemOptions.Object,
                configuredProject.Object,
                projectAsynchronousTasksService.Object,
                IProjectItemSchemaServiceFactory.Create(),
                ITelemetryServiceFactory.Create(telemetryParameters => _telemetryEvents.Add(telemetryParameters)),
                _fileSystem);
        }

        public void Dispose() => _buildUpToDateCheck.Dispose();

        private async Task SetupAsync(
            Dictionary<string, IProjectRuleSnapshotModel>? projectSnapshot = null,
            Dictionary<string, IProjectRuleSnapshotModel>? sourceSnapshot = null,
            IEnumerable<(string FilePath, DateTime Time)>? dependentTimeFiles = null)
        {
            await _buildUpToDateCheck.ActivateAsync();

            BroadcastChange(
                projectRuleSnapshot: projectSnapshot,
                sourceRuleSnapshot: sourceSnapshot,
                dependentTimeFiles: dependentTimeFiles == null
                    ? ImmutableDictionary.Create<string, DateTime>(StringComparers.Paths)
                    : dependentTimeFiles.ToImmutableDictionary(
                        item => item.FilePath,
                        item => item.Time,
                        StringComparers.Paths));

            // Flush the first false
            await AssertNotUpToDateAsync(
                "The up-to-date check has not yet run for this project. Not up-to-date.",
                "FirstRun");
        }

        private void BroadcastChange(
            Dictionary<string, IProjectRuleSnapshotModel>? projectRuleSnapshot = null,
            Dictionary<string, IProjectRuleSnapshotModel>? sourceRuleSnapshot = null,
            bool disableFastUpToDateCheck = false,
            IImmutableDictionary<string, DateTime>? dependentTimeFiles = null,
            string outDir = _outputPath)
        {
            projectRuleSnapshot ??= new Dictionary<string, IProjectRuleSnapshotModel>();

            if (!projectRuleSnapshot.ContainsKey(ConfigurationGeneral.SchemaName))
            {
                projectRuleSnapshot[ConfigurationGeneral.SchemaName] = new IProjectRuleSnapshotModel
                {
                    Properties = ImmutableStringDictionary<string>.EmptyOrdinal
                        .Add("MSBuildProjectFullPath", _msBuildProjectFullPath)
                        .Add("MSBuildProjectDirectory", _msBuildProjectDirectory)
                        .Add("MSBuildAllProjects", _msBuildAllProjects)
                        .Add("OutputPath", _outputPath)
                        .Add("OutDir", outDir)
                        .Add("DisableFastUpToDateCheck", disableFastUpToDateCheck.ToString())
                };
            }

            var value = IProjectVersionedValueFactory.Create(
                ValueTuple.Create(
                    CreateUpdate(projectRuleSnapshot),
                    CreateUpdate(sourceRuleSnapshot),
                    (IProjectSnapshot)IProjectSnapshot2Factory.Create(dependentTimeFiles),
                    IProjectItemSchemaFactory.Create(_itemTypes),
                    IProjectCatalogSnapshotFactory.CreateWithDefaultMapping(_itemTypes)),
                identity: ProjectDataSources.ConfiguredProjectVersion,
                version: _projectVersion);

            _buildUpToDateCheck.TestAccess.OnChanged(value);

            return;

            static IProjectSubscriptionUpdate CreateUpdate(Dictionary<string, IProjectRuleSnapshotModel>? snapshotBySchemaName)
            {
                var snapshots = ImmutableStringDictionary<IProjectRuleSnapshot>.EmptyOrdinal;
                var changes = ImmutableStringDictionary<IProjectChangeDescription>.EmptyOrdinal;

                if (snapshotBySchemaName != null)
                {
                    foreach ((string schemaName, IProjectRuleSnapshotModel model) in snapshotBySchemaName)
                    {
                        var change = new IProjectChangeDescriptionModel
                        {
                            After = model,
                            Difference = new IProjectChangeDiffModel { AnyChanges = true }
                        };

                        snapshots = snapshots.Add(schemaName, model.ToActualModel());
                        changes = changes.Add(schemaName, change.ToActualModel());
                    }
                }

                return IProjectSubscriptionUpdateFactory.Implement(snapshots, changes);
            }
        }

        private static IProjectRuleSnapshotModel SimpleItems(params string[] items)
        {
            return new IProjectRuleSnapshotModel
            {
                Items = items.Aggregate(
                    ImmutableStringDictionary<IImmutableDictionary<string, string>>.EmptyOrdinal,
                    (current, item) => current.Add(item, ImmutableStringDictionary<string>.EmptyOrdinal))
            };
        }

        private static IProjectRuleSnapshotModel ItemWithMetadata(string itemSpec, string metadataName, string metadataValue)
        {
            return new IProjectRuleSnapshotModel
            {
                Items = ImmutableStringDictionary<IImmutableDictionary<string, string>>.EmptyOrdinal.Add(
                    itemSpec,
                    ImmutableStringDictionary<string>.EmptyOrdinal.Add(metadataName, metadataValue))
            };
        }

        private static IProjectRuleSnapshotModel Union(params IProjectRuleSnapshotModel[] models)
        {
            var items = ImmutableDictionary<string, IImmutableDictionary<string, string>>.Empty;

            foreach (var model in models)
            {
                foreach ((string key, IImmutableDictionary<string, string> value) in model.Items)
                {
                    items = items.Add(key, value);
                }
            }

            return new IProjectRuleSnapshotModel { Items = items };
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IsUpToDateCheckEnabledAsync_DelegatesToProjectSystemOptions(bool isEnabled)
        {
            _isFastUpToDateCheckEnabled = isEnabled;

            Assert.Equal(
                isEnabled,
                await _buildUpToDateCheck.IsUpToDateCheckEnabledAsync(CancellationToken.None));
        }

        [Theory]
        [InlineData(BuildAction.Clean)]
        [InlineData(BuildAction.Compile)]
        [InlineData(BuildAction.Deploy)]
        [InlineData(BuildAction.Link)]
        [InlineData(BuildAction.Package)]
        [InlineData(BuildAction.Rebuild)]
        public async Task IsUpToDateAsync_False_NonBuildAction(BuildAction buildAction)
        {
            await SetupAsync();

            await AssertUpToDateAsync("No build outputs defined.");

            await AssertNotUpToDateAsync(buildAction: buildAction);
        }

        [Fact]
        public async Task IsUpToDateAsync_False_BuildTasksActive()
        {
            await SetupAsync();

            await AssertUpToDateAsync("No build outputs defined.");

            _isTaskQueueEmpty = false;

            await AssertNotUpToDateAsync(
                "Critical build tasks are running, not up to date.",
                "CriticalTasks");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_ProjectVersionIncreased()
        {
            await SetupAsync();

            await AssertUpToDateAsync("No build outputs defined.");

            _projectVersion++;

            await AssertNotUpToDateAsync(
                "Project information is older than current project version, not up to date.",
                "ProjectInfoOutOfDate");
        }

        [Fact]
        public async Task IsUpToDateAsync_True_ProjectVersionDecreased()
        {
            await SetupAsync();

            await AssertUpToDateAsync("No build outputs defined.");

            _projectVersion--;

            await AssertUpToDateAsync("No build outputs defined.");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_ItemsChanged()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1")
            };

            var sourceSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [Content.SchemaName] = SimpleItems("ItemPath1", "ItemPath2")
            };

            await SetupAsync(projectSnapshot, sourceSnapshot);

            var outputTime      = DateTime.UtcNow.AddMinutes(-3);
            var lastCheckTime   = DateTime.UtcNow.AddMinutes(-2);
            var itemChangedTime = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuiltOutputPath1", outputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangedTime);

            await AssertNotUpToDateAsync(
                new[]
                {
                    $"The set of project items was changed more recently ({itemChangedTime.ToLocalTime()}) than the earliest output 'C:\\Dev\\Solution\\Project\\BuiltOutputPath1' ({outputTime.ToLocalTime()}), not up to date.",
                    "    Content item added 'ItemPath1' (CopyType=CopyNever)",
                    "    Content item added 'ItemPath2' (CopyType=CopyNever)",
                },
                "Outputs");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_Disabled()
        {
            await SetupAsync();

            await AssertUpToDateAsync("No build outputs defined.");

            BroadcastChange(disableFastUpToDateCheck: true);

            await AssertNotUpToDateAsync(
                "The 'DisableFastUpToDateCheck' property is true, not up to date.",
                "Disabled");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_CopyAlwaysItemExists()
        {
            // TODO add a Content or None item as CopyAlways and verify (these are currently excluded by CollectInputs)

            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [CopyToOutputDirectoryItem.SchemaName] = new IProjectRuleSnapshotModel
                {
                    Items = ImmutableStringDictionary<IImmutableDictionary<string, string>>.EmptyOrdinal
                        .Add("ItemPath1", ImmutableStringDictionary<string>.EmptyOrdinal
                            .Add("TargetPath", "fileName")
                            .Add("CopyToOutputDirectory", "Always"))
                        .Add("ItemPath2", ImmutableStringDictionary<string>.EmptyOrdinal)
                }
            };

            await SetupAsync(projectSnapshot: projectSnapshot);

            await AssertNotUpToDateAsync(
                "Item 'C:\\Dev\\Solution\\Project\\ItemPath1' has CopyToOutputDirectory set to 'Always', not up to date.",
                "CopyAlwaysItemExists");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_OutputItemDoesNotExist()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1")
            };

            await SetupAsync(projectSnapshot: projectSnapshot);

            await AssertNotUpToDateAsync(
                "Output 'C:\\Dev\\Solution\\Project\\BuiltOutputPath1' does not exist, not up to date.",
                "Outputs");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_InputItemDoesNotExist()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1")
            };

            var sourceSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [Compile.SchemaName] = SimpleItems("ItemPath1")
            };

            await SetupAsync(projectSnapshot, sourceSnapshot);

            var itemChangedTime = DateTime.UtcNow.AddMinutes(-3);
            var outputTime      = DateTime.UtcNow.AddMinutes(-2);
            var lastCheckTime   = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuiltOutputPath1", outputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangedTime);

            await AssertNotUpToDateAsync(
                "Input 'C:\\Dev\\Solution\\Project\\ItemPath1' does not exist and is required, not up to date.",
                "Outputs");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_InputNewerThanBuiltOutput()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1")
            };

            var sourceSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [Compile.SchemaName] = SimpleItems("ItemPath1")
            };

            await SetupAsync(projectSnapshot, sourceSnapshot);

            var itemChangedTime = DateTime.UtcNow.AddMinutes(-4);
            var outputTime      = DateTime.UtcNow.AddMinutes(-3);
            var inputTime       = DateTime.UtcNow.AddMinutes(-2);
            var lastCheckTime   = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuiltOutputPath1", outputTime);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\ItemPath1", inputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangedTime);

            await AssertNotUpToDateAsync(
                $"Input 'C:\\Dev\\Solution\\Project\\ItemPath1' is newer ({inputTime.ToLocalTime()}) than earliest output 'C:\\Dev\\Solution\\Project\\BuiltOutputPath1' ({outputTime.ToLocalTime()}), not up to date.",
                "Outputs");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_InputChangedBetweenLastCheckAndEarliestOutput()
        {
            // This test covers a race condition described in https://github.com/dotnet/project-system/issues/4014
            //
            // t0 Modify input file
            // t1 Check up to date (false) so start a build
            // t2 Modify input file
            // t3 Produce first (earliest) output DLL (from t0 input)
            // t4 Check incorrectly claims everything up to date, as t3 > t2

            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1")
            };

            var sourceSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [Compile.SchemaName] = SimpleItems("ItemPath1")
            };

            var t0 = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile(_msBuildProjectFullPath, t0);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\ItemPath1", t0);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuiltOutputPath1", t0.AddMinutes(-1));

            // Run test (t1)
            await SetupAsync(projectSnapshot, sourceSnapshot);

            await AssertNotUpToDateAsync(
                $"Input '{_msBuildProjectFullPath}' is newer ({t0.ToLocalTime()}) than earliest output 'C:\\Dev\\Solution\\Project\\BuiltOutputPath1' ({t0.AddMinutes(-1).ToLocalTime()}), not up to date.",
                "Outputs");

            await Task.Delay(50);

            // Modify input while build in progress (t2)
            var t2 = DateTime.UtcNow;
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\ItemPath1", t2);

            await Task.Delay(50);

            // Update write time of output (t3)
            var t3 = DateTime.UtcNow;
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuiltOutputPath1", t3);

            await Task.Delay(50);

            // Run check again (t4)
            await AssertNotUpToDateAsync(
                $"Input 'C:\\Dev\\Solution\\Project\\ItemPath1' ({t2.ToLocalTime()}) has been modified since the last up-to-date check ({_buildUpToDateCheck.TestAccess.State.LastCheckedAtUtc.ToLocalTime()}), not up to date.",
                "Outputs");
        }

        [Fact]
        public async Task InitialItemDataDoesNotUpdateLastItemsChangedAtUtc()
        {
            // This test covers a false negative described in https://github.com/dotnet/project-system/issues/5386
            // where the initial snapshot of items sets LastItemsChangedAtUtc, so if a project is up to date when
            // it is loaded, then the items are considered changed *after* the last build, but MSBuild's up-to-date
            // check will determine the project doesn't require a rebuild and so the output timestamps won't update.
            // This previously left the project in a state where it would be considered out of date endlessly.

            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1")
            };

            var sourceSnapshot1 = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [Compile.SchemaName] = SimpleItems("ItemPath1")
            };

            var sourceSnapshot2 = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [Compile.SchemaName] = SimpleItems("ItemPath1", "ItemPath2")
            };

            await _buildUpToDateCheck.ActivateAsync();

            Assert.Equal(DateTime.MinValue, _buildUpToDateCheck.TestAccess.State.LastItemsChangedAtUtc);

            // Initial change does NOT set LastItemsChangedAtUtc
            BroadcastChange(projectSnapshot, sourceSnapshot1);

            Assert.Equal(DateTime.MinValue, _buildUpToDateCheck.TestAccess.State.LastItemsChangedAtUtc);

            // Broadcasting an update with no change to items does NOT set LastItemsChangedAtUtc
            BroadcastChange();

            Assert.Equal(DateTime.MinValue, _buildUpToDateCheck.TestAccess.State.LastItemsChangedAtUtc);

            // Broadcasting changed items DOES set LastItemsChangedAtUtc
            BroadcastChange(projectSnapshot, sourceSnapshot2);

            Assert.NotEqual(DateTime.MinValue, _buildUpToDateCheck.TestAccess.State.LastItemsChangedAtUtc);
        }

        [Fact]
        public async Task IsUpToDateAsync_False_CompileItemNewerThanCustomOutput()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckOutput.SchemaName] = SimpleItems("CustomOutputPath1")
            };

            var sourceSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [Compile.SchemaName] = SimpleItems("ItemPath1")
            };

            await SetupAsync(projectSnapshot, sourceSnapshot);

            var itemChangeTime  = DateTime.UtcNow.AddMinutes(-4);
            var outputTime      = DateTime.UtcNow.AddMinutes(-3);
            var compileItemTime = DateTime.UtcNow.AddMinutes(-2);
            var lastCheckTime   = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\CustomOutputPath1", outputTime);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\ItemPath1", compileItemTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                $"Input 'C:\\Dev\\Solution\\Project\\ItemPath1' is newer ({compileItemTime.ToLocalTime()}) than earliest output 'C:\\Dev\\Solution\\Project\\CustomOutputPath1' ({outputTime.ToLocalTime()}), not up to date.",
                "Outputs");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_CopyReferenceInputNewerThanMarkerOutput()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [CopyUpToDateMarker.SchemaName] = SimpleItems("Marker"),
                [ResolvedCompilationReference.SchemaName] = new IProjectRuleSnapshotModel
                {
                    Items = ImmutableStringDictionary<IImmutableDictionary<string, string>>.EmptyOrdinal
                        .Add("Reference1", ImmutableStringDictionary<string>.EmptyOrdinal
                            .Add("CopyUpToDateMarker", "Reference1MarkerPath")
                            .Add("ResolvedPath", "Reference1ResolvedPath")
                            .Add("OriginalPath", "Reference1OriginalPath"))
                }
            };

            await SetupAsync(projectSnapshot: projectSnapshot);

            var outputTime   = DateTime.UtcNow.AddMinutes(-4);
            var resolvedTime = DateTime.UtcNow.AddMinutes(-3);
            var markerTime   = DateTime.UtcNow.AddMinutes(-2);
            var originalTime = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Marker", outputTime);
            _fileSystem.AddFile("Reference1ResolvedPath", resolvedTime);
            _fileSystem.AddFile("Reference1MarkerPath", markerTime);
            _fileSystem.AddFile("Reference1OriginalPath", originalTime);

            await AssertNotUpToDateAsync(
                new[]
                {
                    "No build outputs defined.",
                    $"Latest write timestamp on input marker is {originalTime.ToLocalTime()} on 'Reference1OriginalPath'.",
                    $"Write timestamp on output marker is {outputTime.ToLocalTime()} on 'C:\\Dev\\Solution\\Project\\Marker'.",
                    "Input marker is newer than output marker, not up to date."
                },
                "Marker");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_AnalyzerReferenceNewerThanEarliestOutput()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1"),
                [ResolvedAnalyzerReference.SchemaName] = ItemWithMetadata("Analyzer1", "ResolvedPath", "Analyzer1ResolvedPath")
            };

            await SetupAsync(projectSnapshot);

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-4);
            var outputTime     = DateTime.UtcNow.AddMinutes(-3);
            var inputTime      = DateTime.UtcNow.AddMinutes(-2);
            var lastCheckTime  = DateTime.UtcNow.AddMinutes(-1);

            var outputItem = "C:\\Dev\\Solution\\Project\\BuiltOutputPath1";
            var analyzerItem = "C:\\Dev\\Solution\\Project\\Analyzer1ResolvedPath";

            _fileSystem.AddFile(outputItem, outputTime);
            _fileSystem.AddFile(analyzerItem, inputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                $"Input '{analyzerItem}' is newer ({inputTime.ToLocalTime()}) than earliest output '{outputItem}' ({outputTime.ToLocalTime()}), not up to date.",
                "Outputs");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_CompilationReferenceNewerThanEarliestOutput()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1"),
                [ResolvedCompilationReference.SchemaName] = new IProjectRuleSnapshotModel
                {
                    Items = ImmutableStringDictionary<IImmutableDictionary<string, string>>.EmptyOrdinal
                        .Add("Reference1", ImmutableStringDictionary<string>.EmptyOrdinal
                            .Add("CopyUpToDateMarker", "Reference1MarkerPath")
                            .Add("ResolvedPath", "Reference1ResolvedPath")
                            .Add("OriginalPath", "Reference1OriginalPath"))
                }
            };

            await SetupAsync(projectSnapshot);

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-4);
            var lastCheckTime  = DateTime.UtcNow.AddMinutes(-3);
            var outputTime     = DateTime.UtcNow.AddMinutes(-2);
            var inputTime      = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuiltOutputPath1", outputTime);
            _fileSystem.AddFile("Reference1ResolvedPath", inputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                $"Input 'Reference1ResolvedPath' is newer ({inputTime.ToLocalTime()}) than earliest output 'C:\\Dev\\Solution\\Project\\BuiltOutputPath1' ({outputTime.ToLocalTime()}), not up to date.",
                "Outputs");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_UpToDateCheckInputNewerThanEarliestOutput()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1"),
                [UpToDateCheckInput.SchemaName] = SimpleItems("Item1", "Item2")
            };

            await SetupAsync(projectSnapshot);

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-4);
            var lastCheckTime  = DateTime.UtcNow.AddMinutes(-3);
            var outputTime     = DateTime.UtcNow.AddMinutes(-2);
            var inputTime      = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuiltOutputPath1", outputTime);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Item1", inputTime);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Item2", outputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                $"Input 'C:\\Dev\\Solution\\Project\\Item1' is newer ({inputTime.ToLocalTime()}) than earliest output 'C:\\Dev\\Solution\\Project\\BuiltOutputPath1' ({outputTime.ToLocalTime()}), not up to date.",
                "Outputs");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_Sets_InputNewerThanOutput()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuildDefault"),
                [UpToDateCheckInput.SchemaName] = ItemWithMetadata("Input1", "Set", "Set1"),
                [UpToDateCheckOutput.SchemaName] = ItemWithMetadata("Output1", "Set", "Set1")
            };

            await SetupAsync(projectSnapshot);

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-4);
            var lastCheckTime  = DateTime.UtcNow.AddMinutes(-3);
            var outputTime     = DateTime.UtcNow.AddMinutes(-2);
            var inputTime      = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Input1", inputTime);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Output1", outputTime);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuildDefault", outputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                new[]
                {
                    $"No inputs are newer than earliest output 'C:\\Dev\\Solution\\Project\\BuildDefault' ({outputTime.ToLocalTime()}).",
                    $"Input 'C:\\Dev\\Solution\\Project\\Input1' is newer ({inputTime.ToLocalTime()}) than earliest output 'C:\\Dev\\Solution\\Project\\Output1' ({outputTime.ToLocalTime()}), not up to date."
                },
                "Outputs");
        }

        [Fact]
        public async Task IsUpToDateAsync_True_Sets_InputOlderThanOutput_MultipleSets()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuildDefault"),
                [UpToDateCheckInput.SchemaName] = Union(ItemWithMetadata("Input1", "Set", "Set1"), ItemWithMetadata("Input2", "Set", "Set2")),
                [UpToDateCheckOutput.SchemaName] = Union(ItemWithMetadata("Output1", "Set", "Set1"), ItemWithMetadata("Output2", "Set", "Set2"))
            };

            await SetupAsync(projectSnapshot);

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-6);
            var inputTime1     = DateTime.UtcNow.AddMinutes(-5);
            var outputTime1    = DateTime.UtcNow.AddMinutes(-4);
            var inputTime2     = DateTime.UtcNow.AddMinutes(-3);
            var outputTime2    = DateTime.UtcNow.AddMinutes(-2);
            var lastCheckTime  = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Input1", inputTime1);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Output1", outputTime1);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Input2", inputTime2);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Output2", outputTime2);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuildDefault", outputTime1);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertUpToDateAsync(
                $"No inputs are newer than earliest output 'C:\\Dev\\Solution\\Project\\BuildDefault' ({outputTime1.ToLocalTime()}).",
                $"In set 'Set1', no inputs are newer than earliest output 'C:\\Dev\\Solution\\Project\\Output1' ({outputTime1.ToLocalTime()}).",
                $"In set 'Set2', no inputs are newer than earliest output 'C:\\Dev\\Solution\\Project\\Output2' ({outputTime2.ToLocalTime()}).");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_Sets_InputNewerThanOutput_MultipleSets()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuildDefault"),
                [UpToDateCheckInput.SchemaName] = Union(ItemWithMetadata("Input1", "Set", "Set1"), ItemWithMetadata("Input2", "Set", "Set2")),
                [UpToDateCheckOutput.SchemaName] = Union(ItemWithMetadata("Output1", "Set", "Set1"), ItemWithMetadata("Output2", "Set", "Set2"))
            };

            await SetupAsync(projectSnapshot);

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-6);
            var inputTime1     = DateTime.UtcNow.AddMinutes(-5);
            var outputTime1    = DateTime.UtcNow.AddMinutes(-4);
            var outputTime2    = DateTime.UtcNow.AddMinutes(-3);
            var inputTime2     = DateTime.UtcNow.AddMinutes(-2);
            var lastCheckTime  = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Input1", inputTime1);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Output1", outputTime1);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Input2", inputTime2);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Output2", outputTime2);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuildDefault", outputTime1);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                new[]
                {
                    $"No inputs are newer than earliest output 'C:\\Dev\\Solution\\Project\\BuildDefault' ({outputTime1.ToLocalTime()}).",
                    $"In set 'Set1', no inputs are newer than earliest output 'C:\\Dev\\Solution\\Project\\Output1' ({outputTime1.ToLocalTime()}).",
                    $"Input 'C:\\Dev\\Solution\\Project\\Input2' is newer ({inputTime2.ToLocalTime()}) than earliest output 'C:\\Dev\\Solution\\Project\\Output2' ({outputTime2.ToLocalTime()}), not up to date."
                },
                "Outputs");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_Sets_InputNewerThanOutput_ItemInMultipleSets()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuildDefault"),
                [UpToDateCheckInput.SchemaName] = ItemWithMetadata("Input", "Set", "Set1;Set2"),
                [UpToDateCheckOutput.SchemaName] = Union(ItemWithMetadata("Output1", "Set", "Set1"), ItemWithMetadata("Output2", "Set", "Set2"))
            };

            await SetupAsync(projectSnapshot);

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-5);
            var outputTime2    = DateTime.UtcNow.AddMinutes(-4);
            var inputTime      = DateTime.UtcNow.AddMinutes(-3);
            var outputTime1    = DateTime.UtcNow.AddMinutes(-2);
            var lastCheckTime  = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Input", inputTime);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Output1", outputTime1);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Output2", outputTime2);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuildDefault", outputTime1);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                new[]
                {
                    $"No inputs are newer than earliest output 'C:\\Dev\\Solution\\Project\\BuildDefault' ({outputTime1.ToLocalTime()}).",
                    $"In set 'Set1', no inputs are newer than earliest output 'C:\\Dev\\Solution\\Project\\Output1' ({outputTime1.ToLocalTime()}).",
                    $"Input 'C:\\Dev\\Solution\\Project\\Input' is newer ({inputTime.ToLocalTime()}) than earliest output 'C:\\Dev\\Solution\\Project\\Output2' ({outputTime2.ToLocalTime()}), not up to date."
                },
                "Outputs");
        }

        [Fact]
        public async Task IsUpToDateAsync_True_Sets_InputOnly()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuildDefault"),
                [UpToDateCheckInput.SchemaName] = ItemWithMetadata("Input1", "Set", "Set1"),
            };

            await SetupAsync(projectSnapshot);

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-4);
            var lastCheckTime  = DateTime.UtcNow.AddMinutes(-3);
            var buildTime      = DateTime.UtcNow.AddMinutes(-2);
            var inputTime      = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Input1", inputTime);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuildDefault", buildTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertUpToDateAsync(
                $"No inputs are newer than earliest output 'C:\\Dev\\Solution\\Project\\BuildDefault' ({buildTime.ToLocalTime()}).",
                "No build outputs defined in set 'Set1'.");
        }

        [Fact]
        public async Task IsUpToDateAsync_True_Sets_OutputOnly()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuildDefault"),
                [UpToDateCheckOutput.SchemaName] = ItemWithMetadata("Output1", "Set", "Set1")
            };

            await SetupAsync(projectSnapshot);

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-3);
            var lastCheckTime  = DateTime.UtcNow.AddMinutes(-2);
            var outputTime     = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\Output1", outputTime);
            _fileSystem.AddFile("C:\\Dev\\Solution\\Project\\BuildDefault", outputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertUpToDateAsync(
                $"No inputs are newer than earliest output 'C:\\Dev\\Solution\\Project\\BuildDefault' ({outputTime.ToLocalTime()}).",
                "No inputs defined in set 'Set1'.");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_CopiedOutputFileSourceIsNewerThanDestination()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = ItemWithMetadata("CopiedOutputDestination", "Original", "CopiedOutputSource")
            };

            await SetupAsync(projectSnapshot);

            var destinationPath = @"C:\Dev\Solution\Project\CopiedOutputDestination";
            var sourcePath = @"C:\Dev\Solution\Project\CopiedOutputSource";

            var itemChangeTime  = DateTime.UtcNow.AddMinutes(-4);
            var lastCheckTime   = DateTime.UtcNow.AddMinutes(-3);
            var destinationTime = DateTime.UtcNow.AddMinutes(-2);
            var sourceTime      = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile(destinationPath, destinationTime);
            _fileSystem.AddFile(sourcePath, sourceTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                new[]
                {
                    "No build outputs defined.",
                    $"Checking copied output ({UpToDateCheckBuilt.SchemaName} with {UpToDateCheckBuilt.OriginalProperty} property) file '{sourcePath}':",
                    $"    Source {sourceTime.ToLocalTime()}: '{sourcePath}'.",
                    $"    Destination {destinationTime.ToLocalTime()}: '{destinationPath}'.",
                    "Source is newer than build output destination, not up to date."
                },
                "CopyOutput");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_CopyToOutDirSourceIsNewerThanDestination()
        {
            const string outDirSnapshot = "newOutDir";

            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [CopyToOutputDirectoryItem.SchemaName] = new IProjectRuleSnapshotModel
                {
                    Items = ImmutableStringDictionary<IImmutableDictionary<string, string>>.EmptyOrdinal
                        .Add("C:\\Dev\\Solution\\Project\\Item1", ImmutableStringDictionary<string>.EmptyOrdinal
                            .Add("TargetPath", "Item1")
                            .Add("CopyToOutputDirectory", "PreserveNewest"))
                }
            };

            await _buildUpToDateCheck.ActivateAsync();

            BroadcastChange(outDir: outDirSnapshot, projectRuleSnapshot: projectSnapshot);

            var destinationOutDir = @"NewProjectDirectory\newOutDir\Item1";
            var sourcePath = @"C:\Dev\Solution\Project\Item1";

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-4);
            var lastCheckTime = DateTime.UtcNow.AddMinutes(-3);
            var destinationTime = DateTime.UtcNow.AddMinutes(-2);
            var sourceTime = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile(destinationOutDir, destinationTime);
            _fileSystem.AddFile(sourcePath, sourceTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                new[]
                {
                    "No build outputs defined.",
                    $"Checking PreserveNewest file '{sourcePath}':",
                    $"    Source {sourceTime.ToLocalTime()}: '{sourcePath}'.",
                    $"    Destination {destinationTime.ToLocalTime()}: '{destinationOutDir}'.",
                    "PreserveNewest source is newer than destination, not up to date."
                },
                "CopyToOutputDirectory");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_CopiedOutputFileSourceDoesNotExist()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = ItemWithMetadata("CopiedOutputDestination", "Original", "CopiedOutputSource")
            };

            await SetupAsync(projectSnapshot);

            var destinationPath = @"C:\Dev\Solution\Project\CopiedOutputDestination";
            var sourcePath = @"C:\Dev\Solution\Project\CopiedOutputSource";

            _fileSystem.AddFile(destinationPath);

            await AssertNotUpToDateAsync(
                new[]
                {
                    "No build outputs defined.",
                    $"Checking copied output ({UpToDateCheckBuilt.SchemaName} with {UpToDateCheckBuilt.OriginalProperty} property) file '{sourcePath}':",
                    $"Source '{sourcePath}' does not exist, not up to date."
                },
                "CopyOutput");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_CopiedOutputFileDestinationDoesNotExist()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = ItemWithMetadata("CopiedOutputDestination", "Original", "CopiedOutputSource")
            };

            await SetupAsync(projectSnapshot);

            var destinationPath = @"C:\Dev\Solution\Project\CopiedOutputDestination";
            var sourcePath = @"C:\Dev\Solution\Project\CopiedOutputSource";

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-4);
            var lastCheckTime  = DateTime.UtcNow.AddMinutes(-3);
            var sourceTime     = DateTime.UtcNow.AddMinutes(-2);

            _fileSystem.AddFile(sourcePath, sourceTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                new[]
                {
                    "No build outputs defined.",
                    $"Checking copied output ({UpToDateCheckBuilt.SchemaName} with {UpToDateCheckBuilt.OriginalProperty} property) file '{sourcePath}':",
                    $"    Source {sourceTime.ToLocalTime()}: '{sourcePath}'.",
                    $"Destination '{destinationPath}' does not exist, not up to date."
                },
                "CopyOutput");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_CopyToOutputDirectorySourceIsNewerThanDestination()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [CopyToOutputDirectoryItem.SchemaName] = new IProjectRuleSnapshotModel
                {
                    Items = ImmutableStringDictionary<IImmutableDictionary<string, string>>.EmptyOrdinal
                        .Add("C:\\Dev\\Solution\\Project\\Item1", ImmutableStringDictionary<string>.EmptyOrdinal
                            .Add("TargetPath", "Item1")
                            .Add("CopyToOutputDirectory", "PreserveNewest"))
                }
            };

            await SetupAsync(projectSnapshot: projectSnapshot);

            var destinationPath = @"NewProjectDirectory\NewOutputPath\Item1";
            var sourcePath = @"C:\Dev\Solution\Project\Item1";

            var itemChangeTime  = DateTime.UtcNow.AddMinutes(-4);
            var lastCheckTime   = DateTime.UtcNow.AddMinutes(-3);
            var destinationTime = DateTime.UtcNow.AddMinutes(-2);
            var sourceTime      = DateTime.UtcNow.AddMinutes(-1);

            _fileSystem.AddFile(destinationPath, destinationTime);
            _fileSystem.AddFile(sourcePath, sourceTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                new[]
                {
                    "No build outputs defined.",
                    $"Checking PreserveNewest file '{sourcePath}':",
                    $"    Source {sourceTime.ToLocalTime()}: '{sourcePath}'.",
                    $"    Destination {destinationTime.ToLocalTime()}: '{destinationPath}'.",
                    "PreserveNewest source is newer than destination, not up to date."
                },
                "CopyToOutputDirectory");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_CopyToOutputDirectorySourceDoesNotExist()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [CopyToOutputDirectoryItem.SchemaName] = new IProjectRuleSnapshotModel
                {
                    Items = ImmutableStringDictionary<IImmutableDictionary<string, string>>.EmptyOrdinal
                        .Add("C:\\Dev\\Solution\\Project\\Item1", ImmutableStringDictionary<string>.EmptyOrdinal
                            .Add("TargetPath", "Item1")
                            .Add("CopyToOutputDirectory", "PreserveNewest"))
                }
            };

            await SetupAsync(projectSnapshot: projectSnapshot);

            var destinationPath = @"NewProjectDirectory\NewOutputPath\Item1";
            var sourcePath = @"C:\Dev\Solution\Project\Item1";

            var itemChangeTime  = DateTime.UtcNow.AddMinutes(-4);
            var lastCheckTime   = DateTime.UtcNow.AddMinutes(-3);
            var destinationTime = DateTime.UtcNow.AddMinutes(-2);

            _fileSystem.AddFile(destinationPath, destinationTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                new[]
                {
                    "No build outputs defined.",
                    $"Checking PreserveNewest file '{sourcePath}':",
                    $"Source '{sourcePath}' does not exist, not up to date."
                },
                "CopyToOutputDirectory");
        }

        [Fact]
        public async Task IsUpToDateAsync_False_CopyToOutputDirectoryDestinationDoesNotExist()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [CopyToOutputDirectoryItem.SchemaName] = new IProjectRuleSnapshotModel
                {
                    Items = ImmutableStringDictionary<IImmutableDictionary<string, string>>.EmptyOrdinal
                        .Add("C:\\Dev\\Solution\\Project\\Item1", ImmutableStringDictionary<string>.EmptyOrdinal
                            .Add("TargetPath", "Item1")
                            .Add("CopyToOutputDirectory", "PreserveNewest"))
                }
            };

            await SetupAsync(projectSnapshot: projectSnapshot);

            var destinationPath = @"NewProjectDirectory\NewOutputPath\Item1";
            var sourcePath = @"C:\Dev\Solution\Project\Item1";
            
            var itemChangeTime = DateTime.UtcNow.AddMinutes(-4);
            var lastCheckTime  = DateTime.UtcNow.AddMinutes(-3);
            var sourceTime     = DateTime.UtcNow.AddMinutes(-2);

            _fileSystem.AddFile(sourcePath, sourceTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);

            await AssertNotUpToDateAsync(
                new[]
                {
                    "No build outputs defined.",
                    $"Checking PreserveNewest file '{sourcePath}':",
                    $"    Source {sourceTime.ToLocalTime()}: '{sourcePath}'.",
                    $"Destination '{destinationPath}' does not exist, not up to date."
                },
                "CopyToOutputDirectory");
        }

        [Fact]
        public async Task IsUpToDateAsync_True_InitialItemDataDoesNotUpdateLastAdditionalDependentFileTimesChangedAtUtc()
        {
            await _buildUpToDateCheck.ActivateAsync();

            Assert.Equal(DateTime.MinValue, _buildUpToDateCheck.TestAccess.State.LastAdditionalDependentFileTimesChangedAtUtc);

            var dependentTime = DateTime.UtcNow.AddMinutes(-1);
            var dependentPath = @"C:\Dev\Solution\Project\Dependent";
            _fileSystem.AddFile(dependentPath, dependentTime);
            var dependentTimeFiles = ImmutableDictionary.Create<string, DateTime>(StringComparers.Paths).Add(dependentPath, dependentTime);

            // Initial change does NOT set LastAdditionalDependentFileTimesChangedAtUtc
            BroadcastChange(dependentTimeFiles: dependentTimeFiles);

            Assert.Equal(DateTime.MinValue, _buildUpToDateCheck.TestAccess.State.LastAdditionalDependentFileTimesChangedAtUtc);

            // Broadcasting an update with same Additional Dependent Files does NOT set LastAdditionalDependentFileTimesChangedAtUtc
            BroadcastChange(dependentTimeFiles: dependentTimeFiles);

            Assert.Equal(DateTime.MinValue, _buildUpToDateCheck.TestAccess.State.LastAdditionalDependentFileTimesChangedAtUtc);

            // Broadcasting removing Additional Dependent Files DOES set LastAdditionalDependentFileTimesChangedAtUtc
            BroadcastChange();

            Assert.NotEqual(DateTime.MinValue, _buildUpToDateCheck.TestAccess.State.LastAdditionalDependentFileTimesChangedAtUtc);
        }

        [Fact(Skip = "TODO fix #6227")]
        public async Task IsUpToDateAsync_False_AdditionalDependentFileTimeSourceRemoved()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1")
            };

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-6);
            var dependentTime = DateTime.UtcNow.AddMinutes(-5);
            var lastCheckTime = DateTime.UtcNow.AddMinutes(-4);
            var lastAdditionalDependentFileTimesChanged = DateTime.UtcNow.AddMinutes(-3);
            var outputTime = DateTime.UtcNow.AddMinutes(-2);

            var dependentPath = @"C:\Dev\Solution\Project\Dependent";
            var outputPath = @"C:\Dev\Solution\Project\BuiltOutputPath1";

            await SetupAsync(
                projectSnapshot: projectSnapshot,
                dependentTimeFiles: new[] { (dependentPath, dependentTime) });

            _fileSystem.AddFile(dependentPath, dependentTime);
            _fileSystem.AddFile(outputPath, outputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastAdditionalDependentFileTimesChangedAtUtc(lastAdditionalDependentFileTimesChanged);

            await AssertUpToDateAsync(
                $"No inputs are newer than earliest output '{outputPath}' ({outputTime.ToLocalTime()}).");

            // Remove dependentPath
            await SetupAsync(projectSnapshot: projectSnapshot);

            lastAdditionalDependentFileTimesChanged = DateTime.UtcNow.AddMinutes(-1);

            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);
            _buildUpToDateCheck.TestAccess.SetLastAdditionalDependentFileTimesChangedAtUtc(lastAdditionalDependentFileTimesChanged);

            await AssertNotUpToDateAsync(
                $"The set of AdditionalDependentFileTimes was changed more recently ({lastAdditionalDependentFileTimesChanged.ToLocalTime()}) " +
                $"than the earliest output 'C:\\Dev\\Solution\\Project\\BuiltOutputPath1' ({outputTime.ToLocalTime()}), not up to date.",
                "Outputs");
        }

        [Fact(Skip = "TODO fix #6227")]
        public async Task IsUpToDateAsync_False_AdditionalDependentFileTimeSourceAdded()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1")
            };

            var itemChangeTime = DateTime.UtcNow.AddMinutes(-5);
            var lastCheckTime = DateTime.UtcNow.AddMinutes(-4);
            var outputTime = DateTime.UtcNow.AddMinutes(-3);
            var dependentTime = DateTime.UtcNow.AddMinutes(-2);
            var lastAdditionalDependentFileTimesChanged = DateTime.UtcNow.AddMinutes(-1);

            var dependentPath = @"C:\Dev\Solution\Project\Dependent";
            var outputPath = @"C:\Dev\Solution\Project\BuiltOutputPath1";

            await SetupAsync(
                projectSnapshot: projectSnapshot,
                dependentTimeFiles: new[] { (dependentPath, dependentTime) });

            _fileSystem.AddFile(dependentPath, dependentTime);
            _fileSystem.AddFile(outputPath, outputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);
            _buildUpToDateCheck.TestAccess.SetLastAdditionalDependentFileTimesChangedAtUtc(lastAdditionalDependentFileTimesChanged);

            await AssertNotUpToDateAsync(
                $"The set of AdditionalDependentFileTimes was changed more recently ({lastAdditionalDependentFileTimesChanged.ToLocalTime()}) " +
                $"than the earliest output 'C:\\Dev\\Solution\\Project\\BuiltOutputPath1' ({outputTime.ToLocalTime()}), not up to date.",
                "Outputs");
        }

        [Fact(Skip = "TODO fix #6227")]
        public async Task IsUpToDateAsync_False_AdditionalDependentFileTimeSourceIsNewerThanOutput()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1")
            };

            var lastAdditionalDependentFileTimesChanged = DateTime.UtcNow.AddMinutes(-4);
            var itemChangeTime = DateTime.UtcNow.AddMinutes(-4);
            var outputTime = DateTime.UtcNow.AddMinutes(-3);
            var dependentTime = DateTime.UtcNow.AddMinutes(-2);
            // NOTE This delay is neeed to bypass lastAdditionalDependentFileTimesChangedAtUtc
            var lastCheckTime = DateTime.UtcNow.AddMinutes(-1);

            var dependentPath = @"C:\Dev\Solution\Project\Dependent";
            var outputPath = @"C:\Dev\Solution\Project\BuiltOutputPath1";

            await SetupAsync(
                projectSnapshot: projectSnapshot,
                dependentTimeFiles: new[] { (dependentPath, dependentTime) });

            _fileSystem.AddFile(dependentPath, dependentTime);
            _fileSystem.AddFile(outputPath, outputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);
            _buildUpToDateCheck.TestAccess.SetLastAdditionalDependentFileTimesChangedAtUtc(lastAdditionalDependentFileTimesChanged);

            await AssertNotUpToDateAsync(
                $"Input '{dependentPath}' is newer ({dependentTime.ToLocalTime()}) than earliest output '{outputPath}' ({outputTime.ToLocalTime()}), not up to date.",
                telemetryReason: "Outputs");
        }

        [Fact]
        public async Task IsUpToDateAsync_True_AdditionalDependentFileTimeSourceIsOlderThanOutput()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("Output")
            };

            var lastAdditionalDependentFileTimesChanged = DateTime.UtcNow.AddMinutes(-5);
            var itemChangeTime = DateTime.UtcNow.AddMinutes(-4);
            var dependentTime = DateTime.UtcNow.AddMinutes(-3);
            var outputTime = DateTime.UtcNow.AddMinutes(-2);
            var lastCheckTime = DateTime.UtcNow.AddMinutes(-1);

            var dependentPath = @"C:\Dev\Solution\Project\Dependent";
            var outputPath = @"C:\Dev\Solution\Project\Output";

            await SetupAsync(
                projectSnapshot: projectSnapshot,
                dependentTimeFiles: new[] { (dependentPath, dependentTime) });

            _fileSystem.AddFile(dependentPath, dependentTime);
            _fileSystem.AddFile(outputPath, outputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);
            _buildUpToDateCheck.TestAccess.SetLastAdditionalDependentFileTimesChangedAtUtc(lastAdditionalDependentFileTimesChanged);

            await AssertUpToDateAsync(
                $"No inputs are newer than earliest output '{outputPath}' ({outputTime.ToLocalTime()}).");
        }

        [Fact]
        public async Task IsUpToDateAsync_True_AdditionalDependentFileDoesNotExist()
        {
            var projectSnapshot = new Dictionary<string, IProjectRuleSnapshotModel>
            {
                [UpToDateCheckBuilt.SchemaName] = SimpleItems("BuiltOutputPath1")
            };

            var lastAdditionalDependentFileTimesChanged = DateTime.UtcNow.AddMinutes(-5);
            var itemChangeTime = DateTime.UtcNow.AddMinutes(-4);
            var dependentTime = DateTime.UtcNow.AddMinutes(-3);
            var outputTime = DateTime.UtcNow.AddMinutes(-2);
            var lastCheckTime = DateTime.UtcNow.AddMinutes(-1);

            var dependentPath = @"C:\Dev\Solution\Project\Dependent";
            var outputPath = @"C:\Dev\Solution\Project\BuiltOutputPath1";

            await SetupAsync(
                projectSnapshot: projectSnapshot,
                dependentTimeFiles: new[] { (dependentPath, dependentTime) });

            // NOTE dependentPath is not present on the file system
            _fileSystem.AddFile(outputPath, outputTime);
            _buildUpToDateCheck.TestAccess.SetLastCheckedAtUtc(lastCheckTime);
            _buildUpToDateCheck.TestAccess.SetLastItemsChangedAtUtc(itemChangeTime);
            _buildUpToDateCheck.TestAccess.SetLastAdditionalDependentFileTimesChangedAtUtc(lastAdditionalDependentFileTimesChanged);

            await AssertUpToDateAsync(
                $"No inputs are newer than earliest output '{outputPath}' ({outputTime.ToLocalTime()}).");
        }

        #region Test helpers

        private Task AssertNotUpToDateAsync(string? logMessage = null, string? telemetryReason = null, BuildAction buildAction = BuildAction.Build)
        {
            return AssertNotUpToDateAsync(logMessage == null ? null : new[] { logMessage }, telemetryReason, buildAction);
        }

        private async Task AssertNotUpToDateAsync(IReadOnlyList<string>? logMessages, string? telemetryReason = null, BuildAction buildAction = BuildAction.Build)
        {
            var writer = new AssertWriter(_output);

            if (logMessages != null)
            {
                foreach (var logMessage in logMessages)
                {
                    writer.Add(logMessage);
                }
            }

            Assert.False(await _buildUpToDateCheck.IsUpToDateAsync(buildAction, writer));

            if (telemetryReason != null)
                AssertTelemetryFailureEvent(telemetryReason);
            else
                Assert.Empty(_telemetryEvents);

            writer.Assert();
        }

        private async Task AssertUpToDateAsync(params string[] logMessages)
        {
            var writer = new AssertWriter(_output);

            foreach (var logMessage in logMessages)
            {
                writer.Add(logMessage);
            }

            writer.Add("Project is up to date.");

            Assert.True(await _buildUpToDateCheck.IsUpToDateAsync(BuildAction.Build, writer));
            AssertTelemetrySuccessEvent();
            writer.Assert();
        }

        private void AssertTelemetryFailureEvent(string reason)
        {
            var telemetryEvent = Assert.Single(_telemetryEvents);

            Assert.Equal(TelemetryEventName.UpToDateCheckFail, telemetryEvent.EventName);
            Assert.NotNull(telemetryEvent.Properties);
            Assert.Equal(3, telemetryEvent.Properties!.Count);

            var reasonProp = Assert.Single(telemetryEvent.Properties.Where(p => p.propertyName == TelemetryPropertyName.UpToDateCheckFailReason));
            Assert.Equal(reason, reasonProp.propertyValue);

            var durationProp = Assert.Single(telemetryEvent.Properties.Where(p => p.propertyName == TelemetryPropertyName.UpToDateCheckDurationMillis));
            var duration = Assert.IsType<double>(durationProp.propertyValue);
            Assert.True(duration > 0.0);

            var fileCountProp = Assert.Single(telemetryEvent.Properties.Where(p => p.propertyName == TelemetryPropertyName.UpToDateCheckFileCount));
            var fileCount = Assert.IsType<int>(fileCountProp.propertyValue);
            Assert.True(fileCount >= 0);

            _telemetryEvents.Clear();
        }

        private void AssertTelemetrySuccessEvent()
        {
            var telemetryEvent = Assert.Single(_telemetryEvents);

            Assert.Equal(TelemetryEventName.UpToDateCheckSuccess, telemetryEvent.EventName);

            Assert.NotNull(telemetryEvent.Properties);
            Assert.Equal(2, telemetryEvent.Properties!.Count);

            var durationProp = Assert.Single(telemetryEvent.Properties.Where(p => p.propertyName == TelemetryPropertyName.UpToDateCheckDurationMillis));
            var duration = Assert.IsType<double>(durationProp.propertyValue);
            Assert.True(duration > 0.0);

            var fileCountProp = Assert.Single(telemetryEvent.Properties.Where(p => p.propertyName == TelemetryPropertyName.UpToDateCheckFileCount));
            var fileCount = Assert.IsType<int>(fileCountProp.propertyValue);
            Assert.True(fileCount >= 0);

            _telemetryEvents.Clear();
        }

        private sealed class AssertWriter : TextWriter, IEnumerable
        {
            private readonly ITestOutputHelper _output;
            private readonly Queue<string> _expectedLines;

            public AssertWriter(ITestOutputHelper output, params string[] expectedLines)
            {
                _output = output;
                _expectedLines = new Queue<string>(expectedLines);
            }

            public void Add(string line)
            {
                _expectedLines.Enqueue(line);
            }

            public override Encoding Encoding { get; } = Encoding.UTF8;

            public override void WriteLine(string value)
            {
                if (_expectedLines.Count == 0)
                {
                    throw new Xunit.Sdk.XunitException("Unexpected log message: " + value);
                }

                var expected = $"FastUpToDate: {_expectedLines.Dequeue()} ({Path.GetFileNameWithoutExtension(_projectFullPath)})";

                if (!string.Equals(expected, value))
                {
                    _output.WriteLine("Expected: " + expected);
                    _output.WriteLine("Actual:   " + value);
                }

                Xunit.Assert.Equal(expected, value);
            }

            public void Assert()
            {
                Xunit.Assert.Empty(_expectedLines);
            }

            IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException("Only for collection initialiser syntax");
        }

        #endregion
    }
}
