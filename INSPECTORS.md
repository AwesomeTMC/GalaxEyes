# Overview
This guide is for people wanting to contribute a custom inspector to GalaxEyes. It shouldn't be too difficult but there's a lot of details to keep in mind. Generally, I recommend looking at existing inspectors and especially `ExampleOptimizer`.
# Settings
Each inspector can have their own settings. They are defined at the top of your optimizers .cs file:

```c#
namespace GalaxEyes.Inspectors
{
    public partial class ExampleSettings : FileSettings<ExampleSettings>
    {
        [JsonIgnore] public override string FileName => "example_settings.json";

        [ObservableProperty] [property: Name("Cause error intentionally?")] private bool _causeError = false;
        [ObservableProperty] [property: Name("Cause independent error intentionally?")] private bool _causeIndependentError = false;
        [ObservableProperty] private int _sleepAmount = 0;
    }
    ...
}
```

They can have no settings too. This will remove the settings button from the UI.

```c#
public YourInspector() : base("Your Inspector")
{
}
public override IHaveSettings? Settings { get; } = null;
```

All inspector settings will have their UI and JSON automatically generated for convenience. As long as the settings are loaded in the optimizer's class itself:

```c#
public ExampleOptimizer() : base("Example Optimizer")
{
}
public override ExampleSettings Settings { get; } = ExampleSettings.Load();
```
## Attributes
If you want custom UI for a particular setting, you'll have to use an attribute.
The attributes that already exist are:
### `[property: Folder("What to say on the open folder dialog")]`
Adds an "Open Folder" button that lets the user select a directory.
You can use it like this:
```c#
[ObservableProperty] [property: Folder("Please select a vanilla directory. It should not have any modified files.")] private string _vanillaDirectory = "";
```
### `[property: Name("Custom name for the setting")]`
Overrides the automatically generated display name for the setting.
## Custom Attributes
If none of those are what you want, you can make your own. This is a little trickier.

At the top of `AppSettings.cs`, define your custom attribute:
```c#
[AttributeUsage(AttributeTargets.Property)]
public sealed class FolderAttribute(string desc) : Attribute
{
    public string Title { get; } = desc;
}
```

It can have as many or as few parameters as you want.

Now, in `InspectorSettingsWindow.axaml.cs` in `SettingTemplateSelector`'s `Build`, check if your attribute exists, and if it does, apply a typeName.

```c#
if (entry.GetAttribute<FolderAttribute>() != null)
{
    typeName = "Folder";
}
```

Then in `InspectorSettingsWindow.axaml`, add a new template beow the others:

```xml
<DataTemplate x:Key="Int32" x:DataType="local:LiveSettingEntry">
	<NumericUpDown Value="{Binding Value, Mode=TwoWay}" Minimum="-2147483648" Maximum="2147483647"/>
</DataTemplate>
<DataTemplate x:Key="UInt32" x:DataType="local:LiveSettingEntry">
	<NumericUpDown Value="{Binding Value, Mode=TwoWay}" Minimum="0" Maximum="4294967295"/>
</DataTemplate>
<DataTemplate x:Key="Folder" x:DataType="local:LiveSettingEntry">
	<Grid ColumnDefinitions="Auto *" ColumnSpacing="10">
		<Button Grid.Column="0" Click="ButtonOpenFolder"
				Content="Open Folder"/>
		<TextBox Grid.Column="1" Text="{Binding Value, Mode=TwoWay}" HorizontalAlignment="Stretch" />
	</Grid>
	
</DataTemplate>
```

Get yourself familiar with Avalonia's binding system.
# Checking
Add the `public override List<Result> Check(String filePath)` method to your inspector.


Important details:
- This function should not modify any files.
- It needs to return a `List<Result>`.
- Follow the style of existing optimizers.

You can also have the `SettingsCheck` method. As the name suggests, it is for checking if the settings have any problems. 

```c#
public override List<Result> SettingsCheck()
{
    List<Result> results = new();
    if (Settings.CauseIndependentError)
    {
        Util.AddError(ref results,
            "*",
            "Example optimizer ran into an error",
            InspectorName, null,
            "Hi it's me, the independent error.");
    }
    return results;
}
```

# Resolving
Resolving does the actual actions. This should modify files.

Keep in mind that the file could've changed in between `Check` and your action's function.