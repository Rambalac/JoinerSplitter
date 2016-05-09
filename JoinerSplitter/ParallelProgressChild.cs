namespace JoinerSplitter
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class ParallelProgressChild : ParallelProgress
    {
        private double current;

        internal override double Current
        {
            get
            {
                lock (this)
                {
                    return current;
                }
            }
        }

        public new void Done()
        {
            base.Done = true;
        }

        public void Update(double current)
        {
            lock (this)
            {
                this.current = current;
            }

            Root.Update();
        }
    }
}
