using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JoinerSplitter
{
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
