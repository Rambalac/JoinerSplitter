namespace JoinerSplitter
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using static System.FormattableString;

    public class FFMpeg
    {
        // frame=   81 fps=0.0 q=-1.0 Lsize=   20952kB time = 00:00:03.09 bitrate=55455.1kbits/s
        private static readonly Regex TimeExtract = new Regex(@"\s*frame\s*=\s*(?<frame>\d*)\s*fps\s*=\s*(?<fps>[\d.]*)\s*q\s*=[\d-+.]*\s*(L)?size\s*=\s*(\d*\w{1,5})?\s*time\s*=\s*(?<time>\d{2}:\d{2}:\d{2}\.\d{2}).*");

        private static SemaphoreSlim tasksLimit;

        private FFMpeg()
        {
        }

        public static FFMpeg Instance { get; set; } = new FFMpeg();

        public string FFMpegPath { get; set; } = "ffmpeg.exe";

        public string FFProbePath { get; set; } = "ffprobe.exe";

        public async Task DoJob(Job job, Action<double> progressUpdate, CancellationToken cancellation)
        {
            tasksLimit = new SemaphoreSlim(2);

            var groups = job.FileGroups.ToList();
            var error = groups.Join(job.Files, g => g.FilePath, f => f.FilePath, (g, f) => g.FilePath).ToList();
            if (error.Any())
            {
                throw new ArgumentException("Some output file names are the same as one of inputs:\r\n" + string.Join("\r\n", error.Select(s => "  " + s)));
            }

            var tasks = new List<Task>();
            var progress = new ParallelProgressRoot(progressUpdate);

            foreach (var step in groups)
            {
                var stepDuration = step.Files.Sum(f => f.CutDuration);
                Task task;
                if (step.Files.Count > 1)
                {
                    var subprogress = new ParallelProgressContainer();
                    progress.Add(subprogress);
                    task = ConcatMultipleFiles(step, subprogress, cancellation);
                }
                else
                {
                    var subprogress = new ParallelProgressChild();
                    progress.Add(subprogress);
                    task = CutOneFile(step, subprogress, cancellation);
                }

                tasks.Add(task);
            }

            await Task.WhenAll(tasks.ToArray());
        }

        public async Task<double> GetDuration(string filePath)
        {
            using (var result = StartProcess(FFProbePath, "-v error -select_streams v:0 -show_entries stream=duration -of default=noprint_wrappers=1:nokey=1", $"\"{filePath}\""))
            {
                var proc = await result;

                try
                {
                    var line = proc.GetLines().First();
                    return double.Parse(line, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Wrong ffprobe result: " + string.Join("\r\n", proc.ResultLines), ex);
                }
            }
        }

        public async Task<List<double>> GetKeyFrames(string filePath)
        {
            using (var result = StartProcess(FFProbePath, -1, "-v error -select_streams v -show_frames -show_entries frame=key_frame,best_effort_timestamp_time -of csv", $"\"{filePath}\""))
            {
                var proc = await result;

                try
                {
                    var lines = proc.GetLines();
                    return lines.Where(s => s.StartsWith("frame,1", StringComparison.InvariantCulture))
                        .Select(s => double.Parse(s.Substring(8), CultureInfo.InvariantCulture)).ToList();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Wrong ffprobe result: " + string.Join("\r\n", proc.ResultLines), ex);
                }
            }
        }

        private static Task<ProcessResult> StartProcess(string command, params string[] arguments)
        {
            return StartProcessFull(command, ProcessPriorityClass.Normal, new ProcessTaskParams(), null, null, arguments);
        }

        private static Task<ProcessResult> StartProcess(string command, Action<string> progress, CancellationToken? cancellation, params string[] arguments)
        {
            return StartProcessFull(command, ProcessPriorityClass.Normal, new ProcessTaskParams(), progress, cancellation, arguments);
        }

        private static Task<ProcessResult> StartProcess(string command, int resultLimit, params string[] arguments)
        {
            var pars = new ProcessTaskParams { ResultLinesLimit = resultLimit };

            return StartProcessFull(command, ProcessPriorityClass.Normal, pars, null, null, arguments);
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Disposed by caller")]
        private static Task<ProcessResult> StartProcessFull(string command, ProcessPriorityClass priorityClass, ProcessTaskParams parameters, Action<string> progress, CancellationToken? cancellation, params string[] arguments)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = string.Join(" ", arguments),

                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    ErrorDialog = false
                },
                EnableRaisingEvents = true
            };

            var exited = new TaskCompletionSource<ProcessResult>();
            var result = new ProcessResult(proc, parameters);

            proc.Exited += (sender, args) =>
            {
                var exitcode = proc.ExitCode;
                if (exitcode == 0)
                {
                    exited.SetResult(result);
                }
                else
                {
                    exited.TrySetException(new InvalidOperationException(string.Join("\r\n", result.ErrorLines)));
                }

                proc.Dispose();
            };
            proc.ErrorDataReceived += (sender, args) =>
            {
                result.ErrorLines.AddLast(args.Data);
                if (result.Parameters.ErrorLinesLimit != -1 && result.ErrorLines.Count > result.Parameters.ErrorLinesLimit)
                {
                    result.ErrorLines.RemoveFirst();
                }

                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    progress?.Invoke(args.Data);
                }
            };
            proc.OutputDataReceived += (sender, args) =>
            {
                result.ResultLines.AddLast(args.Data);
                if (result.Parameters.ResultLinesLimit != -1 && result.ResultLines.Count > result.Parameters.ResultLinesLimit)
                {
                    result.ResultLines.RemoveFirst();
                }
            };

            proc.Start();
            proc.PriorityClass = priorityClass;
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();

            cancellation?.Register(() =>
            {
                proc.Kill();
                proc.Dispose();
                exited.TrySetCanceled();
            });

            return exited.Task;
        }

        private static void UpdateProgress(string str, ParallelProgressChild progress, double coef = 1)
        {
            var m = TimeExtract.Match(str);
            if (m.Success)
            {
                var time = TimeSpan.Parse(m.Groups["time"].Value, CultureInfo.InvariantCulture).TotalSeconds;
                progress.Update(time * coef);
            }
        }

        private async Task ConcatMultipleFiles(FilesGroup step, ParallelProgressContainer progress, CancellationToken cancellation)
        {
            var outputFormat = step.OutputEncoding ?? "-c copy";
            var concatFiles = new List<string>();
            var tasks = new List<Task>();
            var filesToDelete = new List<string>();
            var doneLock = new object();
            try
            {
                foreach (var file in step.Files)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (Math.Abs(file.CutDuration - file.Duration) < 0.001)
                    {
                        concatFiles.Add(file.FilePath);
                    }
                    else
                    {
                        var newfile = Path.GetTempFileName() + Path.GetExtension(step.FilePath);

                        var tempargs = Invariant($"-i \"{file.FilePath}\" -ss {file.Start} -t {file.CutDuration} {outputFormat} -y \"{newfile}\"");
                        Debug.WriteLine(tempargs);

                        var cutprogress = new ParallelProgressChild();
                        progress.Add(cutprogress);

                        await tasksLimit.WaitAsync(cancellation);
                        var tempproc = StartProcess(FFMpegPath, str => UpdateProgress(str, cutprogress, 0.5), cancellation, tempargs);

                        var fileCutDuration = file.CutDuration;

                        var task = tempproc.ContinueWith(
                            t =>
                             {
                                 t.Result.Dispose();
                                 tasksLimit.Release();
                             }, cancellation);
                        filesToDelete.Add(newfile);
                        concatFiles.Add(newfile);
                        tasks.Add(task);
                    }
                }

                await Task.WhenAll(tasks.ToArray());

                var concatFile = await CreateConcatFile(concatFiles);
                try
                {
                    var subprogress = new ParallelProgressChild();
                    progress.Add(subprogress);

                    try
                    {
                        await tasksLimit.WaitAsync(cancellation);

                        using (var proc = StartProcess(FFMpegPath, str => UpdateProgress(str, subprogress, 0.5), cancellation, $"-f concat -safe 0 -i \"{concatFile}\" -c copy -y \"{step.FilePath}\""))
                        {
                            await proc;
                        }
                    }
                    finally
                    {
                        tasksLimit.Release();
                    }
                }
                finally
                {
                    File.Delete(concatFile);
                }
            }
            finally
            {
                foreach (var file in filesToDelete)
                {
                    File.Delete(file);
                }
            }
        }

        private async Task<string> CreateConcatFile(List<string> concatFiles)
        {
            var result = Path.GetTempFileName() + ".txt";

            using (var writer = File.CreateText(result))
            {
                foreach (var file in concatFiles)
                {
                    await writer.WriteLineAsync($"file '{file.Replace('\\', '/')}'");
                }
            }

            return result;
        }

        private async Task CutOneFile(FilesGroup step, ParallelProgressChild progress, CancellationToken cancellation)
        {
            var outputFormat = step.OutputEncoding ?? "-c copy";
            var file = step.Files.Single();
            var filePath = step.FilePath;

            var args = Invariant($"-i \"{file.FilePath}\" -ss {file.Start} -t {file.CutDuration} {outputFormat} -y \"{filePath}\"");
            Debug.WriteLine(args);

            await tasksLimit.WaitAsync(cancellation);
            try
            {
                using (var proc = StartProcess(FFMpegPath, str => UpdateProgress(str, progress), cancellation, args))
                {
                    await proc;
                }
            }
            finally
            {
                tasksLimit.Release();
            }
        }

        private class ProcessResult : IDisposable
        {
            private bool disposedValue; // To detect redundant calls

            public ProcessResult(Process proc, ProcessTaskParams parameters)
            {
                Parameters = parameters;
                Process = proc;
            }

            public LinkedList<string> ErrorLines { get; } = new LinkedList<string>();

            public ProcessTaskParams Parameters { get; }

            public LinkedList<string> ResultLines { get; } = new LinkedList<string>();

            private Process Process { get; }

            // This code added to correctly implement the disposable pattern.
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
            }

            public string GetLine()
            {
                return ResultLines.Single(s => !string.IsNullOrWhiteSpace(s)).Trim();
            }

            public IEnumerable<string> GetLines()
            {
                return ResultLines.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim());
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        Process.Dispose();
                    }

                    disposedValue = true;
                }
            }
        }

        private class ProcessTaskParams
        {
            public int ErrorLinesLimit { get; set; } = 10;

            public int ResultLinesLimit { get; set; } = 10;
        }
    }
}