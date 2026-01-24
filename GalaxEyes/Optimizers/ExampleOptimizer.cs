using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GalaxEyes.Optimizers
{
    public class ExampleOptimizer : Optimizer
    {
        public ExampleOptimizer() : base("Example Optimizer")
        {
        }

        public override List<Result> Check(String filePath)
        {
            List<Result> resultList = new List<Result>();
            if (!IsActive)
                return resultList;

            String fileName = Path.GetFileName(filePath);

            // TODO: put these behind optimizer settings
            //Util.AddError(ref resultList, "Some error occured", filePath, () => { return Check(filePath); });
            //Thread.Sleep(1);

            if (fileName.Contains("Unoptimized"))
            {
                // declare the actions the user can take
                List<OptimizerAction> actions = new() {
                    new OptimizerAction(() => { return OptimizeFile(filePath); }, "Rename file"),
                    new OptimizerAction(Util.NULL_ACTION, "Give up")
                };
                resultList.Add(new Result(ResultType.Optimize, "Your file isn't optimized.", filePath, actions));
            }

            return resultList;
        }

        
        public List<Result> OptimizeFile(String filePath)
        {
            String newPath = filePath.Replace("Unoptimized", "Optimized");
            if (!File.Exists(newPath) && File.Exists(filePath))
                File.Move(filePath, newPath);
            return new();
        }
    }
}
