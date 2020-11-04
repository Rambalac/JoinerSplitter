namespace JoinerSplitter
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class FFMpeg
    {
        // frame=   81 fps=0.0 q=-1.0 Lsize=   20952kB time = 00:00:03.09 bitrate=55455.1kbits/s
        private static readonly Regex TimeExtract =
            new Regex(@"\s*frame\s*=\s*(?<frame>\d*)\s*fps\s*=\s*(?<fps>[\d.]*)\s*q\s*=[\d-+.]*\s*(L)?size\s*=\s*(\d*\w{1,5})?\s*time\s*=\s*(?<time>\d{2}:\d{2}:\d{2}\.\d{2}).*");

        private static readonly Regex DurationExtract =
            new Regex(@"\s*Duration: (?<time>\d{2}:\d{2}:\d{2}\.\d{2}).*");

        private static SemaphoreSlim tasksLimit;

        private FFMpeg()
        {
        }

        public static FFMpeg Instance { get; set; } = new FFMpeg();

        public string FFMpegPath { get; set; } = "chcp 65001 && ffmpeg.exe";

        public string FFProbePath { get; set; } = "chcp 65001 && ffprobe.exe";

        public async Task DoJob(Job job, Action<IParallelProgress> progressUpdate, CancellationToken cancellation)
        {
            tasksLimit = new SemaphoreSlim(2);

            var groups = job.FileGroups.ToList();
            var error = groups.Join(job.Files, g => g.FilePath, f => f.FilePath, (g, f) => g.FilePath).ToList();
            if (error.Any())
            {
                throw new ArgumentException("Some output file names are the same as one of inputs:\r\n" + string.Join("\r\n", error.Select(s => "  " + s)));
            }

            var tasks = new List<Task>();
            var progress = new ParallelProgressContainer(progressUpdate);

            foreach (var step in groups)
            {
                Task task;
                var subprogress = new ParallelProgressContainer();
                progress.Add(subprogress);
                task = ConcatMultipleFiles(step, subprogress, cancellation);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks.ToArray());
        }

        public async Task<double> GetDuration(string filePath)
        {
            using (var result = StartProcess(FFProbePath, "-v error -select_streams v:0 -show_entries stream=duration -of default=noprint_wrappers=1:nokey=1", $"\"{filePath}\""))
            {
                try
                {
                    var proc = await result;

                    var line = proc.GetLines().First();
                    return double.Parse(line, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Wrong ffprobe result: " + ex.Message, ex);
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
        private static Task<ProcessResult> StartProcessFull(
            string command,
            ProcessPriorityClass priorityClass,
            ProcessTaskParams parameters,
            Action<string> progress,
            CancellationToken? cancellation,
            params string[] arguments)
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
                    ErrorDialog = false,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                },
                EnableRaisingEvents = true,
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
                    exited.TrySetException(new InvalidOperationException(string.Join("\r\n", result.ErrorLines.Any() ? result.ErrorLines : result.ResultLines)));
                }

                proc.Dispose();
            };
            proc.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    result.ErrorLines.AddLast(args.Data);
                    if ((result.Parameters.ErrorLinesLimit != -1) && (result.ErrorLines.Count > result.Parameters.ErrorLinesLimit))
                    {
                        result.ErrorLines.RemoveFirst();
                    }

                    progress?.Invoke(args.Data);
                }
            };
            proc.OutputDataReceived += (sender, args) =>
            {
                result.ResultLines.AddLast(args.Data);
                if ((result.Parameters.ResultLinesLimit != -1) && (result.ResultLines.Count > result.Parameters.ResultLinesLimit))
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

        private static void UpdateProgress(string str, ParallelProgressChild progress)
        {
            ThreadState.KeepAwake();
            if (progress.Duration == null)
            {
                var dm = DurationExtract.Match(str);
                if (dm.Success)
                {
                    var time = TimeSpan.Parse(dm.Groups["time"].Value, CultureInfo.InvariantCulture).TotalSeconds;
                    progress.SetDuration(time);
                }
                return;
            }

            var m = TimeExtract.Match(str);
            if (m.Success)
            {
                var time = TimeSpan.Parse(m.Groups["time"].Value, CultureInfo.InvariantCulture).TotalSeconds;
                progress.Set(time/progress.Duration.Value );
            }
        }

        private async Task ConcatMultipleFiles(FilesGroup step, ParallelProgressContainer progress, CancellationToken cancellation)
        {
            var outputFormat = step.OutputEncoding ?? "-c copy";
            var concatFile = await CreateConcatFile(step.Files);
            try
            {
                var subProgress = new ParallelProgressChild();
                progress.Add(subProgress);

                try
                {
                    await tasksLimit.WaitAsync(cancellation);

                    string arguments = $"-f concat -safe 0 -i \"{concatFile}\" {outputFormat} -y \"{step.FilePath}\"";
                    using (var proc = StartProcess(
                        FFMpegPath,
                        str => UpdateProgress(str, subProgress),
                        cancellation,
                        arguments))
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

        private async Task<string> CreateConcatFile(ICollection<VideoFile> concatFiles)
        {
            var result = Path.GetTempFileName() + ".txt";

            using (var writer = File.CreateText(result))
            {
                foreach (var file in concatFiles)
                {
                    await writer.WriteLineAsync($"file '{file.FilePath.Replace('\\', '/')}'");
                    await writer.WriteLineAsync($"inpoint {file.Start}");
                    await writer.WriteLineAsync($"outpoint {file.End}");
                }
            }

            return result;
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