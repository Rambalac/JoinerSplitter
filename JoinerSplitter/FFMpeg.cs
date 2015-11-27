using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.FormattableString;

namespace JoinerSplitter
{
    public class FFMpeg
    {
        FFMpeg()
        {

        }

        public static FFMpeg Instance { get; set; } = new FFMpeg();
        public static FFMpeg GetInstance() => Instance;

        public string FFMpegPath { get; set; } = "ffmpeg.exe";
        public string FFProbePath { get; set; } = "ffprobe.exe";

        public async Task<List<double>> GetKeyFrames(string filePath)
        {
            var proc = StartProcess(FFProbePath, -1, "-v error -select_streams v -show_frames -show_entries frame=key_frame,best_effort_timestamp_time -of csv", $"\"{filePath}\"");
            await proc.Task;

            try
            {
                var lines = GetLines(proc.ResultLines);
                var result = lines.Where(s => s.StartsWith("frame,1", StringComparison.InvariantCulture))
                    .Select(s => double.Parse(s.Substring(8), CultureInfo.InvariantCulture)).ToList();

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Wrong ffprobe result: " + string.Join("\r\n", proc.ResultLines), ex);
            }
        }

        public async Task<double> GetDuration(string filePath)
        {
            var proc = StartProcess(FFProbePath, "-v error -select_streams v:0 -show_entries stream=duration -of default=noprint_wrappers=1:nokey=1", $"\"{filePath}\"");
            await proc.Task;

            try
            {
                var line = GetLine(proc.ResultLines);
                var result = double.Parse(line, CultureInfo.InvariantCulture);

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Wrong ffprobe result: " + string.Join("\r\n", proc.ResultLines), ex);
            }
        }

        // frame=   81 fps=0.0 q=-1.0 Lsize=   20952kB time = 00:00:03.09 bitrate=55455.1kbits/s
        readonly static Regex timeExtract = new Regex(@"\s*frame\s*=\s*(?<frame>\d*)\s*fps\s*=\s*(?<fps>[\d.]*)\s*q\s*=[\d-+.]*\s*(L)?size\s*=\s*(\d*\w{1,5})?\s*time\s*=\s*(?<time>\d{2}:\d{2}:\d{2}\.\d{2}).*");

        void UpdateProgress(Action<int> progress, string str, double totalSec, double done, double coef = 1)
        {
            var m = timeExtract.Match(str);
            if (m.Success)
            {
                var time = TimeSpan.Parse(m.Groups["time"].Value, CultureInfo.InvariantCulture).TotalSeconds;
                progress?.Invoke((int)((done + time * coef) * 100 / (2 * totalSec)));
            }

        }
        public async Task DoJob(Job job, Action<int> progress = null)
        {
            var totalSec = job.Files.Sum(f => f.Duration);
            double done = 0;
            var groups = job.FileGroups.ToList();
            var error = groups.Join(job.Files, g => g.FilePath, f => f.FilePath, (g, f) => g.FilePath);
            if (error.Any()) throw new ArgumentException("Some output file names are the same as one of inputs:\r\n" + string.Join("\r\n", error.Select(s => "  " + s)));

            foreach (var step in groups)
            {
                if (step.Files.Count() > 1)
                    await ConcatMultipleFiles(step, done, totalSec, progress);
                else
                    await CutOneFile(step.Files.Single(), step.FilePath, done, totalSec, progress);
                done += step.Files.Sum(f => f.CutDuration);
            }

        }

        async Task CutOneFile(VideoFile file, string filePath, double done, double totalSec, Action<int> progress)
        {
            var args = Invariant($"-i \"{file.FilePath}\" -ss {file.Start} -t {file.CutDuration} -c copy -y \"{filePath}\"");
            Debug.WriteLine(args);

            var proc = StartProcess(FFMpegPath, (str) => UpdateProgress(progress, str, totalSec, done, 2), args);

            await proc.Task;

        }

