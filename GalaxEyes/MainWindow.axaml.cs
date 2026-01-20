using Avalonia.Controls;
using Avalonia.Interactivity;
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

public class OptimizerRow
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
    public ObservableCollection<OptimizerRow> TestList { get; } = new();
}

public sealed class NoneFoundState : RightPaneState { }

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        foreach (Optimizer? optimizer in OptimizerList.Items)
        {
            if (optimizer != null)
                OptimizerRowList.Items.Add(optimizer);
        }

        RightPaneContent = new WaitingState();
    }

    private void ScrollHandler(object? sender, Avalonia.Controls.Primitives.ScrollEventArgs e)
    {
    }

    private async void ClickHandler(object? sender, RoutedEventArgs args)
    {
        Debug.WriteLine("Starting scan...");
        RightPaneContent = new ScanningState();

        await StartScan();
    }

    private async Task StartScan()
    {
        var optimizers = OptimizerList.Items.Cast<Optimizer>().ToList();
        string targetDirectory = ModPath.Text ?? "";

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
            resultsState.TestList.Add(new OptimizerRow { Label = result.Message, FilePath = filePath, Actions = actions }); 
        }

        if (resultsState.TestList.Count > 0)
            RightPaneContent = resultsState;
        else
            RightPaneContent = new NoneFoundState();
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