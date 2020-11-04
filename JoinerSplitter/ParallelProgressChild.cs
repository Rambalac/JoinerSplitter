using System;
using System.Windows;

namespace JoinerSplitter
{
    public class ParallelProgressChild : IParallelProgress
    {
        private double current;
        private double? duration;

        public event EventHandler Update;

        public double Current
        {
            get
            {
                lock (this)
                {
                    return current;
                }
            }
        }

        public double? Duration
        {
            get
            {
                lock (this)
                {
                    return duration;
                }
            }
        }

        public TimeSpan Estimated => throw new NotImplementedException();

        public IParallelProgress Parent { get; set; }

        public bool Done { get; set; }

        public void OnUpdate()
        {
            Update?.Invoke(this, null);
        }

        public void Set(double newCurrent)
        {
            lock (this)
            {
                current = newCurrent;
            }

            OnUpdate();
            Parent?.OnUpdate();
        }

        public void SetDuration(double time)
        {
            lock (this)
            {
                duration = time;
            }
        }
    }
}
