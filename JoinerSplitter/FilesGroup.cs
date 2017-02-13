namespace JoinerSplitter
{
    using System.Collections.Generic;

    public class FilesGroup
    {
        public FilesGroup(string filePath, ICollection<VideoFile> files)
        {
            FilePath = filePath;
            Files = files;
        }

        public string FilePath { get; private set; }

        public ICollection<VideoFile> Files { get; private set; }
    }
}