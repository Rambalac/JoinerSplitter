using System;

namespace JoinerSplitter
{
    public interface IParallelProgress
    {
        event EventHandler Update;

        double Current { get; }

        bool Done { get; }

        TimeSpan Estimated { get; }

        IParallelProgress Parent { get; set; }

        void OnUpdate();
    }
}
