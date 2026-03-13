
using Avalonia.Controls.Documents;
using Binary_Stream;
using CommunityToolkit.Mvvm.ComponentModel;
using Hack.io.BCSV;
using Hack.io.Class;
using Hack.io.Utility;
using Hack.io.YAZ0;
using jkr_lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

namespace GalaxEyes.Inspectors
{
    public partial class AudioTableSettings : InspectorSettings<AudioTableSettings>
    {
        [JsonIgnore] public override string FileName => "audio_table_settings.json";
    }
    public class AudioTableChecker : Inspector
    {
        public static uint STAGE_NAME = 0xE4EC2289;
        public static uint SCENARIO_NO = 0xED08B591;
        public AudioTableChecker() : base("Audio Table Checker")
        {
        }
        public override AudioTableSettings Settings { get; } = AudioTableSettings.Load();

        public override List<Result> Check(string filePath)
        {
            List<Result> resultList = new List<Result>();
            IArchive? arch = Util.TryLoadArchive(ref resultList, filePath, InspectorName, () => { return Check(filePath); });
            if (arch == null)
                return resultList;

            var scenarioBgmInfo = Util.TryLoadFileNodeFromArcByName(arch, "ScenarioBgmInfo.bcsv");
            var stageBgmInfo = Util.TryLoadFileNodeFromArcByName(arch, "StageBgmInfo.bcsv");
            if (scenarioBgmInfo == null || stageBgmInfo == null || scenarioBgmInfo.FileData == null || stageBgmInfo.FileData == null)
            {
                Util.AddError(ref resultList, filePath, "StageBgmInfo or ScenarioBgmInfo data not found", InspectorName, () => { return Check(filePath); });
                return resultList;
            }


            List<string> scenarioBgmStages = CollectStages(scenarioBgmInfo.FileData, arch.Endian);
            List<string> stageBgmStages = CollectStages(stageBgmInfo.FileData, arch.Endian);
            foreach (string stageBgmStage in stageBgmStages)
            {
                if (!scenarioBgmStages.Contains(stageBgmStage))
                {
                    List<InspectorAction> actions = new()
                    {
                        new InspectorAction(Util.NULL_ACTION, "Ignore this once"),
                        new InspectorAction(() => {
                            var newEntry = new BCSV.Entry();
                            newEntry.Add(STAGE_NAME, stageBgmStage);
                            newEntry.Add(SCENARIO_NO, 0);
                            return AddEntry(filePath, "ScenarioBgmInfo.bcsv", newEntry); },
                            "Add stage to ScenarioBgmInfo")
                    };
                    resultList.Add(new Result(ResultType.Warn, filePath, "Stage(s) found in StageBgmInfo, but not ScenarioBgmInfo. Your music might be muted.", InspectorName, actions, stageBgmStage));
                }
                scenarioBgmStages.Remove(stageBgmStage);
            }
            foreach (string scenarioBgmStage in scenarioBgmStages)
            {
                List<InspectorAction> actions = new()
                {
                    new InspectorAction(() => {
                            var newEntry = new BCSV.Entry();
                            newEntry.Add(STAGE_NAME, scenarioBgmStage);

                            return AddEntry(filePath, "StageBgmInfo.bcsv", newEntry); }, "Add stage to StageBgmInfo"),
                    new InspectorAction(Util.NULL_ACTION, "Ignore this once"),
                };
                resultList.Add(new Result(ResultType.Warn, filePath, "Stage(s) found in ScenarioBgmInfo, but not StageBgmInfo. This will cause a crash.", InspectorName, actions, scenarioBgmStage));
            }
            return resultList;
        }

        private List<string> CollectStages(byte[] bcsvData, Endian endian)
        {
            BCSV bcsv = new BCSV();
            bcsv.Load(new UtilityStream(new MemoryStream(bcsvData), (StreamEndian)endian));

            List<string> stages = new();
            for (int i = 0; i < bcsv.EntryCount; i++)
            {
                BCSV.Entry entry = bcsv[i];
                string stageName = (string)entry[bcsv[STAGE_NAME]];
                if (!stages.Contains(stageName))
                    stages.Add(stageName);
            }
            return stages;
        }

        private List<Result> AddEntry(string arcPath, string bcsvPath, BCSV.Entry entry)
        {
            var thisFunc = () => { return AddEntry(arcPath, bcsvPath, entry); };
            List<Result> resultList = new List<Result>();

            // Load archive
            IArchive? arch = Util.TryLoadArchive(ref resultList, arcPath, InspectorName, thisFunc);
            if (arch == null)
            {
                // It had an error while loading archive. It has been stored in resultList
                return resultList;
            }

            // Load BCSV
            var bcsvFile = Util.TryLoadFileNodeFromArcByName(arch, bcsvPath);
            if (bcsvFile == null || bcsvFile.FileData == null)
            {
                Util.AddError(ref resultList, arcPath, "File data not found", InspectorName, thisFunc, bcsvPath);
                return resultList;
            }
            BCSV bcsv = new BCSV();
            bcsv.Load(new UtilityStream(new MemoryStream(bcsvFile.FileData), (StreamEndian)arch.Endian));

            // Add to BCSV
            bcsv.Add(entry);
            var data = new UtilityStream(new MemoryStream(), (StreamEndian)arch.Endian);

            // Save
            bcsv.Save(data);
            bcsvFile.FileData = data.ToArray();
            Util.TrySaveArchive(ref resultList, arcPath, InspectorName, arch, thisFunc);
            return new();
        }

        public override bool DoCheck(string filePath)
        {
            string relativePath = Path.GetRelativePath(MainSettings.Instance.ModDirectory, filePath).Replace("\\", "/");
            return base.DoCheck(filePath) && relativePath == "AudioRes/Info/StageBgmInfo.arc";
        }
    }
}
