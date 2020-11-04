namespace JoinerSplitter
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;

    public class ParallelProgressContainer : IParallelProgress
    {
        private readonly ConcurrentBag<IParallelProgress> children = new ConcurrentBag<IParallelProgress>();
        private double total;

        public bool Done { get; set; }

        public TimeSpan Estimated => Current != 0 ? TimeSpan.FromSeconds((DateTimeOffset.UtcNow - startTime).TotalSeconds * (1 - Current) / Current) : TimeSpan.Zero;

        private readonly DateTimeOffset startTime = DateTimeOffset.UtcNow;

        public ParallelProgressContainer(Action<IParallelProgress> progressUpdate)
        {
            Update += (a, b) => progressUpdate(this);
        }

        public ParallelProgressContainer()
        {
            Update += (a, b) => Parent?.OnUpdate();
        }

        public event EventHandler Update;

        public double Current
        {
            get
            {
                if (Done)
                {
                    return total;
                }

                if (children.All(c => Done))
                {
                    total = 1;
                    Done = true;
                }

                return children.Average(p => p.Current);
            }
        }

        public IParallelProgress Parent { get; set; }

        public void Add(IParallelProgress p)
        {
            p.Parent = this;
            children.Add(p);
        }

        public void OnUpdate()
        {
            Update?.Invoke(this, null);
        }
    }
}
