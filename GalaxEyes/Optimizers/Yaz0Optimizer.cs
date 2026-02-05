using CommunityToolkit.Mvvm.ComponentModel;
using Hack.io.YAZ0;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;

namespace GalaxEyes.Optimizers
{
    public partial class Yaz0Settings : FileSettings<Yaz0Settings>
    {
        [JsonIgnore] public override string FileName => "yaz0_settings.json";

        [ObservableProperty] private uint _strength = 0x1000;
    }
    public class Yaz0Optimizer : Optimizer
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
                List<OptimizerAction> actions = new() {
                    new OptimizerAction(() => { return Encode(filePath, null); }, "Compress file"),
                    new OptimizerAction(Util.NULL_ACTION, "Ignore this once")
                };
                resultList.Add(new Result(ResultType.Optimize, filePath, "Uncompressed file(s) detected.", OptimizerName, actions));
            }
            file.Close();
            return resultList;
        }

        public List<Result> Encode(String filePath, uint? strength)
        {
            List<Result> results = new();
            var thisFunc = () => { return Encode(filePath, strength); };
            strength = strength ?? Settings.Strength;

            var oldData = File.ReadAllBytes(filePath);
            var oldSize = oldData.Length;
            
            var file = Util.TryLoadArchive(ref results, filePath, OptimizerName, thisFunc);
            if (file == null)
                return results;
            Util.TrySaveArchive(ref results, filePath, OptimizerName, file, thisFunc, Settings.Strength);

            var newFile = File.OpenRead(filePath);
            var newSize = newFile.Length;
            newFile.Close();

            if (oldSize < newSize)
            {
                List<OptimizerAction> actions = new() {
                    new OptimizerAction(() => { return Write(filePath, oldData); }, "Stay with old file"),
                    new OptimizerAction(Util.NULL_ACTION, "Stay with new file")
                };
                results.Add(new Result(ResultType.Warn, filePath, "Old archive size is smaller than new size.", OptimizerName, actions));
            }
            return results;
        }

        public List<Result> Write(string filePath, byte[] data)
        {
            File.WriteAllBytes(filePath, data);
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
                Util.AddError(ref results, "*", "YAZ0 Strength not in valid range (0 through 4096).", OptimizerName, null);
            }
            return results;
        }
    }
}
