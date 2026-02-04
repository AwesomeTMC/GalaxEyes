using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;

namespace GalaxEyes.Optimizers
{
    public partial class ExampleSettings : FileSettings<ExampleSettings>
    {
        [JsonIgnore] public override string FileName => "example_settings.json";

        [ObservableProperty] [property: Name("Cause error intentionally?")] private bool _causeError = false;
        [ObservableProperty] private int _sleepAmount = 0;
    }
    public class ExampleOptimizer : Optimizer
    {
        public ExampleOptimizer() : base("Example Optimizer")
        {
        }
        public override ExampleSettings Settings { get; } = ExampleSettings.Load();

        public override List<Result> Check(String filePath)
        {
            List<Result> resultList = new List<Result>();
            String fileName = Path.GetFileName(filePath);

            if (Settings.CauseError)
                Util.AddError(ref resultList,
                    filePath, 
                    "Example optimizer ran into an error", 
                    OptimizerName, () => { return Check(filePath); },
                    "Set this to an empty string to not have an individual message for this result.");
            Thread.Sleep(Settings.SleepAmount);

            if (fileName.Contains("Unoptimized"))
            {
                // declare the actions the user can take
                List<OptimizerAction> actions = new() {
                    new OptimizerAction(() => { return OptimizeFile(filePath); }, "Rename file"),
                    new OptimizerAction(Util.NULL_ACTION, "Give up")
                };
                resultList.Add(new Result(ResultType.Optimize, filePath, "You have unoptimized file(s).", OptimizerName, actions));
            }

            return resultList;
        }

        public List<Result> OptimizeFile(String filePath)
        {
            String newPath = filePath.Replace("Unoptimized", "Optimized");
            if (!File.Exists(newPath) && File.Exists(filePath))
                File.Move(filePath, newPath);
            return new();
        }
    }
}
