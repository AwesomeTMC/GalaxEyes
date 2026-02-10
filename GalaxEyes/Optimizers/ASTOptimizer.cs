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
            double ratio = (32000f / ast.SampleRate);
            var newLoopStart = (int)Math.Round(ast.LoopStart * ratio);
            var newLoopEnd = (int)Math.Round(ast.LoopEnd * ratio);
            var oldFormat = ast.format;

            // encode to pcm16 since that's the only format ffmpeg likes for ASTs
            ast.format = EncodeFormat.PCM16;
            var pcmAstPath = filePath.Replace(".ast", ".tmp.pcm16.ast");
            OutAst(ast, pcmAstPath);

            // Resample using ffmpeg
            var resampledAstPath = filePath.Replace(".ast", ".tmp.resampled.ast");
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{pcmAstPath}\" -ar 32000 \"{resampledAstPath}\"",
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
                Util.AddError(ref results, filePath, "FFMPEG ran into an issue. Do you have it installed?", OptimizerName, () => { return ResampleAST(filePath); }, stderr);
                return results;
            }
            File.Delete(pcmAstPath);

            // Convert back to the old format and fix loop points
            ast = InAst(resampledAstPath);
            ast.format = oldFormat;
            ast.LoopStart = newLoopStart;
            ast.LoopEnd = newLoopEnd;
            File.Delete(resampledAstPath);
            OutAst(ast, filePath);
            
            return results;
        }

        private AST InAst(string filePath)
        {
            using var waveInput = File.OpenRead(filePath);
            using var waveReader = new BeBinaryReader(waveInput);
            var ast = new AST();
            ast.ReadFromStream(waveReader);
            return ast;
        }

        private void OutAst(AST ast, string filePath)
        {
            var tmpPath = filePath + ".tmp";
            using var astOutput = File.Create(tmpPath);
            using var astWriter = new BeBinaryWriter(astOutput);
            astWriter.Flush();
            ast.WriteToStream(astWriter);

            File.Move(tmpPath, filePath, true);
        }
    }
}
