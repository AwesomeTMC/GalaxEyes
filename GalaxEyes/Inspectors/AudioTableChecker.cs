
using Avalonia.Controls.Documents;
using Binary_Stream;
using Hack.io.BCSV;
using Hack.io.Utility;
using Hack.io.YAZ0;
using jkr_lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace GalaxEyes.Inspectors
{

    public class AudioTableChecker : Inspector
    {
        public static uint STAGE_NAME = 0xE4EC2289;
        public static uint SCENARIO_NO = 0xED08B591;
        public AudioTableChecker() : base("Audio Table Checker")
        {
        }
        public override IHaveSettings? Settings { get; } = null;

        public override List<Result> Check(string filePath)
        {
            List<Result> resultList = new List<Result>();
            JKRArchive? arch = Util.TryLoadArchive(ref resultList, filePath, InspectorName, () => { return Check(filePath); });
            if (arch == null)
                return resultList;

            var scenarioBgmInfo = arch.FindFile("ScenarioBgmInfo.bcsv")?.First<JKRFileNode>();
            var stageBgmInfo = arch.FindFile("StageBgmInfo.bcsv").First<JKRFileNode>();
            if (scenarioBgmInfo == null || stageBgmInfo == null)
            {
                Util.AddError(ref resultList, filePath, "StageBgmInfo or ScenarioBgmInfo not found", InspectorName, () => { return Check(filePath); });
                return resultList;
            }


            List<string> scenarioBgmStages = CollectStages(scenarioBgmInfo);
            List<string> stageBgmStages = CollectStages(stageBgmInfo);
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

        private List<string> CollectStages(JKRFileNode bcsvFile)
        {
            BCSV bcsv = new BCSV();
            StreamUtil.PushEndianBig();
            bcsv.Load(new MemoryStream(bcsvFile.Data));

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
            JKRArchive? arch = Util.TryLoadArchive(ref resultList, arcPath, InspectorName, thisFunc);
            if (arch == null)
            {
                // It had an error while loading archive. It has been stored in resultList
                return resultList;
            }

            // Load BCSV
            var bcsvFile = arch.FindFile(bcsvPath)?.First<JKRFileNode>();
            if (bcsvFile == null)
            {
                Util.AddError(ref resultList, arcPath, "File not found", InspectorName, thisFunc, bcsvPath);
                return resultList;
            }
            BCSV bcsv = new BCSV();
            bool endian = arch.Endian == Endian.Little ? false : true;
            StreamUtil.PushEndian(endian);
            bcsv.Load(new MemoryStream(bcsvFile.Data));

            // Add to BCSV
            bcsv.Add(entry);
            var data = new MemoryStream();
            StreamUtil.PushEndian(endian);

            // Save
            bcsv.Save(data);
            bcsvFile.SetFileData(data.ToArray());
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
