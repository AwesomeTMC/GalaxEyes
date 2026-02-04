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

namespace GalaxEyes;

public class OptimizerResultRow(string label, string filePath, string optimizerName, List<OptimizerAction> actions, OptimizerAction? selectedAction) : INotifyPropertyChanged
{
    public string Label { get; set; } = label;
    public string FilePath { get; set; } = filePath;

    public List<OptimizerAction> Actions { get; set; } = actions;
    private OptimizerAction? _selectedAction = selectedAction;
    public OptimizerAction? SelectedAction
    {
        get => _selectedAction;
        set
        {
            _selectedAction = value;
            PropertyChanged?.Invoke(this, new(nameof(SelectedAction)));
        }
    }

    public string OptimizerName { get; set; } = optimizerName;
    public string MainMessage
    {
        get
        {
            if (Label != null && Label != "")
            {
                return Label;
            }
            string modDirectory = MainSettings.Instance.ModDirectory;
            return Path.GetRelativePath(modDirectory, FilePath);
        }
    }
    public string GrayMessage
    {
        get
        {
            string modDirectory = MainSettings.Instance.ModDirectory;
            string path = Path.GetRelativePath(modDirectory, FilePath);
            if (Label != null && Label != "")
            {
                return path + "\n" + OptimizerName;
            }
            return OptimizerName;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class OptimizerResultGroup(List<OptimizerResultRow> optimizerResults, string groupMessage, List<OptimizerAction> actions, OptimizerAction? selectedAction, List<string> optimizerNames)
{
    public List<OptimizerResultRow> OptimizerResults { get; set; } = optimizerResults;
    public string GroupMessage { get; set; } = groupMessage;
    public List<string> OptimizerNames { get; set; } = optimizerNames;
    public string GrayMessage
    {
        get
        {
            return OptimizerResults.Count + " result(s). " + String.Join(", ", OptimizerNames);
        }
    }
    public List<OptimizerAction> Actions { get; set; } = actions;
    private OptimizerAction? _selectedAction = selectedAction;
    public OptimizerAction? SelectedAction
    {
        get => _selectedAction;
        set
        {
            _selectedAction = value;
            // Update all results in group with selected action if it has that action
            foreach (OptimizerResultRow row in OptimizerResults)
            {
                foreach (OptimizerAction action in row.Actions)
                {
                    if (value == null || action.CallbackName.Equals(value.CallbackName))
                    {
                        row.SelectedAction = action;
                        break;
                    }
                }
            }
        }
    }
}

public abstract class RightPaneState { }

public sealed class WaitingState : RightPaneState { }

public sealed class LoadingState(String label) : RightPaneState
{
    public string Label { get; set; } = label;
}

public sealed class ResultsState : RightPaneState
{
    public ObservableCollection<OptimizerResultGroup> ResultList { get; } = new();
}

public sealed class NoneFoundState : RightPaneState { }

public sealed class SelectDirectoryState : RightPaneState
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class DoneState : RightPaneState { }

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public MainWindow()
    {

        InitializeComponent();
        DataContext = this;
        foreach (Optimizer? optimizer in AllOptimizers.Items)
        {
            if (optimizer != null)
                OptimizerList.Items.Add(optimizer);
        }
        MainSettings.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainSettings.Instance.ModDirectory))
            {
                if (CheckDirectoryState())
                    RightPaneContent = new WaitingState();
            }
        };

