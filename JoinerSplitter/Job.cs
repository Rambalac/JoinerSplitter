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

  public class Job
  {
    readonly ObservableCollection<VideoFile> inputFiles = new ObservableCollection<VideoFile>();
    readonly ObservableCollection<VideoFile> outputFiles = new ObservableCollection<VideoFile>();

    public ObservableCollection<VideoFile> InputFiles => inputFiles;
    public ObservableCollection<VideoFile> OutputFiles => outputFiles;
    public string OutputName { get; set; }
    public string OutputFolder { get; set; }
  }
}
