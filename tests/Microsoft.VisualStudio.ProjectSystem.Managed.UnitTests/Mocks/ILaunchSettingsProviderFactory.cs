// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.Threading;
using Moq;

namespace Microsoft.VisualStudio.ProjectSystem
{
    internal class ILaunchSettingsProviderFactory
    {
        /// <summary>
        /// Creates a mock <see cref="ILaunchSettingsProvider"/> for testing purposes.
        /// </summary>
        /// <param name="activeProfileName">The name of the active profile in the new object.</param>
        /// <param name="launchProfiles">The initial set of launch profiles to expose through the provider.</param>
        /// <param name="setActiveProfileCallback">An optional method to call when the active profile is set.</param>
        /// <param name="updateLaunchSettingsCallback">An optional method to call when the set of launch settings is updated</param>
        /// <param name="getProfilesCallback">
        /// A optional method to call when the set of profile is queried. Given the initial set of <paramref name="launchProfiles"/> as an argument.
        /// </param>
        /// <param name="addOrUpdateProfileCallback">An optional method to call when a profile is added or updated.</param>
        /// <param name="removeProfileCallback">An optional methods to call when a profile is removed.</param>
        /// <param name="tryUpdateProfileCallback">An optional method to call when a profile is updated.</param>
        /// <param name="updateGlobalSettingsCallback">An optional method to call when a global setting is updated.</param>
        /// <param name="globalSettings">The initial set of global settings to expose through the provider.</param>
        public static ILaunchSettingsProvider3 Create(
            string? activeProfileName = null,
            IEnumerable<ILaunchProfile>? launchProfiles = null,
            Action<string>? setActiveProfileCallback = null,
            Action<ILaunchSettings>? updateLaunchSettingsCallback = null,
            Func<ImmutableList<ILaunchProfile>, ImmutableList<ILaunchProfile>>? getProfilesCallback = null,
            Action<ILaunchProfile, bool>? addOrUpdateProfileCallback = null,
            Action<string>? removeProfileCallback = null,
            Action<string, Action<IWritableLaunchProfile>>? tryUpdateProfileCallback = null,
            Func<ImmutableDictionary<string, object>, ImmutableDictionary<string, object?>>? updateGlobalSettingsCallback = null,
            ImmutableDictionary<string, object>? globalSettings = null)
        {
            var launchSettingsMock = new Mock<ILaunchSettings>();

            var initialLaunchProfiles = launchProfiles is not null
                ? launchProfiles.ToImmutableList()
                : ImmutableList<ILaunchProfile>.Empty;

            var initialGlobalSettings = globalSettings ?? ImmutableDictionary<string, object>.Empty;

            if (getProfilesCallback is not null)
            {
                launchSettingsMock.Setup(t => t.Profiles).Returns(() => getProfilesCallback(initialLaunchProfiles));
            }
            else
            {
                launchSettingsMock.Setup(t => t.Profiles).Returns(initialLaunchProfiles);
            }

            if (initialGlobalSettings is not null)
            {
                launchSettingsMock.Setup(t => t.GlobalSettings).Returns(initialGlobalSettings);
            }

            if (activeProfileName != null)
            {
                var activeLaunchProfile = launchProfiles?.FirstOrDefault(p => p.Name == activeProfileName)
                    ?? new LaunchProfile { Name = activeProfileName };
                launchSettingsMock.Setup(t => t.ActiveProfile).Returns(activeLaunchProfile);
            }

            var launchSettings = launchSettingsMock.Object;

            var settingsProviderMock = new Mock<ILaunchSettingsProvider3>();
            settingsProviderMock.Setup(t => t.WaitForFirstSnapshot(It.IsAny<int>())).Returns(Task.FromResult<ILaunchSettings?>(launchSettings));
            settingsProviderMock.SetupGet(t => t.CurrentSnapshot).Returns(launchSettings);

            if (setActiveProfileCallback != null)
            {
                settingsProviderMock.Setup(t => t.SetActiveProfileAsync(It.IsAny<string>()))
                    .Returns<string>(v =>
                    {
                        setActiveProfileCallback(v);
                        return Task.CompletedTask;
                    });
            }

            if (updateLaunchSettingsCallback != null)
            {
                settingsProviderMock.Setup(t => t.UpdateAndSaveSettingsAsync(It.IsAny<ILaunchSettings>()))
                    .Returns<ILaunchSettings>(v =>
                    {
                        updateLaunchSettingsCallback(v);
                        return Task.CompletedTask;
                    });
            }

            if (addOrUpdateProfileCallback is not null)
            {
                settingsProviderMock.Setup(t => t.AddOrUpdateProfileAsync(It.IsAny<ILaunchProfile>(), It.IsAny<bool>()))
                    .Returns<ILaunchProfile, bool>((profile, addToFront) =>
                    {
                        addOrUpdateProfileCallback(profile, addToFront);
                        return Task.CompletedTask;
                    });
            }

            if (removeProfileCallback is not null)
            {
                settingsProviderMock.Setup(t => t.RemoveProfileAsync(It.IsAny<string>()))
                    .Returns<string>(name =>
                    {
                        removeProfileCallback(name);
                        return Task.CompletedTask;
                    });
            }

            var settingsProvider3Mock = settingsProviderMock.As<ILaunchSettingsProvider3>();
            if (tryUpdateProfileCallback is not null)
            {
                settingsProvider3Mock.Setup(t => t.TryUpdateProfileAsync(It.IsAny<string>(), It.IsAny<Action<IWritableLaunchProfile>>()))
                    .Returns<string, Action<IWritableLaunchProfile>>((name, action) =>
                    {
                        tryUpdateProfileCallback(name, action);
                        return TaskResult.True;
                    });
            }
            else
            {
                settingsProvider3Mock.Setup(t => t.TryUpdateProfileAsync(It.IsAny<string>(), It.IsAny<Action<IWritableLaunchProfile>>()))
                    .Returns<string, Action<IWritableLaunchProfile>>((name, action) =>
                    {
                        var profile = new WritableLaunchProfile { Name = name };
                        action(profile);
                        return TaskResult.True;
                    });

            }

            if (updateGlobalSettingsCallback is not null)
            {
                settingsProvider3Mock.Setup(t => t.UpdateGlobalSettingsAsync(It.IsAny<Func<ImmutableDictionary<string, object>, ImmutableDictionary<string, object?>>>()))
                    .Returns(updateGlobalSettingsCallback);
            }
            else
            {
                settingsProvider3Mock.Setup(t => t.UpdateGlobalSettingsAsync(It.IsAny<Func<ImmutableDictionary<string, object>, ImmutableDictionary<string, object?>>>()))
                    .Returns<Func<ImmutableDictionary<string, object>, ImmutableDictionary<string, object?>>>(updateFunc =>
                    {
                        _ = updateFunc(ImmutableDictionary<string, object>.Empty);
                        return Task.CompletedTask;
                    });
            }

            var settingsProvider = settingsProviderMock.Object;
            return settingsProvider;
        }
    }
}
