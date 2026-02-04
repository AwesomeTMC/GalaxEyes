using Binary_Stream;
using GalaxEyes.Optimizers;
using Hack.io.Utility;
using Hack.io.YAZ0;
using jkr_lib;
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
        if (directory == "" || !Directory.Exists(directory))
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

    public static JKRArchive? TryLoadArchive(ref List<Result> results, string arcPath, string optimizerName, Func<List<Result>> retryCallback)
    {
        List<(Func<Stream, bool> CheckFunc, Func<byte[], byte[]> DecodeFunction)> DecompFuncs =
        [
            (YAZ0.Check, YAZ0.Decompress)
        ];
        try
        {
            var data = FileUtil.ReadWithDecompression(arcPath, [.. DecompFuncs]);
            if (data == null)
            {
                data = File.ReadAllBytes(arcPath);
            }
            return new JKRArchive(data);
        }
        catch (BadImageFormatException e)
        {
            Util.AddError(ref results, arcPath, "Bad magic for RARC", optimizerName, retryCallback, e.ToString());
            return null;
        }
    }

    public static bool TrySaveArchive(ref List<Result> results, string arcPath, string optimizerName, JKRArchive arc, Func<List<Result>> retryCallback, uint strength = 0x1000)
    {
        try
        {
            var data = arc.ToBytes();
            StreamUtil.PushEndian(arc.Endian == Endian.Little ? false : true);
            var yaz0_data = YAZ0.Compress_Strong(data, null, strength);
            File.WriteAllBytes(arcPath, yaz0_data);
            return true;
        }
        catch (Exception e)
        {
            Util.AddError(ref results, arcPath, "Failed to save archive", optimizerName, retryCallback, e.ToString());
            return false;
        }
    }

    public static string CamelCaseSpace(string input)
    {
        string result = string.Concat(
            input.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? " " + c : c.ToString()
            )
        );
        return result;
    }
}