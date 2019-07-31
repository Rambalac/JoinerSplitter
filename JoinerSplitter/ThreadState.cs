namespace JoinerSplitter
{
    public static class ThreadState
    {
        public static void KeepAwake()
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.ExecutionState.EsSystemRequired);
        }

        public static void AllowSleep()
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.ExecutionState.EsContinuous);
        }

        public static void PreventSleep()
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.ExecutionState.EsContinuous | NativeMethods.ExecutionState.EsSystemRequired);
        }
    }
}