        async Task ConcatMultipleFiles(Job.FilesGroup step, double done, double totalSec, Action<int> progress)
        {
            var concatFiles = new List<string>();
            var filesToDelete = new List<string>();
            foreach (var file in step.Files)
                if (Math.Abs(file.CutDuration - file.Duration) < 0.0001)
                {
                    concatFiles.Add(file.FilePath);
                    done += file.Duration;
                }
                else
                {
                    var newfile = Path.GetTempFileName() + Path.GetExtension(file.FilePath);

                    var tempargs = Invariant($"-i \"{file.FilePath}\" -ss {file.Start} -t {file.CutDuration} -c copy -y \"{newfile}\"");
                    Debug.WriteLine(tempargs);
                    var tempproc = StartProcess(FFMpegPath, (str) => UpdateProgress(progress, str, totalSec, done), tempargs);

                    await tempproc.Task;

                    done += file.CutDuration;
                    concatFiles.Add(newfile);
                    filesToDelete.Add(newfile);
                }

            var concatFile = await CreateConcatFile(concatFiles);

            var proc = StartProcess(FFMpegPath, (str) => UpdateProgress(progress, str, totalSec, done), $"-f concat -i \"{concatFile}\" -c copy -y {step.FilePath}");

            await proc.Task;

            File.Delete(concatFile);
            foreach (var file in filesToDelete)
                File.Delete(file);
        }

        async Task<string> CreateConcatFile(List<string> concatFiles)
        {
            var result = Path.GetTempFileName() + ".txt";

            using (var writer = File.CreateText(result))
            {

                foreach (var file in concatFiles)
                    await writer.WriteLineAsync($"file '{file.Replace('\\', '/')}'");
            }
            return result;
        }

        static string GetLine(ICollection<string> list)
        {
            return list.Single(s => s != null && s.Trim('\r', '\n', ' ', '\t') != "").Trim('\r', '\n', ' ', '\t');
        }

        static IEnumerable<string> GetLines(ICollection<string> list)
        {
            return list.Where(s => s != null).Select(s => s.Trim('\r', '\n', ' ', '\t')).Where(s => !string.IsNullOrWhiteSpace(s));
        }


        class ProcessTaskParams
        {
            public int ErrorLinesLimit = 10;
            public int ResultLinesLimit = 10;
        }
        class ProcessTask
        {
            public ProcessTaskParams parameters;
            public LinkedList<string> ErrorLines = new LinkedList<string>();
            public LinkedList<string> ResultLines = new LinkedList<string>();
            public Process Process;
            public Task Task;

            public ProcessTask(Process proc, Task t, ProcessTaskParams parameters)
            {
                this.parameters = parameters;
                Process = proc;
                Task = t;
            }
        }

        ProcessTask StartProcess(string command, params string[] arguments)
        {
            return StartProcess(command, ProcessPriorityClass.Normal, new ProcessTaskParams(), null, arguments);
        }

        ProcessTask StartProcess(string command, Action<string> progress, params string[] arguments)
        {
            return StartProcess(command, ProcessPriorityClass.Normal, new ProcessTaskParams(), progress, arguments);
        }

        ProcessTask StartProcess(string command, int resultLimit, params string[] arguments)
        {
            var pars = new ProcessTaskParams { ResultLinesLimit = resultLimit };

            return StartProcess(command, ProcessPriorityClass.Normal, pars, null, arguments);
        }

        ProcessTask StartProcess(string command, ProcessPriorityClass priorityClass, ProcessTaskParams parameters, Action<string> progress, params string[] arguments)
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

            var exited = new TaskCompletionSource<bool>();
            var result = new ProcessTask(proc, exited.Task, parameters);

            proc.Exited += (sender, args) =>
            {
                var exitcode = proc.ExitCode;
                if (exitcode == 0)
                {
                    exited.SetResult(true);
                }
                else
                {
                    exited.TrySetException(new Exception(string.Join("\r\n", result.ErrorLines)));
                }
            };
            proc.ErrorDataReceived += (sender, args) =>
            {
                result.ErrorLines.AddLast(args.Data);
                if (result.parameters.ErrorLinesLimit != -1 && result.ErrorLines.Count > result.parameters.ErrorLinesLimit) result.ErrorLines.RemoveFirst();

                if (!string.IsNullOrWhiteSpace(args.Data))
                    progress?.Invoke(args.Data);
            };
            proc.OutputDataReceived += (sender, args) =>
            {
                result.ResultLines.AddLast(args.Data);
                if (result.parameters.ResultLinesLimit != -1 && result.ResultLines.Count > result.parameters.ResultLinesLimit) result.ResultLines.RemoveFirst();
            };

            proc.Start();
            proc.PriorityClass = priorityClass;
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();
            return result;
        }
    }
}
