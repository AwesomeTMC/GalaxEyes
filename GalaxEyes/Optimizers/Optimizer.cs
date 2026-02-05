using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GalaxEyes.Optimizers
{
    public static class AllOptimizers
    {
        public static List<Optimizer> Items = new()
        {
            new ExampleOptimizer(),
            new VanillaFileOptimizer(),
            new AudioTableChecker(),
            new ASTOptimizer(),
            new Yaz0Optimizer(),
        };
    }

    public enum ResultType
    {
        Optimize,
        Warn,
        Error
    }

    public class OptimizerAction(Func<List<Result>> callback, String callbackName)
    {
        public Func<List<Result>> Callback = callback;
        public String CallbackName = callbackName;
        public override String ToString() => this.CallbackName;
    }

    public class Result(ResultType type, string affectedFile, string groupMessage, string optimizerName, List<OptimizerAction>? callbacks = null, string resultSpecificMessage = "")
    {
        public List<OptimizerAction> Callbacks { get; set; } = callbacks ?? new();
        public ResultType Type { get; set; } = type;
        public string Message { get; set; } = resultSpecificMessage;
        public string AffectedFile { get; set; } = affectedFile;
        public string GroupMessage { get; set; } = groupMessage;
        public string OptimizerName { get; set; } = optimizerName;
    }
    public abstract class Optimizer(String name)
    {
        /// <summary>
        /// The name of the optimizer. This will be displayed to the user.
        /// </summary>
        public String OptimizerName { get; set; } = name;
        /// <summary>
        /// Whether or not the optimizer is active.
        /// Part of <see cref="DoCheck(string)"/>.
        /// </summary>
        public Boolean IsActive { get; set; } = true;
        /// <summary>
        /// Runs a check on <paramref name="file"/>. 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public abstract List<Result> Check(String file);
        /// <summary>
        /// Associated settings for the optimizer.
        /// See <see cref="ExampleOptimizer"/> for how to override this.
        /// If set to null, the Settings button will not appear.
        /// </summary>
        public abstract IHaveSettings? Settings { get; }
        /// <summary>
        /// Runs after every <see cref="OptimizerAction"/> is called. It is not necessarily after a success.
        /// </summary>
        /// <returns></returns>
        public virtual List<Result> RunAfter()
        {
            return new();
        }
        /// <summary>
        /// Checks if the current optimizer should run <see cref="Check(string)"/> on the <paramref name="filePath"/> given.
        /// </summary>
        /// <param name="filePath">The absolute path of the file</param>
        /// <returns></returns>
        public virtual bool DoCheck(string filePath)
        {
            return IsActive;
        }
        /// <summary>
        /// Used by UI to check if this optimizer's settings are null or not.
        /// </summary>
        public bool HasSettings { get
            {
                return Settings != null;
            } 
        }
        /// <summary>
        /// Checks if the current optimizer has any invalid/unset settings. Independent of any specific file, and run once before <see cref="DoCheck(string)"/>.
        /// </summary>
        /// <returns>A list of any error messages. Returns an empty list if there are no errors.</returns>
        public virtual List<Result> SettingsCheck()
        {
            return new();
        }
    }
}
