using GalaxEyes.Optimizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GalaxEyes;

public static class Util
{
    public static Func<List<Result>> NULL_ACTION = () => { return new(); };
    public static void AddError(ref List<Result> results, string message, string affectedFile, Func<List<Result>> retryCallback)
    {
        List<OptimizerAction> standardActions = new()
        {
            new OptimizerAction(retryCallback, "Retry"),
            new OptimizerAction(NULL_ACTION, "Ignore this once"),
            // TODO: Add more ignore options
        };

        results.Add(new Result(ResultType.Error, message, affectedFile, standardActions));
    }
}