        if (CheckDirectoryState())
            RightPaneContent = new WaitingState();
    }
    public MainSettings Settings => MainSettings.Instance;

    public string[] Themes { get; } =
    {
        "Dark",
        "Light",
        "System"
    };

    private void ScrollHandler(object? sender, Avalonia.Controls.Primitives.ScrollEventArgs e)
    {
    }

    private async void ScanButtonEvent(object? sender, RoutedEventArgs args)
    {
        if (!CheckDirectoryState())
            return;

        Debug.WriteLine("Starting scan...");
        RightPaneContent = new LoadingState("Scanning...");

        SetAllEnabled(false);
        await Task.Yield();
        await StartScan();
        SetAllEnabled(true);
    }

    private bool CheckDirectoryState()
    {
        var stagePath = Path.Combine(MainSettings.Instance.ModDirectory, "StageData");
        var objectPath = Path.Combine(MainSettings.Instance.ModDirectory, "ObjectData");
        bool isValidState = false;
        if (MainSettings.Instance.ModDirectory == "")
        {
            RightPaneContent = new SelectDirectoryState
            {
                Title = "Please select a directory",
                Description = "You must select a mod directory to scan.\n" +
                "It needs to have the folders \"StageData\" and \"ObjectData\" inside."
            };
        }
        else if (!Path.Exists(MainSettings.Instance.ModDirectory))
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
        string targetDirectory = MainSettings.Instance.ModDirectory;
        RightPaneContent = new LoadingState("Scanning...");

        var finalState = await Task.Run(() =>
        {
            List<Result> tempResults = new();

            if (Directory.Exists(targetDirectory))
            {
                var files = Directory.EnumerateFiles(targetDirectory, "*.*", SearchOption.AllDirectories);
                foreach (String file in files)
                {
                    tempResults.AddRange(ScanFile(file, optimizers));
                }
            }

            var state = new ResultsState();

            foreach (Result result in tempResults)
            {
                Util.AddResult(state.ResultList, result, result.Callbacks.FirstOrDefault());
            }

            return state;
        });

        RightPaneContent = finalState.ResultList.Any()
            ? finalState
            : new NoneFoundState();
    }

    private List<Result> ScanFile(string file, List<Optimizer> optimizers)
    {
        List<Result> tempResults = new();
        foreach (Optimizer? optimizer in optimizers)
        {
            if (optimizer != null && optimizer.DoCheck(file))
            {
                try
                {
                    tempResults.AddRange(optimizer.Check(file));
                }
                catch (Exception e)
                {
                    Util.AddException(ref tempResults, e, file, optimizer.OptimizerName, () => { return optimizer.Check(file); });
                }
            }
        }
        return tempResults;
    }

    private async void ResolveButtonEvent(object? sender, RoutedEventArgs args)
    {
        SetAllEnabled(false);
        await StartResolve();
        SetAllEnabled(true);
    }

    private async Task StartResolve()
    {
        ResultsState? resultsState = RightPaneContent as ResultsState;
        if (resultsState == null)
            return;
        RightPaneContent = new LoadingState("Resolving...");

        List<Result> newResults = await Task.Run(() =>
        {
            List<Result> tempResults = new();
            foreach (var group in resultsState.ResultList)
            {
                foreach (var result in group.OptimizerResults)
                {
                    if (result == null || result.SelectedAction == null)
                        continue;

                    try
                    {
                        tempResults.AddRange(result.SelectedAction.Callback());
                    }
                    catch (Exception e)
                    {
                        Util.AddException(ref tempResults, e, result.FilePath, result.OptimizerName, result.SelectedAction.Callback);
                    }
                }

            }

            return tempResults;
        });

        newResults.AddRange(await Task.Run(() =>
        {
            List<Result> tempResults = new();
            foreach (var optimizer in AllOptimizers.Items)
            {
                try
                {
                    tempResults.AddRange(optimizer.RunAfter());
                }
                catch (Exception e)
                {
                    Util.AddException(ref tempResults, e, "All files", optimizer.OptimizerName, () => { return optimizer.RunAfter(); });
                }
            }
            return tempResults;
        }));



        resultsState.ResultList.Clear();

        foreach (Result result in newResults)
        {
            Util.AddResult(resultsState.ResultList, result, result.Callbacks.FirstOrDefault());
        }

        if (resultsState.ResultList.Count > 0)
            RightPaneContent = resultsState;
        else
            RightPaneContent = new DoneState();
    }

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
        options.SuggestedStartLocation = await this.StorageProvider.TryGetFolderFromPathAsync(MainSettings.Instance.ModDirectory);
        var result = await this.StorageProvider.OpenFolderPickerAsync(options);

        if (result != null && result.Count > 0)
        {
            var storageFolder = result[0];
            MainSettings.Instance.ModDirectory = storageFolder.Path.AbsolutePath.Replace("%20", " ");
        }
    }

    private async void OptimizerSettingsEvent(object? sender, RoutedEventArgs args)
    {
        var button = (Button?)sender;
        var optimizer = (Optimizer?)button?.DataContext!;
        if (optimizer == null)
            return;

        var x = new OptimizerSettingsWindow(optimizer);
        await x.ShowDialog(this);

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

    private void ThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateTheme(MainSettings.Instance.CurrentTheme);
    }

    public void SetAllEnabled(bool isEnabled)
    {
        OptimizerList.IsEnabled = isEnabled;
        SettingsTab.IsEnabled = isEnabled;
        StartScanButton.IsEnabled = isEnabled;
    }
}