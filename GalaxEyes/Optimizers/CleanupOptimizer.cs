using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;

namespace GalaxEyes.Optimizers
{
    public class CleanupOptimizer : Optimizer
    {
        public CleanupOptimizer() : base("Cleanup Optimizer")
        {
        }
        public override IHaveSettings? Settings { get; } = null;

        public override List<Result> Check(String filePath)
        {
            List<Result> resultList = new List<Result>();
            List<OptimizerAction> actions = new() {
                new OptimizerAction(() => { return RemoveFile(filePath); }, "Remove file"),
                new OptimizerAction(Util.NULL_ACTION, "Ignore this once")
            };
            resultList.Add(new Result(ResultType.Optimize, filePath, "Temporary files detected.", OptimizerName, actions));

            return resultList;
        }

        public override bool DoCheck(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return base.DoCheck(filePath) && fileName.Contains(".tmp");
        }

        public List<Result> RemoveFile(String filePath)
        {
            File.Delete(filePath);
            return new();
        }
    }
}
