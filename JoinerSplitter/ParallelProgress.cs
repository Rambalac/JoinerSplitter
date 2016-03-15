using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JoinerSplitter
{
    public abstract class ParallelProgress
    {
        internal abstract double Current { get; }

        internal bool Done { get; set; } = false;

        internal ParallelProgressRoot Root { get; set; } = null;
    }
}
