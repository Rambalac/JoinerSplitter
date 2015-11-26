using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace JoinerSplitter
{
    [DataContract]
    public class VideoFile : INotifyPropertyChanged
    {
        [DataMember]
        string filePath;

        [DataMember]
        int groupIndex;

        [DataMember]
        double start;

        [DataMember]
        double end;

        List<double> keyFrames;

        double duration;

        public List<double> KeyFrames => keyFrames;
        public string FileName => Path.GetFileName(FilePath);
        public double CutDuration => End - Start;
        public Uri FileUri => new Uri(FilePath);

        public string FilePath => filePath;

        public double Duration => duration;

        public double Start
        {
            get
            {
                return start;
            }

            set
            {
                start = value;
                OnPropertyChanged(nameof(Start));
            }
        }

        public double End
        {
            get
            {
                return end;
            }

            set
            {
                end = value;
                OnPropertyChanged(nameof(End));
            }
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
        public VideoFile(string path, double duration, List<double> keyFrames)
        {
            filePath = path;
            this.duration = end = duration;
            this.keyFrames = keyFrames;
        }

        public VideoFile(VideoFile video)
        {
            filePath = video.filePath;
            duration = video.Duration;
            keyFrames = video.keyFrames;
            start = video.start;
            end = video.end;
            groupIndex = video.groupIndex;
        }

        protected virtual void OnPropertyChanged(string e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [OnDeserialized]
        void OnDeserialized(StreamingContext context)
        {
            if (!File.Exists(filePath))
            {
                var testPath = Path.GetFileName(filePath);
                if (File.Exists(testPath))
                {
                    filePath = testPath;
                }
                else throw new SerializationException("Video file used in job does not exist");
            }
            duration = FFMpeg.GetInstance().GetDuration(filePath).Result;
            keyFrames = FFMpeg.GetInstance().GetKeyFrames(filePath).Result;
        }
    }
}