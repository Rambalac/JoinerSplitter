namespace JoinerSplitter
{
    using System.Collections.Concurrent;
    using System.Linq;

    public class ParallelProgressContainer : ParallelProgress
    {
        private readonly ConcurrentBag<ParallelProgress> children = new ConcurrentBag<ParallelProgress>();
        private double total;

        internal override double Current
        {
            get
            {
                if (Done)
                {
                    return total;
                }

                double sum = children.Sum(p => p.Current);
                if (children.All(c => Done))
                {
                    total = sum;
                    Done = true;
                }

                return sum;
            }
        }

        public void Add(ParallelProgress p)
        {
            children.Add(p);
            p.Root = Root;
        }
    }
}
