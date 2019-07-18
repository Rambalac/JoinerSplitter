namespace JoinerSplitter
{
    public abstract class ParallelProgress
    {
        internal abstract double Current { get; }

        internal bool Done { get; set; }

        internal ParallelProgressRoot Root { get; set; }
    }
}
