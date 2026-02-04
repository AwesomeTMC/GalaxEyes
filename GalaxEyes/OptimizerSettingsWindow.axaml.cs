using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Metadata;
using Avalonia.Platform.Storage;
using GalaxEyes.Optimizers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace GalaxEyes;

public partial class OptimizerSettingsWindow : Window, INotifyPropertyChanged
{
    public OptimizerSettingsWindow(Optimizer associatedOptimizer)
    {
        AssociatedOptimizer = associatedOptimizer;
        InitializeComponent();
        DataContext = this;
        if (AssociatedOptimizer.Settings != null)
        {
            AllSettings.ItemsSource = AssociatedOptimizer.Settings.GetEditableEntries();
        }

    }

    public Optimizer AssociatedOptimizer { get; set; }

    private async void ButtonOpenFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not LiveSettingEntry setting)
            return;

        var options = new Avalonia.Platform.Storage.FolderPickerOpenOptions();
        var attr = setting.GetAttribute<FolderAttribute>();
        if (attr == null)
            return;
        var desc = attr.Title;
        options.Title = desc;
        options.AllowMultiple = false;
        options.SuggestedStartLocation = await this.StorageProvider.TryGetFolderFromPathAsync(setting.Value.ToString());
        var result = await this.StorageProvider.OpenFolderPickerAsync(options);

        if (result != null && result.Count > 0)
        {
            var storageFolder = result[0];
            setting.Value = storageFolder.Path.AbsolutePath.Replace("%20", " ");
        }
    }
}

public class SettingTemplateSelector : IDataTemplate
{
    [Content]
    public Dictionary<string, IDataTemplate> Templates { get; } = new();

    public Control? Build(object? param)
    {
        if (param is not LiveSettingEntry entry) return null;

        string typeName = entry.Value.GetType().Name;

        if (entry.GetAttribute<FolderAttribute>() != null)
        {
            typeName = "Folder";
        }
        if (Templates.TryGetValue(typeName, out var template))
        {
            return template.Build(param);
        }

        return Templates["String"].Build(param);
    }

    public bool Match(object? data) => data is LiveSettingEntry;
}