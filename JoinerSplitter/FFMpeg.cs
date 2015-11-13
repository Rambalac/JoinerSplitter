using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

    public async Task DoJob(Job job)
    {
      var steps = job.OutputFiles.GroupBy(f => f.GroupIndex).Select(g =>
        g.Select(f => $"-ss {f.Start} -t {f.End - f.Start} -i \"{f.FilePath.LocalPath}\"")
        .Concat(new string[] { $"-c copy {job.OutputName}_{g.Key}" }));
      foreach (var step in steps)
      {
        var proc = StartProcess(FFMpegPath, step.ToArray());
        await proc.Task;
      }
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
      return StartProcess(command, ProcessPriorityClass.Normal, arguments);
    }
    private ProcessTask StartProcess(string command, ProcessPriorityClass priorityClass, params string[] arguments)
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
