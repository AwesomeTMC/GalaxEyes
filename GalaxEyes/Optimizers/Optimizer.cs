using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GalaxEyes.Optimizers
{
    public static class OptimizerList
    {
        public static List<Optimizer> Items = new()
        {
            new ExampleOptimizer(),
        };
    }

    public enum ResultType
    {
        Optimize,
        Warn,
        Error
    }

    public class OptimizerAction(Action callback, String callbackName)
    {
        public Action Callback = callback;
        public string CallbackName = callbackName;
    }

    public class Result(ResultType type, string message, string affectedFile, List<OptimizerAction>? callbacks = null)
    {
        public List<OptimizerAction> Callbacks { get; set; } = callbacks ?? new();
        public ResultType Type { get; set; } = type;
        public string Message { get; set; } = message;
        public string AffectedFile { get; set; } = affectedFile;
    }
    public abstract class Optimizer
    {
        public String OptimizerName { get; set; } = "";
        public Boolean IsActive { get; set; } = true;
        public Optimizer(String name) { OptimizerName = name; }

        public abstract List<Result> Check(String file);
    }
}
