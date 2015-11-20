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

        string filePath;
        public string FileName => Path.GetFileName(FilePath);
        TimeSpan duration;

        int groupIndex;
        TimeSpan start;
        TimeSpan end;

        public TimeSpan CutDuration => End - Start;

        public Uri FileUri => new Uri(FilePath);

        public string FilePath
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

        public int GroupIndex
        {
            get
            {
                return groupIndex;
            }

            set
            {
                groupIndex = value;
                OnPropertyChanged(nameof(GroupIndex));
            }
        }

        public VideoFile()
        {

        }
        public VideoFile(string path)
        {
            FilePath = path;
        }

        public VideoFile(VideoFile video)
        {
            filePath = video.filePath;
            duration = video.Duration;
            start = video.start;
            end = video.end;
            groupIndex = video.groupIndex;
        }

        protected virtual void OnPropertyChanged(string e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}