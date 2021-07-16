# HOW TO: Add a New Launch Profile UI

Launch Profile UIs are defined by XAML files describing an instance of the [Rule](https://docs.microsoft.com/en-us/dotnet/api/microsoft.build.framework.xamltypes.rule) class. Visual Studio uses these XAML files to dynamically build the UI and bind the controls to launch settings. This HOW TO describes two options for defining and distributing these XAML files. At the end your Launch Profile will look something like this:

![New Launch Profile](new-launch-profile.png)

## Option 1: XAML file on disk

In this option, the XAML resides in a standalone .xaml file on disk and is included in the end user's project as a specific kind of MSBuild item, `PropertyPageSchema`. Visual Studio reads these items from the project to determine which kinds of Launch Profiles it can display.

This may be an attractive option if you already have a NuGet package or Visual Studio extension that injects MSBuild .props and .targets files into the end user's project, or if you want to use MSBuild `Condition`s to control when the Launch Profile UI is available to a project. 

### Step 1 (optional): Add the Microsoft.Build.Framework package

Use the NuGet Package Manager to add the Microsoft.Build.Framework package to your project. This is an optional step, but it will allow the XAML editor to find the [Rule](https://docs.microsoft.com/en-us/dotnet/api/microsoft.build.framework.xamltypes.rule) type (and related types) and provide code completion, tool tips, Go to Definition, and other features while you type.

### Step 2: Define the XAML file

Add a new XAML file named "MyLaunchProfile.xaml" to your project. Depending on how the file is created you may end up with a `<Page>` item in your project but this is not what we want as we're not using the file to describe a piece of WPF UI.

Update your project to replace the `<Page>` item with one of the following:

- SDK-style projects:
  ``` xml
  <None Update="MyPropertyPage.xaml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
  ```
- Non-SDK-style projects:
  ``` xml
  <None Include="MyPropertyPage.xaml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
  ```

Now VS won't do anything with this file but copy it to the output directory when you build.

### Step 3: Define the `PropertyPageSchema` item

Next you need to update the .props or .targets files imported by the end users' projects to properly reference the property page so Visual Studio can find it. Note that the creation and distribution of the .props and .targets files (as well as the distribution of MyPropertyPage.xaml itself) is beyond the scope of this document.

Add the following item to your .props or .targets file:

``` xml
<PropertyPageSchema Include="path\to\MyLaunchProfile.xaml">
  <Context>Project</Context>
</PropertyPageSchema>
```

### Step 4: Describe the property page

Replace the contents of MyLaunchProfile.xaml with the following:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Rule Name="MyLaunchProfile"
      Description="Properties associated with launching and debugging a custom debug target."
      DisplayName="My Custom Debug Target"
      PageTemplate="commandNameBasedDebugger"
      xmlns:sys="clr-namespace:System;assembly=mscorlib"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns="http://schemas.microsoft.com/build/2009/properties">

    <Rule.Metadata>
        <sys:String x:Key="CommandName">MyCustomDebugTarget</sys:String>

        <!-- KnownImageIds.ImageCatalogGuid -->
        <sys:Guid x:Key="ImageMonikerGuid">AE27A6B0-E345-4288-96DF-5EAF394EE369</sys:Guid>

        <!-- KnownImageIds.Execute -->
        <sys:Int32 x:Key="ImageMonikerId">1173</sys:Int32>
    </Rule.Metadata>

    <Rule.DataSource>
        <DataSource Persistence="LaunchProfile"
                    HasConfigurationCondition="False"
                    ItemType="LaunchProfile"/>
    </Rule.DataSource>

    <StringProperty Name="ExecutablePath"
                    DisplayName="Executable"
                    Description="Path to the executable to run."
                    Subtype="file" />
</Rule>
```

The format of the file is described in detail in [Property Specification](property-specification.md), but the most important points are:
- The `Name` must be unique.
- The `PageTemplate` attribute must have the value `"commandNameBasedDebugger"`.
- The `Rule.Metadata` element must specify a `CommandName`: this is the debug command name (also known as a debug target) corresponding to this UI. Debug commands and how they are handled by VS are beyond the scope of this document,
- The `ImageMonikerGuid` and `ImageMonikerId` in `Rule.Metadata` together define an ImageMoniker designating the icon to show for this Launch Profile UI. These are optional and if they are not defined a default icon will be supplied.
- The `Rule.DataSource` element must be specified as shown above.
  - The `ItemType` attribute indicates that this `Rule` only pertains to launch profiles, as opposed to other types of items that are typically found in a project (`Compile`, `Reference`, etc.).
  - The `Persistence` attribute indicates that values for the properties should be read and written through the launch profile persistence mechanism, as opposed to trying to read data from the MSBuild file.

Further notes:
- Properties are ordered in the UI as they are ordered in the .xaml file.
- Properties can be placed in categories, just like the properties on property pages. However, the Launch Profiles UI currently ignores categories.

You should now be able to build and see the MyLaunchProfile.xaml copied as-is to the output directory.

And you're done. Projects that import the .targets file will now show this UI when editing the Launch Profiles.

## Option 2: Embedded XAML file

In this option the XAML file is embedded in an assembly as a resource and discovered by means of a MEF export. Compared to Option 1, this requires more initial setup but does not require you to distribute an additional file. This may be an attractive option if you are already exporting MEF components for use in Visual Studio.

### Step 1: Define the `XamlPropertyRule` item

Add the following item to your .csproj file:

```xml
  <ItemGroup>
    <XamlPropertyRule Include="path\to\MyPropertyPage.xaml">
      <Namespace>MyNamespace</Namespace>
      <DataAccess>IRule</DataAccess>
      <RuleInjectionClassName>MyPropertyPageProperties</RuleInjectionClassName>
    </XamlPropertyRule>
  </ItemGroup>
```

### Step 2: Register the property page and properties

Add a partial class that corresponds to the properties defined within the rule and generated during build:

```csharp
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using System;
using System.ComponentModel.Composition;

namespace MyNamespace
{
    [Export]
    [AppliesTo(MyProject.UniqueCapability)]
    internal partial class MyPropertyPageProperties : StronglyTypedPropertyAccess
    {
        [ExportPropertyXamlRuleDefinition("<Assembly Name>, Version=<Assembly Version>, Culture=<Assembly Culture>, PublicKeyToken=<Assembly Public Key Token>", "XamlRuleToCode:MyPropertyPage.xaml", "Project")]
        [AppliesTo(MyProject.UniqueCapability)]
        private object MyRule { get { throw new NotImplementedException(); } }

        [ImportingConstructor]
        public MyPropertyPageProperties(ConfiguredProject configuredProject)
            : base(configuredProject)
        {
        }

        public MyPropertyPageProperties(ConfiguredProject configuredProject, string file, string itemType, string itemName)
            : base(configuredProject, file, itemType, itemName)
        {
        }

        public MyPropertyPageProperties(ConfiguredProject configuredProject, IProjectPropertiesContext projectPropertiesContext)
            : base(configuredProject, projectPropertiesContext)
        {
        }

        public MyPropertyPageProperties(ConfiguredProject configuredProject, UnconfiguredProject project)
            : base(configuredProject, project)
        {
        }
    }
}
```

### Step 3: Register the assembly codebase

In order for Visual Studio to locate the rule, you must register the codebase of the assembly embedding the rule from the Visual Studio package via the `ProvideCodeBaseAttribute`.

> Note: this is true *even when other MEF components are successfully loaded without a registered codebase*.

```csharp
using Microsoft.VisualStudio.Shell;

[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Path\To\MyAssembly.dll")]

```

### Step 4: Bind rule properties to launch settings values

Registering the property page allows the settings UI to be visible when the launch profile is selected, but settings will not be read from or written to the launch profile until they are bound to a specific launch profile values via a `ILaunchProfileExtensionValueProvider` implementation.

```csharp
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using System.Collections.Immutable;

namespace MyNamespace
{
    [ExportLaunchProfileExtensionValueProvider(
    new[]
    {
            ExecutablePathPropertyName
    },
    ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    [AppliesTo(MyProject.UniqueCapability)]
    internal sealed class MyLaunchProfileExtensionValueProvider : ILaunchProfileExtensionValueProvider
    {
        private const string ExecutablePathPropertyName = "ExecutablePath";

        public string OnGetPropertyValue(string propertyName, ILaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule rule)
        {
            string propertyValue = null;
            switch (propertyName)
            {
                case ExecutablePathPropertyName:
                    propertyValue = (string)launchProfile.OtherSettings["executablePath"];
                    break;
            }

            return propertyValue ?? string.Empty;
        }

        public void OnSetPropertyValue(string propertyName, string propertyValue, IWritableLaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule rule)
        {
            switch (propertyName)
            {
                case ExecutablePathPropertyName:
                    launchProfile.OtherSettings["executablePath"] = propertyValue;
                    break;
            }
        }
    }
}
```