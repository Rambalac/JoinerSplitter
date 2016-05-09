namespace JoinerSplitter
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class ParallelProgressRoot : ParallelProgressContainer
    {
        private readonly Action<double> progressUpdate;

        public ParallelProgressRoot(Action<double> progressUpdate)
        {
            this.progressUpdate = progressUpdate;
            Root = this;
        }

        internal void Update()
        {
            progressUpdate(Current);
        }
    }
}
