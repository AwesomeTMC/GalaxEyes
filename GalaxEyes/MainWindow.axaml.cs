using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using GalaxEyes.Optimizers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace GalaxEyes;

public class OptimizerResultRow
{
    public string Label { get; set; } = "";
    public bool IsChecked { get; set; }
    public string FilePath { get; set; } = "";
    public List<string> Actions { get; set; } = new();
    public string SelectedAction { get; set; } = "";
}

public abstract class RightPaneState { }

public sealed class WaitingState : RightPaneState { }

public sealed class ScanningState : RightPaneState { }

public sealed class ResultsState : RightPaneState
{
    public ObservableCollection<OptimizerResultRow> ResultList { get; } = new();
}

public sealed class NoneFoundState : RightPaneState { }

public sealed class SelectDirectoryState : RightPaneState
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public MainWindow()
    {
        CurrentTheme = AppSettings.Default.CurrentTheme;

        InitializeComponent();
        DataContext = this;
        foreach (Optimizer? optimizer in AllOptimizers.Items)
        {
            if (optimizer != null)
                OptimizerList.Items.Add(optimizer);
        }
        ModDirectory = AppSettings.Default.ModDirectory;
    }

    public string[] Themes { get; } =
    {
        "Dark",
        "Light",
        "System"
    };

    private void ScrollHandler(object? sender, Avalonia.Controls.Primitives.ScrollEventArgs e)
    {
    }

    private async void ClickHandler(object? sender, RoutedEventArgs args)
    {
        if (!CheckDirectoryState())
            return;

        Debug.WriteLine("Starting scan...");
        RightPaneContent = new ScanningState();

        await StartScan();
    }

    private bool CheckDirectoryState()
    {
        var stagePath = Path.Combine(ModDirectory, "StageData");
        var objectPath = Path.Combine(ModDirectory, "ObjectData");
        bool isValidState = false;
        if (ModDirectory == "")
        {
            RightPaneContent = new SelectDirectoryState
            {
                Title = "Please select a directory",
                Description = "You must select a mod directory to scan.\n" +
                "It needs to have the folders \"StageData\" and \"ObjectData\" inside."
            };
        }
        else if (!Path.Exists(ModDirectory))
        {
            RightPaneContent = new SelectDirectoryState
            {
                Title = "Invalid Directory",
                Description = "Your selected mod directory doesn't exist.\n" +
                "Please select a mod directory the folders \"StageData\" and \"ObjectData\" inside."
            };
        }
        else if (!Path.Exists(stagePath) || !Path.Exists(objectPath))
        {
            RightPaneContent = new SelectDirectoryState
            {
                Title = "Invalid Directory",
                Description = "Your selected mod directory doesn't appear to be a mod directory.\n" +
                "It needs to have the folders \"StageData\" and \"ObjectData\" inside."
            };
        }
        else
        {
            isValidState = true;
        }
        StartScanButton.IsEnabled = isValidState;
        return isValidState;
    }

    private async Task StartScan()
    {
        var optimizers = OptimizerList.Items.Cast<Optimizer>().ToList();
        string targetDirectory = ModDirectory;

        List<Result> results = await Task.Run(() =>
        {
            List<Result> tempResults = new();

            if (!Directory.Exists(targetDirectory)) return tempResults;

            foreach (String file in Directory.GetFiles(targetDirectory, "*.*", SearchOption.AllDirectories))
            {
                foreach (Optimizer? optimizer in optimizers)
                {
                    if (optimizer != null && optimizer.IsActive)
                    {
                        try
                        {
                            tempResults.AddRange(optimizer.Check(file));
                        }
                        catch (Exception e)
                        {
                            tempResults.Add(new Result(ResultType.Error, e.ToString(), file));
                        }
                    }
                }
            }
            return tempResults;
        });

        var resultsState = new ResultsState();

        foreach (Result result in results)
        {
            String filePath = result.AffectedFile.Replace(targetDirectory, "");
            if (targetDirectory.EndsWith("\\") || targetDirectory.EndsWith("/"))
                filePath = "<Your Mod>/" + filePath;
            else
                filePath = "<Your Mod>" + filePath;
            filePath = filePath.Replace("\\", "/");

            List<string> actions = new();
            foreach (OptimizerAction action in result.Callbacks)
            {
                actions.Add(action.CallbackName);
            }
            resultsState.ResultList.Add(new OptimizerResultRow { Label = result.Message, FilePath = filePath, Actions = actions });
        }

        if (resultsState.ResultList.Count > 0)
            RightPaneContent = resultsState;
        else
            RightPaneContent = new NoneFoundState();
    }

    // Only meant for use by ModDirectory's 'set' method.
    private void UpdateTheme(String? theme)
    {
        switch (theme)
        {
            case "Dark":
                Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
                break;
            case "Light":
                Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
                break;
            default:
                theme = "System";
                Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
                break;
        }
    }

    private async void OpenModDirectoryDialog(object? sender, RoutedEventArgs e)
    {
        var options = new Avalonia.Platform.Storage.FolderPickerOpenOptions();
        options.Title = "Select your mod directory. It usually contains 'StageData'.";
        options.AllowMultiple = false;
        options.SuggestedStartLocation = await this.StorageProvider.TryGetFolderFromPathAsync(AppSettings.Default.ModDirectory);
        var result = await this.StorageProvider.OpenFolderPickerAsync(options);

        if (result != null && result.Count > 0)
        {
            var storageFolder = result[0];
            ModDirectory = storageFolder.Path.AbsolutePath.Replace("%20", " ");
        }
    }

    private string _currentTheme;
    public string CurrentTheme
    {
        get => _currentTheme;
        set
        {
            _currentTheme = value;
            SettingChanged(nameof(CurrentTheme), value);
            UpdateTheme(value);
        }
    }

    private string _modDirectory;
    public string ModDirectory
    {
        get => _modDirectory;
        set
        {
            if (_modDirectory == value)
                return;
            _modDirectory = value;
            SettingChanged(nameof(ModDirectory), value);
            if (CheckDirectoryState())
                RightPaneContent = new WaitingState();
        }
    }

    private void SettingChanged(string name, object val)
    {
        PropertyChanged?.Invoke(this, new(name));
        AppSettings.Default[name] = val;
        AppSettings.Default.Save();
    }

    private RightPaneState? _rightPaneContent;
    public RightPaneState? RightPaneContent
    {
        get => _rightPaneContent;
        set
        {
            _rightPaneContent = value;
            PropertyChanged?.Invoke(this, new(nameof(RightPaneContent)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}