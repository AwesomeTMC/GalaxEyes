using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GalaxEyes.Optimizers
{
    public partial class ExampleSettings : FileSettings<ExampleSettings>
    {
        [JsonIgnore] public override string FileName => "example_settings.json";

        [ObservableProperty] private bool _causeError = false;
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
            if (!IsActive)
                return resultList;

            String fileName = Path.GetFileName(filePath);

            if (Settings.CauseError)
                Util.AddError(ref resultList, "Some error occured", filePath, () => { return Check(filePath); });
            Thread.Sleep(Settings.SleepAmount);

            if (fileName.Contains("Unoptimized"))
            {
                // declare the actions the user can take
                List<OptimizerAction> actions = new() {
                    new OptimizerAction(() => { return OptimizeFile(filePath); }, "Rename file"),
                    new OptimizerAction(Util.NULL_ACTION, "Give up")
                };
                resultList.Add(new Result(ResultType.Optimize, "Your file isn't optimized.", filePath, actions));
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
