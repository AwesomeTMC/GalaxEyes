using Binary_Stream;
using CommunityToolkit.Mvvm.ComponentModel;
using Hack.io.BCSV;
using Hack.io.Class;
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

namespace GalaxEyes.Inspectors
{
    public partial class KCLSettings : InspectorSettings<KCLSettings>
    {
        [JsonIgnore] public override string FileName => "kcl_settings.json";

        [ObservableProperty] [property: Name("Minimum duplicates to show on scan")] private uint _minimumDuplicates = 16;
        [ObservableProperty] private uint _maxTrianglesPerCube = 25;
        [ObservableProperty] private uint _minCubeSize = 8;
    }
    public class KCLOptimizer : Inspector
    {
        public KCLOptimizer() : base("KCL/PA Optimizer")
        {
        }
        public override KCLSettings Settings { get; } = KCLSettings.Load();

        public override List<Result> Check(String filePath)
        {
            List<Result> resultList = new List<Result>();

            var arc = Util.TryLoadArchive(ref resultList, filePath, InspectorName, () => { return Check(filePath); });
            if (arc == null)
                return resultList;
            

            var objectName = Path.GetFileName(filePath).Replace(".arc", "");
            bool hasKcl = Util.TryLoadFileNodeFromArcByName(arc, objectName + ".kcl") != null;
            bool hasPa = Util.TryLoadFileNodeFromArcByName(arc, objectName + ".pa") != null;
            if (!hasKcl || !hasPa)
                return resultList;

            int dupeCount = DuplicateCount(arc, objectName);
            if (dupeCount > Settings.MinimumDuplicates)
            {
                
                List<InspectorAction> actions = new()
                {
                    new InspectorAction(() => { return RemoveDuplicates(filePath);  }, "Remove duplicates"),
                    new InspectorAction(Util.NULL_ACTION, "Ignore this once")
                };
                resultList.Add(new Result(ResultType.Optimize, filePath, "File contains at least " + Settings.MinimumDuplicates + " duplicate materials", InspectorName, actions, dupeCount.ToString()));
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

        private KCL InKcl(IArchive arc, string kclPath)
        {
            bool endian = arc.Endian == Binary_Stream.Endian.Big;
            var strm = Util.TryLoadFileFromArcByName(arc, kclPath)?.Wrap();
            if (strm == null)
                throw new FileNotFoundException("KCL not found in arc!");
            strm.Endian = (StreamEndian)arc.Endian;
            var kcl = new KCL();
            kcl.Read(strm);
            return kcl;
        }

        private void OutKcl(IArchive arc, KCL kcl, string kclName)
        {
            bool endian = arc.Endian == Binary_Stream.Endian.Big;
            var kclFile = Util.TryLoadFileNodeFromArcByName(arc, kclName);
            if (kclFile == null)
            {
                throw new FileNotFoundException("KCL not found in archive! " + kclName);
            }
            var data = new MemoryStream().Wrap();
            data.Endian = (StreamEndian)arc.Endian;
            kcl.Write(data);
            kclFile.FileData = data.ToArray();
        }

        private BCSV InPa(IArchive arc, string paName)
        {
            bool endian = arc.Endian == Binary_Stream.Endian.Big;
            var strm = Util.TryLoadFileFromArcByName(arc, paName)?.Wrap();
            if (strm == null)
                throw new FileNotFoundException("PA not found in arc!");
            strm.Endian = (StreamEndian)arc.Endian;
            var bcsv = new BCSV();
            bcsv.Load(strm);
            return bcsv;
        }

        private void OutPa(IArchive arc, BCSV pa, string paName)
        {
            bool endian = arc.Endian == Binary_Stream.Endian.Big;
            var paFile = Util.TryLoadFileNodeFromArcByName(arc, paName);
            if (paFile == null)
            {
                throw new FileNotFoundException("PA not found in archive! " + paName);
            }
            var data = new MemoryStream().Wrap();
            data.Endian = (StreamEndian)arc.Endian;
            pa.Save(data);
            paFile.FileData = data.ToArray();
        }

        private int DuplicateCount(IArchive arc, string objectName)
        {
            // if checking with this is too slow, maybe add an option to turn off counting
            var pa = InPa(arc, objectName + ".pa");
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
            return dupeCount;
        }

        private List<Result> RemoveDuplicates(string arcPath)
        {
            List<Result> resultList = new();
            var thisFunc = () => { return RemoveDuplicates(arcPath); };
            var arc = Util.TryLoadArchive(ref resultList, arcPath, InspectorName, thisFunc);
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
            Util.TrySaveArchive(ref resultList, arcPath, InspectorName, arc, thisFunc);
            return resultList;
        }

        public override List<Result> SettingsCheck()
        {
            List<Result> results = new();
            if (Settings.MinimumDuplicates == 0)
            {
                Util.AddError(ref results,
                    "*",
                    "KCL Settings improperly set!",
                    InspectorName, Util.NULL_ACTION,
                    "Minimum duplicates must be at least 1");
            }
            return results;
        }
    }
}
