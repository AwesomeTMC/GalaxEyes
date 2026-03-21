using Avalonia.Media.Imaging;
using Binary_Stream;
using GalaxEyes.Inspectors;
using Hack.io.Class;
using Hack.io.RARC;
using Hack.io.Utility;
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GalaxEyes;

public interface IArchiveNode
{
    public string Name { get; set; }
    public string? Extension { get; }
    public string Path { get; }
    public byte[]? FileData { get; set; }
}

public interface IArchive
{
    public Endian Endian { get; set; }
    public byte[] ToBytes();
    public List<IArchiveNode> GetArchiveNodes(string regex);
    public IArchiveNode? GetArchiveNodeByPath(string filePath)
    {
        var matches = GetArchiveNodes("*");
        foreach (var match in matches)
        {
            if (filePath.StartsWith("/"))
                filePath = filePath.Replace("/", "");
            if (match.Path == filePath)
            {
                return match;
            }
        }
        return null;
    }
    public IArchiveNode? GetArchiveNodeByName(string fileName)
    {
        var matches = GetArchiveNodes("*");
        foreach (var match in matches)
        {
            if (match.Name == fileName)
            {
                return match;
            }
        }
        return null;
    }
}

public class HackIORARCFile(ArchiveFile file) : IArchiveNode
{

    public ArchiveFile ArchiveFile = file;
    public string Path
    {
        get
        {
            if (ArchiveFile.Parent == null)
                return ArchiveFile.Name;
            return ArchiveFile.FullPath;

        }
    }

    public string Name { get => ArchiveFile.Name; set => ArchiveFile.Name = value; }

    public string? Extension => ArchiveFile.Extension;

    public byte[]? FileData { get => ArchiveFile.FileData; set => ArchiveFile.FileData = value; }
}

public class HackIORARC : RARC, IArchive
{
    public Endian Endian { get; set; }

    public HackIORARC(byte[] data)
    {
        BinaryStream stream = new BinaryStream(data);
        stream.Endian = Endian.Big;
        string magic = stream.ReadString(4);
        if (magic == "RARC")
            Endian = Endian.Big;
        else if (magic == "CRAR")
            Endian = Endian.Little;
        else
            throw new InvalidDataException("File is not a RARC! Expected CRAR or RARC, got " + magic);
        stream.Endian = Endian;
        stream.Position = 0;
        Read(new UtilityStream(stream, (StreamEndian)stream.Endian));
    }

    public byte[] ToBytes()
    {
        BinaryStream outStrm = new BinaryStream();
        outStrm.Endian = Endian;
        Write(new UtilityStream(outStrm, (StreamEndian)outStrm.Endian));
        return outStrm.ToArray();
    }

    public List<IArchiveNode> GetArchiveNodes(string regex)
    {
        List<IArchiveNode> nodes = new();
        foreach (string path in FindItems(regex))
        {
            object? file = this[path];
            if (file is not ArchiveFile node)
                continue;
            nodes.Add(new HackIORARCFile(node));
        }
        return nodes;
    }
}

public class JKRLibRARCFile(JKRFileNode file) : IArchiveNode
{
    public JKRFileNode Node { get; set; } = file;
    public string Name { get => Node.Name; set => Node.Name = value; }

    public string? Extension { 
        get
        {
            string[] parts = Name.Split('.');
            if (parts.Length == 1)
                return "";
            return "." + parts[^1].ToLower();
        } 
    }

    public string Path => Node.ToString();

    public byte[]? FileData { get => Node.Data; set => Node.SetFileData(value ?? new byte[0]); }
}

public class JKRLibRARC : JKRArchive, IArchive
{
    public JKRLibRARC(ReadOnlySpan<byte> span) : base(span)
    {
    }

    Endian IArchive.Endian { get => Endian; set => Endian = value; }

    public List<IArchiveNode> GetArchiveNodes(string regex)
    {
        List<IArchiveNode> nodes = new();
        foreach (var node in FileNodes)
        {
            if (regex == "*" || Regex.IsMatch(node.Name, regex))
                nodes.Add(new JKRLibRARCFile(node));
        }
        return nodes;
    }

    public byte[] ToBytes()
    {
        BinaryStream writer = new(Endian);
        Write(writer);
        return writer.ToArray();
    }
}

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
            if (!Directory.Exists(System.IO.Path.Combine(directory, vanillaDirectory)))
                return false;
        }
        return true;
    }

    public static IArchive? TryLoadArchive(ref List<Result> results, string arcPath, string inspectorName, Func<List<Result>> retryCallback)
    {
        return TryLoadArchive(ref results, arcPath, inspectorName, Util.FromResult(retryCallback));
    }

    public static IArchive? TryLoadArchive(ref List<Result> results, string arcPath, string inspectorName, Func<Task<List<Result>>> retryCallback)
    {
        try
        {
            var data = Binary_Stream.Yaz0.Decompress(File.ReadAllBytes(arcPath));
            if (MainSettings.Instance.ArchiveHandler == ArchiveHandler.JKRLib)
                return new JKRLibRARC(data);
            else if (MainSettings.Instance.ArchiveHandler == ArchiveHandler.HackIO)
                return new HackIORARC(data);
            else
                throw new NotImplementedException("Archive handler " + MainSettings.Instance.ArchiveHandler + " not implemented!");
        }
        catch (BadImageFormatException e)
        {
            Util.AddError(ref results, arcPath, "Bad magic for RARC", inspectorName, retryCallback, e.ToString());
            return null;
        }
    }

    public static bool TrySaveArchive(ref List<Result> results, string arcPath, string inspectorName, IArchive arc, Func<List<Result>> retryCallback, uint strength = 0x1000)
    {
        return TrySaveArchive(ref results, arcPath, inspectorName, arc, Util.FromResult(retryCallback), strength);
    }

    public static bool TrySaveArchive(ref List<Result> results, string arcPath, string inspectorName, IArchive arc, Func<Task<List<Result>>> retryCallback, uint strength = 0x1000)
    {
        try
        {
            var data = arc.ToBytes();
            var yaz0_data = GalaxEyes.Yaz0.Compress(data, 0x1000, (ushort)strength);
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

    public static BinaryStream? TryLoadFileFromArcByPath(IArchive arc, string filePath)
    {
        var file = TryLoadFileNodeFromArcByPath(arc, filePath);
        if (file == null || file.FileData == null)
            return null;
        var strm = new BinaryStream(file.FileData);
        return strm;
    }

    public static IArchiveNode? TryLoadFileNodeFromArcByPath(IArchive arc, string filePath)
    {
        return arc.GetArchiveNodeByPath(filePath);
    }

    public static BinaryStream? TryLoadFileFromArcByName(IArchive arc, string filePath)
    {
        var file = TryLoadFileNodeFromArcByName(arc, filePath);
        if (file == null || file.FileData == null)
            return null;
        var strm = new BinaryStream(file.FileData);
        return strm;
    }

    public static IArchiveNode? TryLoadFileNodeFromArcByName(IArchive arc, string fileName)
    {
        return arc.GetArchiveNodeByName(fileName);
    }

    public static List<IArchiveNode> SearchArcFiles(IArchive arc, string regex)
    {
        return arc.GetArchiveNodes(regex);
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