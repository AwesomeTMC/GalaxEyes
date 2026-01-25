using GalaxEyes.Optimizers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GalaxEyes;

public static class Util
{
    public static Func<List<Result>> NULL_ACTION = () => { return new(); };
    public static void AddError(ref List<Result> results, string message, string affectedFile, Func<List<Result>> retryCallback)
    {
        List<OptimizerAction> standardActions = new()
        {
            new OptimizerAction(retryCallback, "Retry"),
            new OptimizerAction(NULL_ACTION, "Ignore this once"),
            // TODO: Add more ignore options
        };

        results.Add(new Result(ResultType.Error, message, affectedFile, standardActions));
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