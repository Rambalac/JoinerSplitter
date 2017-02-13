namespace JoinerSplitter
{
    using System;

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
