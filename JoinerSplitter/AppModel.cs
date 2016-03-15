using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;

namespace JoinerSplitter
{
    public class AppModel : INotifyPropertyChanged
    {
        private VideoFile currentFile;

        private Job currentJob;

        public AppModel()
        {
            currentJob = new Job();
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

        private async Task<VideoFile> CreateVideoFileObject(string path)
        {
            var duration = await FFMpeg.Instance.GetDuration(path);
            try
            {
                var keyFrames = await FFMpeg.Instance.GetKeyFrames(path);
                return new VideoFile(path, duration, keyFrames);
            }
            catch (InvalidOperationException)
            {
                return new VideoFile(path, duration, null);
            }
        }

        public async Task AddFiles(string[] files)
        {
            await AddFiles(files, null, -1);
        }

        public async Task AddFiles(string[] files, VideoFile before, int groupIndex)
        {
            var Error = new List<string>();
            var lastFile = CurrentJob.Files.LastOrDefault();
            var beforeIndex = before != null ? CurrentJob.Files.IndexOf(before) : -1;
            if (groupIndex < 0) groupIndex = lastFile?.GroupIndex ?? 0;
            foreach (var filepath in files)
            {
                try
                {
                    var file = await CreateVideoFileObject(filepath);
                    file.GroupIndex = groupIndex;
                    if (before == null)
                        CurrentJob.Files.Add(file);
                    else
                    {
                        CurrentJob.Files.Insert(beforeIndex++, file);
                    }
                }
                catch (Exception)
                {
                    Error.Add(Path.GetFileName(filepath));
                }
            }
            if (Error.Any())
            {
                if (files.Length == Error.Count)
                    throw new InvalidOperationException("None of files can be exported by ffmpeg:\r\n" + string.Join("\r\n", Error.Select(s => "  " + s)));

                throw new InvalidOperationException("Some files can not be exported by ffmpeg:\r\n" + string.Join("\r\n", Error.Select(s => "  " + s)));
            }

            if (string.IsNullOrEmpty(CurrentJob.OutputName) && files.Length > 0)
                CurrentJob.OutputName = Path.GetFileNameWithoutExtension(files[0]) + ".out" + Path.GetExtension(files[0]);
        }

        private void NormalizeGroups()
        {
            var jobFiles = CurrentJob.Files;
            if (jobFiles.Count == 0) return;
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

        internal void DuplicateVideos(IList selectedItems)
        {
            var selected = selectedItems.Cast<VideoFile>().ToList();
            int insertIndex = selected.Select(v => CurrentJob.Files.IndexOf(v)).Max() + 1;
            foreach (var file in selected)
                CurrentJob.Files.Insert(insertIndex++, new VideoFile(file));
            NormalizeGroups();
        }

        public void DeleteVideos(IList selectedItems)
        {
            var jobFiles = CurrentJob.Files;
            foreach (var file in selectedItems.Cast<VideoFile>().ToList())
                jobFiles.Remove(file);
            NormalizeGroups();
        }

        public void MoveFiles(VideoFile[] files, VideoFile before, int groupIndex)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));
            var jobFiles = CurrentJob.Files;
            if (before != null)
            {
                int ind = jobFiles.IndexOf(before);
                while (before != null && files.Contains(before))
                {
                    ind++;
                    before = (ind < jobFiles.Count) ? jobFiles[ind] : null;
                }
            }
            foreach (var file in files)
                jobFiles.Remove(file);
            int insertIndex = (before != null) ? jobFiles.IndexOf(before) : jobFiles.Count;
            var lastFile = jobFiles.LastOrDefault();
            if (groupIndex < 0) groupIndex = lastFile?.GroupIndex ?? 0;
            foreach (var file in files)
            {
                jobFiles.Insert(insertIndex++, file);
                file.GroupIndex = groupIndex;
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
                if (fileindex < jobFiles.Count - 1 && !selected.Contains(jobFiles[fileindex + 1]))
                {
                    if (jobFiles[fileindex + 1].GroupIndex > file.GroupIndex)
                        file.GroupIndex = jobFiles[fileindex + 1].GroupIndex;
                    else
                        jobFiles.Move(fileindex, fileindex + 1);
                }
            }

            NormalizeGroups();
        }

        public async Task OpenJob(string path)
        {
            CurrentJob = await Task.Run(() =>
            {
                using (var stream = File.OpenRead(path))
                {
                    Environment.CurrentDirectory = Path.GetDirectoryName(path);
                    var ser = new DataContractJsonSerializer(typeof(Job));
                    var result = (Job)ser.ReadObject(stream);
                    result.JobFilePath = path;
                    return result;
                }
            });
        }

        public void MoveVideosUp(IList selectedItems)
        {
            var jobFiles = CurrentJob.Files;
            var selected = selectedItems.Cast<VideoFile>().OrderBy(f => jobFiles.IndexOf(f)).ToList();
            for (var i = 0; i < selected.Count; i++)
            {
                var file = selected[i];
                var fileindex = jobFiles.IndexOf(file);
                if (fileindex > 0 && !selected.Contains(jobFiles[fileindex - 1]))
                {
                    if (jobFiles[fileindex - 1].GroupIndex < file.GroupIndex)
                        file.GroupIndex = jobFiles[fileindex - 1].GroupIndex;
                    else
                        jobFiles.Move(fileindex, fileindex - 1);
                }
            }

            NormalizeGroups();
        }

        public async Task SaveJob(string path)
        {
            await Task.Run(() =>
            {
                using (var stream = File.Create(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(Job));
                    ser.WriteObject(stream, CurrentJob);
                }
                CurrentJob.JobFilePath = path;
            });
        }

        public void SplitCurrentVideo(double currentTime)
        {
            var currentIndex = CurrentJob.Files.IndexOf(CurrentFile);
            var splitTime = CurrentFile.KeyFrames?.Where(f => f > currentTime).DefaultIfEmpty(CurrentFile.Duration).First() ?? currentTime;
            if (splitTime <= CurrentFile.Start || splitTime >= CurrentFile.End) return;

            var newFile = new VideoFile(CurrentFile)
            {
                Start = splitTime,
                GroupIndex = CurrentFile.GroupIndex + 1
            };

            CurrentFile.End = splitTime;
            CurrentJob.Files.Insert(currentIndex + 1, newFile);
            for (var i = currentIndex + 2; i < CurrentJob.Files.Count; i++)
                CurrentJob.Files[i].GroupIndex += 1;
        }

        public void SplitGroup()
        {
            var currentIndex = CurrentJob.Files.IndexOf(CurrentFile);
            if (currentIndex == 0) return;

            for (var i = currentIndex; i < CurrentJob.Files.Count; i++)
                CurrentJob.Files[i].GroupIndex += 1;

            NormalizeGroups();
        }
    }
}