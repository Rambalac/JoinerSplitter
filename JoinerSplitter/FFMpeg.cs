using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JoinerSplitter
{
    public class FFMpeg
    {
        public string FFMpegPath { get; set; } = "ffmpeg.exe";
        public string FFProbePath { get; set; } = "ffprobe.exe";

        public async Task<TimeSpan> GetDuration(string filePath)
        {
            var proc = StartProcess(FFProbePath, "-v error -select_streams v:0 -show_entries stream=duration -of default=noprint_wrappers=1:nokey=1", $"\"{filePath}\"");
            await proc.Task;

            try
            {
                var line = GetLine(proc.ResultLines);
                var result = double.Parse(line);

                return TimeSpan.FromSeconds(result);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Wrong ffprobe result: " + string.Join("\r\n", proc.ResultLines), ex);
            }
        }

        // frame=   81 fps=0.0 q=-1.0 Lsize=   20952kB time = 00:00:03.09 bitrate=55455.1kbits/s
        readonly Regex timeExtract = new Regex(@"\s*frame\s*=\s*(?<frame>\d*)\s*fps\s*=\s*(?<fps>[\d.]*)\s*q\s*=[\d-+.]*\s*Lsize\s*=\s*(\d*\w{1,5})?\s*time\s*=\s*(?<time>\d{2}:\d{2}:\d{2}\.\d{2}).*");

        private void UpdateProgress(Action<int> progress, string str, double totalSec, double done, double coof = 1)
        {
            var m = timeExtract.Match(str);
            if (m.Success)
            {
                var time = TimeSpan.Parse(m.Groups["time"].Value).TotalSeconds;
                progress?.Invoke((int)((done + time * coof) * 100 / (2 * totalSec)));
            }

        }
        public async Task DoJob(Job job, Action<int> progress = null)
        {
            var totalSec = job.Files.Sum(f => f.Duration.TotalSeconds);
            double done = 0;
            foreach (var step in job.FileGroups)
            {
                if (step.Files.Count() > 1)
                    await ConcatMultipleFiles(step, done, totalSec, progress);
                else
                    await CutOneFile(step.Files.Single(), step.FilePath, done, totalSec, progress);
                done += step.Files.Sum(f => f.CutDuration.TotalSeconds);
            }

        }

        private async Task CutOneFile(VideoFile file, string filePath, double done, double totalSec, Action<int> progress)
        {
            var args = $"-ss {file.Start} -t {file.End - file.Start} -i \"{file.FilePath}\" -c copy -y \"{filePath}\"";

            var proc = StartProcess(FFMpegPath, (str) => UpdateProgress(progress, str, totalSec, done, 2), args);

            await proc.Task;

        }

        async Task ConcatMultipleFiles(Job.FilesGroup step, double done, double totalSec, Action<int> progress)
        {
            var concatFiles = new List<string>();
            var filesToDelete = new List<string>();
            foreach (var file in step.Files)
                if (file.CutDuration == file.Duration)
                {
                    concatFiles.Add(file.FilePath);
                    done += file.Duration.TotalSeconds;
                }
                else
                {
                    var newfile = Path.GetTempFileName() + Path.GetExtension(file.FilePath);

                    var tempargs = $"-ss {file.Start} -t {file.End - file.Start} -i \"{file.FilePath}\" -c copy -y \"{newfile}\"";

                    var tempproc = StartProcess(FFMpegPath, (str) => UpdateProgress(progress, str, totalSec, done), tempargs);

                    await tempproc.Task;

                    done += (file.End - file.Start).TotalSeconds;
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

        private async Task<string> CreateConcatFile(List<string> concatFiles)
        {
            var result = Path.GetTempFileName()+".txt";

            using (var writer = File.CreateText(result))
            {

                foreach (var file in concatFiles)
                    await writer.WriteLineAsync($"file '{file.Replace('\\', '/')}'");
            }
            return result;
        }

        private static string GetLine(ICollection<string> list)
        {
            return list.Single(s => s != null && s.Trim('\r', '\n', ' ', '\t') != "").Trim('\r', '\n', ' ', '\t');
        }

        class ProcessTask
        {
            public LinkedList<string> ErrorLines = new LinkedList<string>();
            public int ErrorLinesLimit = 10;
            public LinkedList<string> ResultLines = new LinkedList<string>();
            public int ResultLinesLimit = 10;
            public Process Process;
            public Task Task;

            public ProcessTask(Process proc, Task t)
            {
                Process = proc;
                Task = t;
            }
        }

        private ProcessTask StartProcess(string command, params string[] arguments)
        {
            return StartProcess(command, ProcessPriorityClass.Normal, null, arguments);
        }

        private ProcessTask StartProcess(string command, Action<string> progress, params string[] arguments)
        {
            return StartProcess(command, ProcessPriorityClass.Normal, progress, arguments);
        }

        private ProcessTask StartProcess(string command, ProcessPriorityClass priorityClass, Action<string> progress, params string[] arguments)
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
            var result = new ProcessTask(proc, exited.Task);

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
                if (result.ErrorLines.Count > result.ErrorLinesLimit) result.ErrorLines.RemoveFirst();

                if (!string.IsNullOrWhiteSpace(args.Data))
                    progress?.Invoke(args.Data);
            };
            proc.OutputDataReceived += (sender, args) =>
            {
                result.ResultLines.AddLast(args.Data);
                if (result.ResultLines.Count > result.ResultLinesLimit) result.ResultLines.RemoveFirst();
            };

            proc.Start();
            proc.PriorityClass = priorityClass;
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();
            return result;
        }
    }
}
