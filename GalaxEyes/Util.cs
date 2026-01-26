using GalaxEyes.Optimizers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GalaxEyes;

public static class Util
{
    public static Func<List<Result>> NULL_ACTION = () => { return new(); };
    public static void AddException(ref List<Result> results, Exception e, string affectedFile, string optimizerName, Func<List<Result>> retryCallback)
    {
        AddError(ref results, affectedFile, e.GetType().ToString(), optimizerName, retryCallback, e.ToString());
    }
    public static void AddError(ref List<Result> results, string affectedFile, string groupMessage, string optimizerName, Func<List<Result>> retryCallback, string resultSpecificMessage="")
    {
        List<OptimizerAction> standardActions = new()
        {
            new OptimizerAction(retryCallback, "Retry"),
            new OptimizerAction(NULL_ACTION, "Ignore this once"),
            // TODO: Add more ignore options
        };

        results.Add(new Result(ResultType.Error, affectedFile, groupMessage, optimizerName, standardActions, resultSpecificMessage));
    }

    public static void AddResult(ObservableCollection<OptimizerResultGroup> groups, Result result, OptimizerAction? selectedAction)
    {
        var resultRow = new OptimizerResultRow(result.Message, result.AffectedFile, result.OptimizerName, result.Callbacks, selectedAction);
        foreach (OptimizerResultGroup group in groups)
        {
            if (group.GroupMessage == result.GroupMessage)
            {
                group.OptimizerResults.Add(resultRow);
                if (!group.OptimizerNames.Contains(result.OptimizerName))
                    group.OptimizerNames.Add(result.OptimizerName);
                return;
            }
        }

        List<OptimizerResultRow> rows = new() { resultRow };
        List<string> names = new() { result.OptimizerName };
        var newGroup = new OptimizerResultGroup(rows, result.GroupMessage, result.Callbacks, selectedAction, names);
        groups.Add(newGroup);
    }

    public static void RemoveEmptyFolders(string startLocation)
    {
        foreach (var directory in Directory.GetDirectories(startLocation))
        {
            RemoveEmptyFolders(directory);
            if (Directory.GetFiles(directory).Length == 0
                && Directory.GetDirectories(directory).Length == 0)
            {
                Debug.WriteLine("Delete directory " + directory);
                Directory.Delete(directory, false);
            }
        }
    }

    public static bool IsValidVanillaDirectory(string directory)
    {
        string[] VanillaDirectories =
        {
            "StageData",
            "LayoutData",
            "ObjectData"
        };
        if (!Directory.Exists(directory))
        {
            return false;
        }
        foreach (string vanillaDirectory in VanillaDirectories)
        {
            if (!Directory.Exists(Path.Combine(directory, vanillaDirectory)))
                return false;
        }
        return true;
    }
}