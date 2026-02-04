using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GalaxEyes.Optimizers
{
    public partial class VanillaFileSettings : FileSettings<VanillaFileSettings>
    {
        [JsonIgnore] public override string FileName => "vanilla_optimizer_settings.json";

        [ObservableProperty] [property: Folder("Please select a vanilla directory. It should not have any modified files.")] private string _vanillaDirectory = "";
    }
    public class VanillaFileOptimizer : Optimizer
    {
        public VanillaFileOptimizer() : base("Vanilla File Optimizer")
        {
        }
        public override VanillaFileSettings Settings { get; } = VanillaFileSettings.Load();

        public override List<Result> Check(String filePath)
        {
            List<Result> resultList = new List<Result>();
            String fileName = Path.GetFileName(filePath);

            if (!Util.IsValidVanillaDirectory(Settings.VanillaDirectory))
            {
                string error = "Valid vanilla directory not set.";
                Util.AddError(ref resultList, filePath, error, OptimizerName, () => { return Check(filePath); });
                return resultList;
            }

            string relativePath = Path.GetRelativePath(MainSettings.Instance.ModDirectory, filePath);
            string vanillaPath = Path.Combine(Settings.VanillaDirectory, relativePath);


            if (File.Exists(vanillaPath))
            {
                var vanillaFile = File.OpenRead(vanillaPath);
                var modFile = File.OpenRead(filePath);
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hash1 = sha256.ComputeHash(vanillaFile);
                var hash2 = sha256.ComputeHash(modFile);

                vanillaFile.Close();
                modFile.Close();

                if (hash1.SequenceEqual(hash2))
                {
                    List<OptimizerAction> actions = new() {
                        new OptimizerAction(() => { return RemoveFile(filePath); }, "Remove file"),
                        new OptimizerAction(Util.NULL_ACTION, "Ignore this once")
                    };
                    resultList.Add(new Result(ResultType.Optimize, filePath, "Vanilla file(s) detected.", OptimizerName, actions));
                }
            }

            return resultList;
        }


        public List<Result> RemoveFile(String filePath)
        {
            File.Delete(filePath);
            return new();
        }

        public override List<Result> RunAfter()
        {
            Util.RemoveEmptyFolders(MainSettings.Instance.ModDirectory);
            return new();
        }
    }
}
