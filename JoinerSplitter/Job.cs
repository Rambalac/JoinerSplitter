namespace JoinerSplitter
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;

    [DataContract]
    public class Job : INotifyPropertyChanged
    {
        [DataMember]
        private readonly ObservableCollection<VideoFile> files = new ObservableCollection<VideoFile>();

        private EncodingPreset encoding;

        [DataMember]
        private string outputFolder;

        [DataMember]
        private string outputName;

        public Job()
        {
            files.CollectionChanged += Files_CollectionChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [DataMember]
        public EncodingPreset Encoding
        {
            get => encoding;
            set
            {
                encoding = value;
                OnPropertyChanged(nameof(encoding));
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        public ICollection<FilesGroup> FileGroups
        {
            get
            {
                if (!Files.Any())
                {
                    return new FilesGroup[] { };
                }

                var folder = OutputFolder ?? Path.GetDirectoryName(Files.First().FilePath) ?? throw new NullReferenceException();
                if (Files.Select(f => f.GroupIndex).Distinct().Count() == 1)
                {
                    return new[] { new FilesGroup(Path.Combine(folder, OutputName), Files, Encoding?.ComplexFilter,  Encoding?.OutputEncoding) };
                }

                var noext = Path.GetFileNameWithoutExtension(OutputName);
                var ext = Path.GetExtension(OutputName);
                return Files.GroupBy(f => f.GroupIndex, (k, f) => new FilesGroup(Path.Combine(folder, $"{noext}_{k}{ext}"), f.ToList(), Encoding?.ComplexFilter,  Encoding?.OutputEncoding)).ToList();
            }
        }

        public ObservableCollection<VideoFile> Files => files;

        public bool HasMoreGroupsThanOne => Files.Select(f => f.GroupIndex).Distinct().Count() > 1;

        public string JobFilePath { get; set; } = string.Empty;

        public EncodingPreset OriginalEncoding { get; set; }

        public string OutputFolder
        {
            get => outputFolder;

            set
            {
                outputFolder = value;
                OnPropertyChanged(nameof(OutputFolder));
            }
        }

        public string OutputName
        {
            get => outputName;

            set
            {
                outputName = value;
                OnPropertyChanged(nameof(OutputName));
            }
        }

        public bool Changed { get; set; }

        private void Files_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasMoreGroupsThanOne));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            Changed = true;
        }
    }
}