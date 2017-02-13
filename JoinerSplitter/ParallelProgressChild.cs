namespace JoinerSplitter
{
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
