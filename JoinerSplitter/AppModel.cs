namespace JoinerSplitter
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization.Json;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Properties;

    public class AppModel : INotifyPropertyChanged
    {
        private static readonly IList<EncodingPreset> DefaultEncoderPresets = Settings.Default.DefaultEncodingPresets
                                                                                      .Cast<string>().SelectGroups(3).Select(
                                                                                           i => new EncodingPreset { Name = i[0], OutputEncoding = i[1], ComplexFilter = i[2] }).ToList();

        private VideoFile currentFile;

        private Job currentJob;

        public AppModel()
        {
            currentJob = new Job();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public IEnumerable<EncodingPreset> ComboBoxEncoderPresets => CurrentJob.OriginalEncoding != null
                                                                         ? new[] { CurrentJob.OriginalEncoding }.Concat(EncoderPresets)
                                                                         : EncoderPresets;

        public VideoFile CurrentFile
        {
            get => currentFile;

            set
            {
                currentFile = value;
                OnPropertyChanged(nameof(CurrentFile));
                OnPropertyChanged(nameof(HasCurrentFile));
            }
        }

        public Uri CurrentFileUri => CurrentFile?.FileUri ?? new Uri(string.Empty);

        public Job CurrentJob
        {
            get => currentJob;

            set
            {
                currentJob = value;
                OnPropertyChanged(nameof(CurrentJob));
            }
        }

        public ObservableCollection<EncodingPreset> EncoderPresets { get; set; } =
            new ObservableCollection<EncodingPreset>(Settings.Default?.EncodingPresets1 ?? DefaultEncoderPresets);

        public bool HasCurrentFile => CurrentFile != null;

        public string OutputFolder => SelectedOutputFolder ?? CurrentJob.OutputFolder;

        public string SelectedOutputFolder
        {
            get => Settings.Default?.OutputFolder;
            set
            {
                Settings.Default.OutputFolder = value;
                SaveSettings();
                OnPropertyChanged(nameof(OutputFolder));
            }
        }

        public async Task AddFiles(string[] files)
        {
            await AddFiles(files, null, -1);
        }

        public async Task AddFiles(string[] files, VideoFile before, int groupIndex)
        {
            var error = new List<string>();
            var lastFile = CurrentJob.Files.LastOrDefault();
            var beforeIndex = before != null ? CurrentJob.Files.IndexOf(before) : -1;
            if (groupIndex < 0)
            {
                groupIndex = lastFile?.GroupIndex ?? 0;
            }

            foreach (var filepath in files)
            {
                try
                {
                    var file = await CreateVideoFileObject(filepath);
                    file.GroupIndex = groupIndex;
                    if (before == null)
                    {
                        CurrentJob.Files.Add(file);
                    }
                    else
                    {
                        CurrentJob.Files.Insert(beforeIndex++, file);
                    }
                }
                catch (Exception)
                {
                    error.Add(Path.GetFileName(filepath));
                }
            }

            if (error.Any())
            {
                if (files.Length == error.Count)
                {
                    throw new InvalidOperationException("None of files can be exported by ffmpeg:\r\n" + string.Join("\r\n", error.Select(s => "  " + s)));
                }

                throw new InvalidOperationException("Some files can not be exported by ffmpeg:\r\n" + string.Join("\r\n", error.Select(s => "  " + s)));
            }

            if (string.IsNullOrEmpty(CurrentJob.OutputName) && (files.Length > 0))
            {
                CurrentJob.OutputName = Path.GetFileNameWithoutExtension(files[0]) + ".out" + Path.GetExtension(files[0]);
            }
        }

        public void DeleteVideos(IList selectedItems)
        {
            var jobFiles = CurrentJob.Files;
            foreach (var file in selectedItems.Cast<VideoFile>().ToList())
            {
                jobFiles.Remove(file);
            }

            NormalizeGroups();
        }

        public void MoveFiles(VideoFile[] files, VideoFile before, int groupIndex)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            var jobFiles = CurrentJob.Files;
            if (before != null)
            {
                var ind = jobFiles.IndexOf(before);
                while ((before != null) && files.Contains(before))
                {
                    ind++;
                    before = ind < jobFiles.Count ? jobFiles[ind] : null;
                }
            }

            foreach (var file in files)
            {
                jobFiles.Remove(file);
            }

            var insertIndex = before != null ? jobFiles.IndexOf(before) : jobFiles.Count;
            var lastFile = jobFiles.LastOrDefault();
            if (groupIndex < 0)
            {
                groupIndex = lastFile?.GroupIndex ?? 0;
            }

            foreach (var file in files)
            {
                jobFiles.Insert(insertIndex++, file);
                file.GroupIndex = groupIndex;
            }

            NormalizeGroups();
        }

        public void MoveVideosUp(IList selectedItems)
        {
            var jobFiles = CurrentJob.Files;
            var selected = selectedItems.Cast<VideoFile>().OrderBy(f => jobFiles.IndexOf(f)).ToList();
            foreach (var file in selected)
            {
                var fileindex = jobFiles.IndexOf(file);
                if ((fileindex > 0) && !selected.Contains(jobFiles[fileindex - 1]))
                {
                    if (jobFiles[fileindex - 1].GroupIndex < file.GroupIndex)
                    {
                        file.GroupIndex = jobFiles[fileindex - 1].GroupIndex;
                    }
                    else
                    {
                        jobFiles.Move(fileindex, fileindex - 1);
                    }
                }
            }

            NormalizeGroups();
        }

        public async Task OpenJob(string path)
        {
            CurrentJob = await Task.Run(
                             () =>
                             {
                                 Environment.CurrentDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;

                                 var result = JsonConvert.DeserializeObject<Job>(File.ReadAllText(path));
                                 result.JobFilePath = path;
                                 if (result.Encoding != null)
                                 {
                                     result.OriginalEncoding = new EncodingPreset
                                     {
                                         Name = result.Encoding.Name,
                                         OutputEncoding = result.Encoding.OutputEncoding,
                                         ComplexFilter = result.Encoding.ComplexFilter,
                                         DisplayName = $"{result.Encoding.Name.Trim()} (original)",
                                     };
                                     result.Encoding = result.OriginalEncoding;
                                     result.Changed = false;
                                 }

                                 return result;
                             });
            OnPropertyChanged(nameof(ComboBoxEncoderPresets));
        }

        public void SaveEncoders()
        {
            Settings.Default.EncodingPresets1 = new EncodingPresetsCollection(EncoderPresets);
            OnPropertyChanged(nameof(ComboBoxEncoderPresets));
            SaveSettings();
        }

        public async Task SaveJob(string path)
        {
            try
            {
                if (CurrentJob == null || !CurrentJob.Files.Any())
                {
                    throw new Exception("Wrong job");
                }

                var tmpPath = path + ".tmp";
                string contents = JsonConvert.SerializeObject(CurrentJob);
                File.WriteAllText(tmpPath, contents);
                var saveFileInfo = new FileInfo(tmpPath);
                if (saveFileInfo.Length == 0)
                {
                    throw new Exception("Wrong job length");
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tmpPath, path);

                CurrentJob.JobFilePath = path;
                CurrentJob.Changed = false;
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }

        public void SaveSettings()
        {
            Settings.Default.Save();
        }

        public void SplitCurrentVideo(double currentTime)
        {
            var currentIndex = CurrentJob.Files.IndexOf(CurrentFile);
            var splitTime = CurrentFile.KeyFrames?.Where(f => f > currentTime).DefaultIfEmpty(CurrentFile.Duration).First() ?? currentTime;
            if ((splitTime <= CurrentFile.Start) || (splitTime >= CurrentFile.End))
            {
                return;
            }

            var newFile = new VideoFile(CurrentFile)
            {
                Start = splitTime,
                GroupIndex = CurrentFile.GroupIndex
            };

            CurrentFile.End = splitTime;
            CurrentJob.Files.Insert(currentIndex + 1, newFile);
        }

        public void SplitGroup()
        {
            var currentIndex = CurrentJob.Files.IndexOf(CurrentFile);
            if (currentIndex == 0)
            {
                return;
            }

            for (var i = currentIndex; i < CurrentJob.Files.Count; i++)
            {
                CurrentJob.Files[i].GroupIndex += 1;
            }

            NormalizeGroups();
        }

        internal void DuplicateVideos(IList selectedItems)
        {
            var selected = selectedItems.Cast<VideoFile>().ToList();
            var insertIndex = selected.Select(v => CurrentJob.Files.IndexOf(v)).Max() + 1;
            foreach (var file in selected)
            {
                CurrentJob.Files.Insert(insertIndex++, new VideoFile(file));
            }

            NormalizeGroups();
        }

        internal void MoveVideosDown(IList selectedItems)
        {
            var jobFiles = CurrentJob.Files;
            var selected = selectedItems.Cast<VideoFile>().OrderBy(f => jobFiles.IndexOf(f)).ToList();
            for (var i = selected.Count - 1; i >= 0; i--)
            {
                var file = selected[i];
                var fileindex = jobFiles.IndexOf(file);
                if ((fileindex < jobFiles.Count - 1) && !selected.Contains(jobFiles[fileindex + 1]))
                {
                    if (jobFiles[fileindex + 1].GroupIndex > file.GroupIndex)
                    {
                        file.GroupIndex = jobFiles[fileindex + 1].GroupIndex;
                    }
                    else
                    {
                        jobFiles.Move(fileindex, fileindex + 1);
                    }
                }
            }

            NormalizeGroups();
        }

        private async Task<VideoFile> CreateVideoFileObject(string path)
        {
            var duration = await FFMpeg.Instance.GetDuration(path);

            return new VideoFile(path, duration);
        }

        private void NormalizeGroups()
        {
            var jobFiles = CurrentJob.Files;
            if (jobFiles.Count == 0)
            {
                return;
            }

            var curindex = 0;
            var lastIndex = jobFiles[0].GroupIndex;
            foreach (var file in jobFiles)
            {
                if (file.GroupIndex != lastIndex)
                {
                    lastIndex = file.GroupIndex;
                    curindex++;
                }

                file.GroupIndex = curindex;
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}