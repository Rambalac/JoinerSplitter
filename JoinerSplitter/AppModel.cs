using System;
using System.ComponentModel;

namespace JoinerSplitter
{
    public class AppModel : INotifyPropertyChanged
    {
        private VideoFile currentFile;

        private Job currentJob;

        public AppModel()
        {
            CurrentJob = new Job();
        }

        public event PropertyChangedEventHandler PropertyChanged;
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

        public Uri CurrentFileUri => CurrentFile?.FileUri ?? new Uri("");
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

        public bool HasCurrentFile => CurrentFile != null;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}