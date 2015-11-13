using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JoinerSplitter
{
  public class VideoFile : INotifyPropertyChanged
  {

    private Uri filePath;
    public string FileName => Path.GetFileName(FilePath.LocalPath);
    private TimeSpan duration;

    public int GroupIndex { get; set; }
    private TimeSpan start;
    private TimeSpan end;

    public bool ReadOnly { get; set; } = true;

    public Uri FilePath
    {
      get
      {
        return filePath;
      }

      set
      {
        filePath = value;
        OnPropertyChanged(nameof(FilePath));
      }
    }

    public TimeSpan Duration
    {
      get
      {
        return duration;
      }

      set
      {
        duration = value;
        OnPropertyChanged(nameof(Duration));
      }
    }

    public TimeSpan Start
    {
      get
      {
        return start;
      }

      set
      {
        start = value;
        OnPropertyChanged(nameof(Start));
        OnPropertyChanged(nameof(StartSeconds));
      }
    }

    public TimeSpan End
    {
      get
      {
        return end;
      }

      set
      {
        end = value;
        OnPropertyChanged(nameof(End));
        OnPropertyChanged(nameof(EndSeconds));
      }
    }

    public double StartSeconds
    {
      get { return start.TotalSeconds; }

      set { Start = TimeSpan.FromSeconds(value); }
    }

    public double EndSeconds
    {
      get { return end.TotalSeconds; }

      set { End = TimeSpan.FromSeconds(value); }
    }

    public VideoFile()
    {

    }
    public VideoFile(string path)
    {
      FilePath = new Uri(path);
    }

    protected virtual void OnPropertyChanged(string e)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e));
    }

    public event PropertyChangedEventHandler PropertyChanged;
  }

  public class Job
  {
    readonly ObservableCollection<VideoFile> inputFiles = new ObservableCollection<VideoFile>();
    readonly ObservableCollection<VideoFile> outputFiles = new ObservableCollection<VideoFile>();

    public ObservableCollection<VideoFile> InputFiles => inputFiles;
    public ObservableCollection<VideoFile> OutputFiles => outputFiles;
    public string OutputName { get; set; }
  }
}
