using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using GalaxEyes.Inspectors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GalaxEyes;

public class InspectorResultRow(string label, string filePath, string inspectorName, List<InspectorAction> actions, InspectorAction? selectedAction) : INotifyPropertyChanged
{
    public string Label { get; set; } = label;
    public string FilePath { get; set; } = filePath;

    public List<InspectorAction> Actions { get; set; } = actions;
    private InspectorAction? _selectedAction = selectedAction;
    public InspectorAction? SelectedAction
    {
        get => _selectedAction;
        set
        {
            _selectedAction = value;
            PropertyChanged?.Invoke(this, new(nameof(SelectedAction)));
        }
    }

    public string InspectorName { get; set; } = inspectorName;
    public string MainMessage
    {
        get
        {
            if (Label != null && Label != "")
            {
                return Label;
            }
            string modDirectory = MainSettings.Instance.ModDirectory;
            return GetPathMessage(modDirectory);
        }
    }
    public string GrayMessage
    {
        get
        {
            string modDirectory = MainSettings.Instance.ModDirectory;
            string path = GetPathMessage(modDirectory);
            if (Label != null && Label != "")
            {
                return path + "\n" + InspectorName;
            }
            return InspectorName;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private string GetPathMessage(string modDirectory)
    {
        if (FilePath == "*")
            return "All files are affected.";
        return Path.GetRelativePath(modDirectory, FilePath);
    }
}

public class InspectorResultGroup(List<InspectorResultRow> inspectorResults, string groupMessage, List<InspectorAction> actions, InspectorAction? selectedAction, List<string> inspectorNames)
{
    public List<InspectorResultRow> InspectorResults { get; set; } = inspectorResults;
    public string GroupMessage { get; set; } = groupMessage;
    public List<string> InspectorNames { get; set; } = inspectorNames;
    public string GrayMessage
    {
        get
        {
            return InspectorResults.Count + " result(s). " + String.Join(", ", InspectorNames);
        }
    }
    public List<InspectorAction> Actions { get; set; } = actions;
    private InspectorAction? _selectedAction = selectedAction;
    public InspectorAction? SelectedAction
    {
        get => _selectedAction;
        set
        {
            _selectedAction = value;
            // Update all results in group with selected action if it has that action
            foreach (InspectorResultRow row in InspectorResults)
            {
                foreach (InspectorAction action in row.Actions)
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
    public ObservableCollection<InspectorResultGroup> ResultList { get; } = new();
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
        foreach (Inspector? inspector in AllInspectors.Items)
        {
            if (inspector != null)
                InspectorList.Items.Add(inspector);
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
        var allInspectors = InspectorList.Items.Cast<Inspector>().ToList();
        string targetDirectory = MainSettings.Instance.ModDirectory;
        RightPaneContent = new LoadingState("Scanning...");

        var finalState = await Task.Run(() =>
        {
            List<Result> tempResults = new();

            List<Inspector> inspectors = new List<Inspector>();
            foreach (Inspector inspector in allInspectors)
            {
                if (!inspector.IsActive)
                {
                    continue;
                }
                var inspectorResults = inspector.SettingsCheck();
                if (inspectorResults.Count == 0)
                    inspectors.Add(inspector);
                else
                    tempResults.AddRange(inspectorResults);
            }

            if (Directory.Exists(targetDirectory) && inspectors.Count > 0)
            {
                var files = Directory.EnumerateFiles(targetDirectory, "*.*", SearchOption.AllDirectories);
                foreach (String file in files)
                {
                    tempResults.AddRange(ScanFile(file, inspectors));
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

    private List<Result> ScanFile(string file, List<Inspector> inspectors)
    {
        List<Result> tempResults = new();
        foreach (Inspector? inspector in inspectors)
        {
            if (inspector != null && inspector.DoCheck(file))
            {
                try
                {
                    tempResults.AddRange(inspector.Check(file));
                }
                catch (Exception e)
                {
                    Util.AddException(ref tempResults, e, file, inspector.InspectorName, () => { return inspector.Check(file); });
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
                foreach (var result in group.InspectorResults)
                {
                    if (result == null || result.SelectedAction == null)
                        continue;

                    try
                    {
                        tempResults.AddRange(result.SelectedAction.Callback());
                    }
                    catch (Exception e)
                    {
                        Util.AddException(ref tempResults, e, result.FilePath, result.InspectorName, result.SelectedAction.Callback);
                    }
                }

            }

            return tempResults;
        });

        newResults.AddRange(await Task.Run(() =>
        {
            List<Result> tempResults = new();
            foreach (var inspector in AllInspectors.Items)
            {
                try
                {
                    tempResults.AddRange(inspector.RunAfter());
                }
                catch (Exception e)
                {
                    Util.AddException(ref tempResults, e, "All files", inspector.InspectorName, () => { return inspector.RunAfter(); });
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

    private async void InspectorSettingsEvent(object? sender, RoutedEventArgs args)
    {
        var button = (Button?)sender;
        var inspector = (Inspector?)button?.DataContext!;
        if (inspector == null)
            return;

        var x = new InspectorSettingsWindow(inspector);
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
        InspectorList.IsEnabled = isEnabled;
        SettingsTab.IsEnabled = isEnabled;
        StartScanButton.IsEnabled = isEnabled;
    }
}