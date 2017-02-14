namespace OmniSharp.Utilities
{
    public struct ProcessExitStatus
    {
        public static ProcessExitStatus Empty { get; } = new ProcessExitStatus();

        public int Code { get; }
        public bool Started { get; }
        public bool TimedOut { get; }

        public bool Succeeded => this.Code == 0;
        public bool Failed => this.Code != 0 || !Started || TimedOut;

        public ProcessExitStatus(int code, bool started = true, bool timedOut = false)
        {
            this.Code = code;
            this.Started = started;
            this.TimedOut = timedOut;
        }

        public override string ToString()
        {
            var suffix = string.Empty;
            if (!Started)
            {
                suffix = " (not started)";
            }
            else if (TimedOut)
            {
                suffix = " (timed out)";
            }

            return Code.ToString() + suffix;
        }
    }
}