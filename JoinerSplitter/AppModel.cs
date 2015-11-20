using System.ComponentModel;

namespace JoinerSplitter
{

    public class AppModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        Job currentJob;

        public VideoFile CurrentFile
        {
            get
            {
                return currentFile;
            }

            set
            {
                currentFile = value;
                OnPropertyChanged(nameof(CurrentFile));
                OnPropertyChanged(nameof(HasCurrentFile));
            }
        }

        public bool HasCurrentFile => CurrentFile != null;

        public Job CurrentJob
        {
            get
            {
                return currentJob;
            }

            set
            {
                currentJob = value;
                OnPropertyChanged(nameof(CurrentJob));
            }
        }

        VideoFile currentFile;

        public AppModel()
        {
            CurrentJob = new Job();
        }
        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}