using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using static System.FormattableString;

namespace JoinerSplitter
{
    [DataContract]
    public class Job : INotifyPropertyChanged
    {
        [DataMember]
        private readonly ObservableCollection<VideoFile> files = new ObservableCollection<VideoFile>();

        [DataMember]
        private string outputFolder;

        [DataMember]
        private string outputName;

        public Job()
        {
            files.CollectionChanged += Files_CollectionChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICollection<FilesGroup> FileGroups
        {
            get
            {
                if (!Files.Any())
                {
                    return new FilesGroup[] { };
                }

                var folder = OutputFolder ?? Path.GetDirectoryName(Files.First().FilePath);
                if (Files.Select(f => f.GroupIndex).Distinct().Count() == 1)
                {
                    return new FilesGroup[] { new FilesGroup(Path.Combine(folder, OutputName), Files) };
                }

                var noext = Path.GetFileNameWithoutExtension(OutputName);
                var ext = Path.GetExtension(OutputName);
                return Files.GroupBy(f => f.GroupIndex, (k, f) => new FilesGroup(Path.Combine(folder, Invariant($"{noext}_{k}{ext}")), f.ToList())).ToList();
            }
        }

        public ObservableCollection<VideoFile> Files => files;

        public bool HasMoreGroupsThanOne => Files.Select(f => f.GroupIndex).Distinct().Count() > 1;

        public string JobFilePath { get; set; } = string.Empty;

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

        private void Files_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasMoreGroupsThanOne));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}