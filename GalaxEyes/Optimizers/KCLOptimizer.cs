using Binary_Stream;
using CommunityToolkit.Mvvm.ComponentModel;
using Hack.io.BCSV;
using Hack.io.KCL;
using Hack.io.Utility;
using jkr_lib;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace GalaxEyes.Optimizers
{
    public partial class KCLSettings : FileSettings<KCLSettings>
    {
        [JsonIgnore] public override string FileName => "kcl_settings.json";

        [ObservableProperty] [property: Name("Minimum duplicates to show on scan")] private uint _minimumDuplicates = 16;
        [ObservableProperty] private uint _maxTrianglesPerCube = 25;
        [ObservableProperty] private uint _minCubeSize = 8;
    }
    public class KCLOptimizer : Optimizer
    {
        public KCLOptimizer() : base("KCL/PA Optimizer")
        {
        }
        public override KCLSettings Settings { get; } = KCLSettings.Load();

        public override List<Result> Check(String filePath)
        {
            List<Result> resultList = new List<Result>();

            var arc = Util.TryLoadArchive(ref resultList, filePath, OptimizerName, () => { return Check(filePath); });
            if (arc == null)
                return resultList;
            

            var objectName = Path.GetFileName(filePath).Replace(".arc", "");
            //Debug.WriteLine(objectName);
            bool hasKcl = arc.FindFile(objectName + ".kcl").Count() > 0;
            bool hasPa = arc.FindFile(objectName + ".pa").Count() > 0;
            if (!hasKcl || !hasPa)
                return resultList;

            int dupeCount = DuplicateCount(arc, objectName);
            if (dupeCount > Settings.MinimumDuplicates)
            {
                
                List<OptimizerAction> actions = new()
                {
                    new OptimizerAction(() => { return RemoveDuplicates(filePath);  }, "Remove duplicates"),
                    new OptimizerAction(Util.NULL_ACTION, "Ignore this once")
                };
                resultList.Add(new Result(ResultType.Optimize, filePath, "File contains at least " + Settings.MinimumDuplicates + " duplicate materials", OptimizerName, actions, dupeCount.ToString()));
            }

            return resultList;
        }

        public override bool DoCheck(string filePath)
        {
            if (!base.DoCheck(filePath))
                return false;
            if (!filePath.EndsWith(".arc"))
                return false;
            if (Directory.GetParent(filePath)?.Name != "ObjectData") 
                return false;
            return true;
        }

        private KCL InKcl(JKRArchive arc, string kclPath)
        {
            bool endian = arc.Endian == Binary_Stream.Endian.Big;
            var strm = Util.TryLoadFileFromArc(arc, kclPath);
            if (strm == null)
                throw new FileNotFoundException("KCL not found in arc!");
            var kcl = new KCL();
            StreamUtil.PushEndian(endian);
            kcl.Read(strm);
            return kcl;
        }

        private void OutKcl(JKRArchive arc, KCL kcl, string kclPath)
        {
            bool endian = arc.Endian == Binary_Stream.Endian.Big;
            var kclFile = arc.FindFile(kclPath)?.First<JKRFileNode>();
            if (kclFile == null)
            {
                throw new FileNotFoundException("KCL not found in archive! " + kclPath);
            }
            var data = new MemoryStream();
            StreamUtil.PushEndian(endian);
            kcl.Write(data);
            kclFile.SetFileData(data.ToArray());
        }

        private BCSV InPa(JKRArchive arc, string paPath)
        {
            bool endian = arc.Endian == Binary_Stream.Endian.Big;
            var strm = Util.TryLoadFileFromArc(arc, paPath);
            if (strm == null)
                throw new FileNotFoundException("KCL not found in arc!");
            var bcsv = new BCSV();
            StreamUtil.PushEndian(endian);
            bcsv.Load(strm);
            return bcsv;
        }

        private void OutPa(JKRArchive arc, BCSV pa, string filePath)
        {
            bool endian = arc.Endian == Binary_Stream.Endian.Big;
            var paFile = arc.FindFile(filePath)?.First<JKRFileNode>();
            if (paFile == null)
            {
                throw new FileNotFoundException("PA not found in archive! " + filePath);
            }
            var data = new MemoryStream();
            StreamUtil.PushEndian(endian);
            pa.Save(data);
            paFile.SetFileData(data.ToArray());
        }

        private int DuplicateCount(JKRArchive arc, string objectName)
        {
            // if checking with this is too slow, maybe add an option to turn off counting
            var kcl = InKcl(arc, objectName + ".kcl");
            var pa = InPa(arc, objectName + ".pa");
            var obj = WavefrontObj.CreateWavefront(kcl);
            int dupeCount = 0;

            List<BCSV.Entry> uniqueEntries = new();
            for (int i = 0; i < pa.EntryCount; i++)
            {
                BCSV.Entry entry = pa[i];
                bool isDuplicate = false;
                for (int j = 0; j < uniqueEntries.Count; j++)
                {
                    var uniqueEntry = uniqueEntries[j];
                    if (uniqueEntry.Equals(entry))
                    {
                        dupeCount++;
                        isDuplicate = true;
                        break;
                    }
                }
                if (isDuplicate)
                    continue;

                
                uniqueEntries.Add(entry);
            }
            //Debug.WriteLine("Duplicate count " + dupeCount);
            return dupeCount;
        }

        private List<Result> RemoveDuplicates(string arcPath)
        {
            List<Result> resultList = new();
            var thisFunc = () => { return RemoveDuplicates(arcPath); };
            var arc = Util.TryLoadArchive(ref resultList, arcPath, OptimizerName, thisFunc);
            if (arc == null)
                return resultList;
            var objectName = Path.GetFileName(arcPath).Replace(".arc", "");
            var kcl = InKcl(arc, objectName + ".kcl");
            var pa = InPa(arc, objectName + ".pa");
            var obj = WavefrontObj.CreateWavefront(kcl);

            List<BCSV.Entry> uniqueEntries = new();
            for (int i = 0; i < pa.EntryCount; i++)
            {
                BCSV.Entry entry = pa[i];
                bool isDuplicate = false;
                for (int j = 0; j < uniqueEntries.Count; j++)
                {
                    var uniqueEntry = uniqueEntries[j];
                    if (uniqueEntry.Equals(entry))
                    {
                        isDuplicate = true;
                        obj.MergeGroup(uniqueEntries.Count, j);
                        break;
                    }
                }
                if (isDuplicate)
                    continue;

                uniqueEntries.Add(entry);
            }

            pa.Clear();
            pa.AddRange(uniqueEntries);
            
            var newKcl = new KCL(obj, pa, (int)Settings.MaxTrianglesPerCube, (int)Settings.MinCubeSize);
            
            OutKcl(arc, newKcl, objectName + ".kcl");
            OutPa(arc, pa, objectName + ".pa");
            Util.TrySaveArchive(ref resultList, arcPath, OptimizerName, arc, thisFunc);
            return resultList;
        }
    }
}
