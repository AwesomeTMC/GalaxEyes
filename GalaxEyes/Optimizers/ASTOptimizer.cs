using Be.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using jatast;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;

namespace GalaxEyes.Optimizers
{
    public class ASTOptimizer : Optimizer
    {
        public ASTOptimizer() : base("AST Optimizer")
        {
        }
        public override IHaveSettings? Settings { get; } = null;

        public override List<Result> Check(String filePath)
        {
            List<Result> resultList = new List<Result>();

            var ast = InAst(filePath);
            if (ast.format == EncodeFormat.PCM16)
            {
                List<OptimizerAction> actions = new()
                {
                    new OptimizerAction(() => {return ADPCMEncodeAST(filePath); }, "ADPCM Encode AST"),
                    new OptimizerAction(Util.NULL_ACTION, "Ignore this once")
                };
                resultList.Add(new Result(ResultType.Optimize, filePath, "AST encoded in PCM16. Try encoding it in ADPCM.", OptimizerName, actions));
            }
            if (ast.SampleRate > 32000)
            {
                List<OptimizerAction> actions = new()
                {
                    new OptimizerAction(() => {return ResampleAST(filePath); }, "Resample to 32khz"),
                    new OptimizerAction(Util.NULL_ACTION, "Ignore this once")
                };
                resultList.Add(new Result(ResultType.Optimize, filePath, "AST sample rate > 32khz.", OptimizerName, actions, ast.SampleRate.ToString()));
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

        public override bool DoCheck(string filePath)
        {
            return base.DoCheck(filePath) && filePath.EndsWith(".ast");
        }

        public List<Result> ADPCMEncodeAST(string filePath)
        {
            var ast = InAst(filePath);
            ast.format = EncodeFormat.ADPCM4;
            OutAst(ast, filePath);
            return new();
        }

        public List<Result> ResampleAST(string filePath)
        {
            List<Result> results = new();
            var ast = InAst(filePath);
            var ratio = (32000 / ast.SampleRate);
            var newLoopStart = ast.LoopStart * ratio;
            var newLoopEnd = ast.LoopEnd * ratio;
            var oldFormat = ast.format;

            ast.format = EncodeFormat.PCM16;
            var tmpAstPath = filePath.Replace(".ast", ".tmp.ast");
            OutAst(ast, tmpAstPath);

            // Resample using ffmpeg
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{tmpAstPath}\" -ar 32000 \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Util.AddError(ref results, filePath, "FFMPEG ran into an issue", OptimizerName, () => { return ResampleAST(filePath); }, stderr);
                return results;
            }
            File.Delete(tmpAstPath);


            // Convert back to the old format and fix loop points
            ast = InAst(filePath);
            ast.format = oldFormat;
            ast.LoopStart = newLoopStart;
            ast.LoopEnd = newLoopEnd;
            OutAst(ast, filePath);
            return results;
        }

        private AST InAst(string filePath)
        {
            var waveInput = File.OpenRead(filePath);
            var waveReader = new BeBinaryReader(waveInput);
            var ast = new AST();
            ast.ReadFromStream(waveReader);
            waveReader.Close();
            return ast;
        }

        private void OutAst(AST ast, string filePath)
        {
            var astOutput = File.OpenWrite(filePath);
            var astWriter = new BeBinaryWriter(astOutput);
            astWriter.Flush();
            ast.WriteToStream(astWriter);
            astWriter.Close();
        }
    }
}
