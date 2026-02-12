using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GalaxEyes.Inspectors
{
    public static class AllInspectors
    {
        public static List<Inspector> Items = new()
        {
#if DEBUG
            new ExampleOptimizer(),
#endif
            new VanillaFileOptimizer(),
            new AudioTableChecker(),
            new ASTOptimizer(),
            new Yaz0Optimizer(),
            new KCLOptimizer(),
            new CleanupOptimizer(),
        };
    }

    public enum ResultType
    {
        Optimize,
        Warn,
        Error
    }

    public class InspectorAction(Func<List<Result>> callback, String callbackName)
    {
        public Func<List<Result>> Callback = callback;
        public String CallbackName = callbackName;
        public override String ToString() => this.CallbackName;
    }

    public class Result(ResultType type, string affectedFile, string groupMessage, string inspectorName, List<InspectorAction>? callbacks = null, string resultSpecificMessage = "")
    {
        public List<InspectorAction> Callbacks { get; set; } = callbacks ?? new();
        public ResultType Type { get; set; } = type;
        public string Message { get; set; } = resultSpecificMessage;
        public string AffectedFile { get; set; } = affectedFile;
        public string GroupMessage { get; set; } = groupMessage;
        public string InspectorName { get; set; } = inspectorName;
    }
    public abstract class Inspector(String name)
    {
        /// <summary>
        /// The name of the inspector. This will be displayed to the user.
        /// </summary>
        public String InspectorName { get; set; } = name;
        /// <summary>
        /// Whether or not the inspector is active.
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
        /// Associated settings for the inspector.
        /// See <see cref="ExampleOptimizer"/> for how to override this.
        /// If set to null, the Settings button will not appear.
        /// </summary>
        public abstract IHaveSettings? Settings { get; }
        /// <summary>
        /// Runs after every <see cref="InspectorAction"/> is called. It is not necessarily after a success.
        /// </summary>
        /// <returns></returns>
        public virtual List<Result> RunAfter()
        {
            return new();
        }
        /// <summary>
        /// Checks if the current inspector should run <see cref="Check(string)"/> on the <paramref name="filePath"/> given.
        /// </summary>
        /// <param name="filePath">The absolute path of the file</param>
        /// <returns></returns>
        public virtual bool DoCheck(string filePath)
        {
            return IsActive;
        }
        /// <summary>
        /// Used by UI to check if this inspector's settings are null or not.
        /// </summary>
        public bool HasSettings { get
            {
                return Settings != null;
            } 
        }
        /// <summary>
        /// Checks if the current inspector has any invalid/unset settings. Independent of any specific file, and run once before <see cref="DoCheck(string)"/>.
        /// </summary>
        /// <returns>A list of any error messages. Returns an empty list if there are no errors.</returns>
        public virtual List<Result> SettingsCheck()
        {
            return new();
        }
    }
}
