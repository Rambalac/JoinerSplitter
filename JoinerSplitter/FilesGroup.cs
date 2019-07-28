namespace JoinerSplitter
{
    using System.Collections.Generic;

    public class FilesGroup
    {
        public FilesGroup(string filePath, ICollection<VideoFile> files, string complexFilter, string outputEncoding)
        {
            FilePath = filePath;
            Files = files;
            ComplexFilter = complexFilter;
            OutputEncoding = outputEncoding;
        }

        public string FilePath { get; }

        public ICollection<VideoFile> Files { get; }

        public string ComplexFilter { get; }

        public string OutputEncoding { get; }
    }
}