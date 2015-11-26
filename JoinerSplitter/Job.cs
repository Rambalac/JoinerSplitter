using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace JoinerSplitter
{
    [DataContract]
    public class Job : INotifyPropertyChanged
    {
        public class FilesGroup
        {
            public readonly string FilePath;
            public readonly ICollection<VideoFile> Files;

            public FilesGroup(string filePath, ICollection<VideoFile> files)
            {
                this.FilePath = filePath;
                this.Files = files;
            }
        }

        public string JobFilePath { get; set; } = "";

        [DataMember]
        readonly ObservableCollection<VideoFile> files = new ObservableCollection<VideoFile>();

        public ObservableCollection<VideoFile> Files => files;

        [DataMember]
        private string outputFolder;

        [DataMember]
        string outputName;

        public bool HasMoreGroupsThanOne => Files.Select(f => f.GroupIndex).Distinct().Count() > 1;

        public Job()
        {
            files.CollectionChanged += Files_CollectionChanged;
        }

        void Files_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasMoreGroupsThanOne));
        }

        public string OutputFolder
        {
            get
            {
                return outputFolder;
            }

            set
            {
                outputFolder = value;
                OnPropertyChanged(nameof(OutputFolder));
            }
        }

        public string OutputName
        {
            get
            {
                return outputName;
            }

            set
            {
                outputName = value;
                OnPropertyChanged(nameof(OutputName));
            }
        }

        public ICollection<FilesGroup> FileGroups
        {
            get
            {
                if (!Files.Any()) return new FilesGroup[] { };
                var folder = OutputFolder ?? Path.GetDirectoryName(Files.First().FilePath);
                if (Files.Select(f => f.GroupIndex).Distinct().Count() == 1) return new FilesGroup[] { new FilesGroup(Path.Combine(folder, OutputName), Files) };
                var noext = Path.GetFileNameWithoutExtension(OutputName);
                var ext = Path.GetExtension(OutputName);
                return Files.GroupBy(f => f.GroupIndex, (k, f) => new FilesGroup(Path.Combine(folder, $"{noext}_{k}{ext}"), f.ToList())).ToList();
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
