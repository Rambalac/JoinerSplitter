using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using static System.FormattableString;

namespace JoinerSplitter
{

    public class FilesGroup
    {
        public string FilePath { get; private set; }
        public ICollection<VideoFile> Files { get; private set; }

        public FilesGroup(string filePath, ICollection<VideoFile> files)
        {
            FilePath = filePath;
            Files = files;
        }
    }
}