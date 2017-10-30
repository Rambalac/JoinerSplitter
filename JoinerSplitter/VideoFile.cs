namespace JoinerSplitter
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.Serialization;

    [DataContract]
    public class VideoFile : INotifyPropertyChanged
    {
        private double duration;

        [DataMember]
        private double end;

        [DataMember]
        private string filePath;

        [DataMember]
        private int groupIndex;

        private readonly ICollection<double> keyFrames;

        [DataMember]
        private double start;

        public VideoFile()
        {
        }

        public VideoFile(string path, double duration)
        {
            filePath = path;
            this.duration = end = duration;
        }

        public VideoFile(VideoFile video)
        {
            if (video == null)
            {
                throw new ArgumentNullException(nameof(video));
            }

            filePath = video.filePath;
            duration = video.Duration;
            keyFrames = video.keyFrames;
            start = video.start;
            end = video.end;
            groupIndex = video.groupIndex;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public double CutDuration => End - Start;

        public double Duration => duration;

        public double End
        {
            get => end;

            set
            {
                end = value;
                OnPropertyChanged(nameof(End));
            }
        }

        public string FileName => Path.GetFileName(FilePath);

        public string FilePath => filePath;

        public Uri FileUri => new Uri(FilePath);

        public int GroupIndex
        {
            get => groupIndex;

            set
            {
                groupIndex = value;
                OnPropertyChanged(nameof(GroupIndex));
            }
        }

        public ICollection<double> KeyFrames => keyFrames;

        public double Start
        {
            get => start;

            set
            {
                start = value;
                OnPropertyChanged(nameof(Start));
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            filePath = Path.GetFullPath(filePath);
            if (!File.Exists(filePath))
            {
                var testPath = Path.GetFullPath(Path.GetFileName(filePath));
                if (File.Exists(testPath))
                {
                    filePath = testPath;
                }
                else
                {
                    throw new SerializationException("Video file used in job does not exist");
                }
            }

            duration = FFMpeg.Instance.GetDuration(filePath).Result;
        }
    }
}