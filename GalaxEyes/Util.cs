using Avalonia.Media.Imaging;
using Binary_Stream;
using GalaxEyes.Inspectors;
using Hack.io.KCL;
using Hack.io.Utility;
using Hack.io.YAZ0;
using jkr_lib;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
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
    public static Func<Task<List<Result>>> NULL_ACTION = async () => { return new(); };

    public static Func<Task<List<Result>>> FromResult(Func<List<Result>> func)
    {
        return () => { return Task.FromResult(func()); };
    }

    public static void AddException(ref List<Result> results, Exception e, string affectedFile, string inspectorName, Func<Task<List<Result>>> retryCallback)
    {
        AddError(ref results, affectedFile, e.GetType().ToString(), inspectorName, retryCallback, e.ToString());
    }

    public static void AddException(ref List<Result> results, Exception e, string affectedFile, string inspectorName, Func<List<Result>> retryCallback)
    {
        AddError(ref results, affectedFile, e.GetType().ToString(), inspectorName, retryCallback, e.ToString());
    }

    public static void AddError(ref List<Result> results, string affectedFile, string groupMessage, string inspectorName, Func<Task<List<Result>>>? retryCallback, string resultSpecificMessage="")
    {

        List<InspectorAction> standardActions = new();
        if (retryCallback != null)
            standardActions.Add(new InspectorAction(retryCallback, "Retry"));
        standardActions.Add(new InspectorAction(NULL_ACTION, "Ignore this once"));

        results.Add(new Result(ResultType.Error, affectedFile, groupMessage, inspectorName, standardActions, resultSpecificMessage));
    }

    public static void AddError(ref List<Result> results, string affectedFile, string groupMessage, string inspectorName, Func<List<Result>>? retryCallback, string resultSpecificMessage = "")
    {

        List<InspectorAction> standardActions = new();
        if (retryCallback != null)
            standardActions.Add(new InspectorAction(retryCallback, "Retry"));
        standardActions.Add(new InspectorAction(NULL_ACTION, "Ignore this once"));

        results.Add(new Result(ResultType.Error, affectedFile, groupMessage, inspectorName, standardActions, resultSpecificMessage));
    }

    public static void AddResult(ObservableCollection<InspectorResultGroup> groups, Result result, InspectorAction? selectedAction)
    {
        var resultRow = new InspectorResultRow(result.Message, result.AffectedFile, result.InspectorName, result.Callbacks, selectedAction);
        foreach (InspectorResultGroup group in groups)
        {
            if (group.GroupMessage == result.GroupMessage)
            {
                group.InspectorResults.Add(resultRow);
                if (!group.InspectorNames.Contains(result.InspectorName))
                    group.InspectorNames.Add(result.InspectorName);
                return;
            }
        }

        List<InspectorResultRow> rows = new() { resultRow };
        List<string> names = new() { result.InspectorName };
        var newGroup = new InspectorResultGroup(rows, result.GroupMessage, result.Callbacks, selectedAction, names);
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
                Debug.WriteLine("Deleting empty directory " + directory);
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

    public static JKRArchive? TryLoadArchive(ref List<Result> results, string arcPath, string inspectorName, Func<List<Result>> retryCallback)
    {
        return TryLoadArchive(ref results, arcPath, inspectorName, Util.FromResult(retryCallback));
    }

    public static JKRArchive? TryLoadArchive(ref List<Result> results, string arcPath, string inspectorName, Func<Task<List<Result>>> retryCallback)
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
            Util.AddError(ref results, arcPath, "Bad magic for RARC", inspectorName, retryCallback, e.ToString());
            return null;
        }
    }

    public static bool TrySaveArchive(ref List<Result> results, string arcPath, string inspectorName, JKRArchive arc, Func<List<Result>> retryCallback, uint strength = 0x1000)
    {
        return TrySaveArchive(ref results, arcPath, inspectorName, arc, Util.FromResult(retryCallback), strength);
    }

    public static bool TrySaveArchive(ref List<Result> results, string arcPath, string inspectorName, JKRArchive arc, Func<Task<List<Result>>> retryCallback, uint strength = 0x1000)
    {
        try
        {
            var data = arc.ToBytes();
            StreamUtil.PushEndian(arc.Endian == Endian.Little ? false : true);
            var yaz0_data = YAZ0.Compress_Strong(data, null, strength);
            WriteAllBytesSafe(arcPath, yaz0_data);
            return true;
        }
        catch (Exception e)
        {
            Util.AddError(ref results, arcPath, "Failed to save archive", inspectorName, retryCallback, e.ToString());
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

    public static BinaryStream? TryLoadFileFromArc(JKRArchive arc, string filePath)
    {
        var iter = arc.FindFile(filePath);
        if (iter.Count() == 0)
            return null;
        var file = iter.First<JKRFileNode>();
        var strm = new BinaryStream(file.Data);
        return strm;
    }

    public static JKRFileNode? TryLoadFileNodeFromArc(JKRArchive arc, string filePath)
    {
        var iter = arc.FindFile(filePath);
        if (iter.Count() == 0)
            return null;
        var file = iter.First<JKRFileNode>();
        return file;
    }

    /// <summary>
    /// Similar to <see cref="File.WriteAllBytes(string, byte[])"/>, but uses a tmp path to ensure no data is lost.
    /// </summary>
    /// <param name="path">The path to write to.</param>
    /// <param name="bytes">The data to write.</param>
    public static void WriteAllBytesSafe(string path, byte[] bytes)
    {
        var tmpPath = path + ".tmp";
        File.WriteAllBytes(tmpPath, bytes);
        File.Move(tmpPath, path, true);
    }

    public static Bitmap ToAvaloniaBitmap(Image<Rgba32> image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        ms.Position = 0;
        return new Bitmap(ms);
    }
}