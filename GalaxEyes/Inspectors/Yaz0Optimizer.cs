using CommunityToolkit.Mvvm.ComponentModel;
using Hack.io.YAZ0;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;

namespace GalaxEyes.Inspectors
{
    public partial class Yaz0Settings : InspectorSettings<Yaz0Settings>
    {
        [JsonIgnore] public override string FileName => "yaz0_settings.json";

        public uint Strength { get => GetField(0x1000u); set => SetField(value); }
    }
    public class Yaz0Optimizer : Inspector
    {
        public Yaz0Optimizer() : base("Yaz0 Optimizer")
        {
        }
        public override Yaz0Settings Settings { get; } = Yaz0Settings.Load();

        public override List<Result> Check(String filePath)
        {
            List<Result> resultList = new List<Result>();

            var file = File.OpenRead(filePath);
            
            if (!YAZ0.Check(file))
            {
                List<InspectorAction> actions = new() {
                    new InspectorAction(() => { return Compress(filePath, null); }, "Compress file")
                };
                Util.AddOptimize(ref resultList, filePath, "Uncompressed file(s) detected.", InspectorName, actions);
            }
            file.Close();
            return resultList;
        }

        public List<Result> Compress(String filePath, uint? strength)
        {
            List<Result> results = new();
            var thisFunc = () => { return Compress(filePath, strength); };
            strength = strength ?? Settings.Strength;

            var oldData = new MemoryStream(File.ReadAllBytes(filePath));
            var oldSize = oldData.Length;
            
            var file = Util.TryLoadArchive(ref results, filePath, InspectorName, thisFunc);
            if (file == null)
                return results;
            Util.TrySaveArchive(ref results, filePath, InspectorName, file, thisFunc, Settings.Strength);

            var newFile = File.OpenRead(filePath);
            var newSize = newFile.Length;
            newFile.Close();

            if (oldSize < newSize)
            {
                var hash = Convert.ToHexString(SHA256.HashData(oldData));
                var entry = new IgnoreEntry() { Hash = hash };
                entry.Inspectors.Add(InspectorName);
                Util.AddIgnoreEntry(entry);
                Write(filePath, oldData.ToArray());
            }
            return results;
        }

        public List<Result> Write(string filePath, byte[] data)
        {
            Util.WriteAllBytesSafe(filePath, data);
            return new();
        }

        public override bool DoCheck(string filePath)
        {
            return base.DoCheck(filePath) && filePath.EndsWith(".arc");
        }

        public override List<Result> SettingsCheck()
        {
            List<Result> results = new();
            if (Settings.Strength > 0x1000 || Settings.Strength < 0)
            {
                Util.AddError(ref results, "*", "YAZ0 Strength not in valid range (0 through 4096).", InspectorName, Util.NULL_ACTION);
            }
            return results;
        }
    }
}
