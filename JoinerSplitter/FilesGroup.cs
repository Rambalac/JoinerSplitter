namespace JoinerSplitter
{
    using System.Collections.Generic;

    public class FilesGroup
    {
        public FilesGroup(string filePath, ICollection<VideoFile> files, string outputEncoding)
        {
            FilePath = filePath;
            Files = files;
            OutputEncoding = outputEncoding;
        }

        public string FilePath { get; }

        public ICollection<VideoFile> Files { get; }

        public string OutputEncoding { get; }
    }
}