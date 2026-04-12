using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;

namespace GalaxEyes.Inspectors
{
    public partial class ExampleSettings : InspectorSettings<ExampleSettings>
    {
        [JsonIgnore] public override string FileName => "example_settings.json";

        [Name("Cause error intentionally?")]
        public bool CauseError { get => GetField(false); set => SetField(value); }

        [Name("Cause independent error intentionally?")]
        public bool CauseIndependentError { get => GetField(false); set => SetField(value); }

        public int SleepAmount { get => GetField(0); set => SetField(value); }

        // You can add this to make your Inspector disabled by default.
        protected override void InitializeNew()
        {
            IsEnabled = false;
        }
    }
    public class ExampleOptimizer : Inspector
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
                    InspectorName, () => { return Check(filePath); },
                    "Set this to an empty string to not have an individual message for this result.");
            Thread.Sleep(Settings.SleepAmount);

            if (fileName.Contains("Unoptimized"))
            {
                // declare the actions the user can take
                List<InspectorAction> actions = new() {
                    new InspectorAction(() => { return OptimizeFile(filePath); }, "Rename file"),
                    new InspectorAction(Util.NULL_ACTION, "Give up")
                };
                resultList.Add(new Result(ResultType.Optimize, filePath, "You have unoptimized file(s).", InspectorName, actions));
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

        public override List<Result> SettingsCheck()
        {
            List<Result> results = new();
            if (Settings.CauseIndependentError)
            {
                Util.AddError(ref results,
                    "*",
                    "Example optimizer ran into an error",
                    InspectorName, Util.NULL_ACTION,
                    "Hi it's me, the independent error.");
            }
            return results;
        }
    }
}
