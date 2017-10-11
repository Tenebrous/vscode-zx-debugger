namespace ZXDebug
{
    /// <summary>
    /// Describes capabilities, abilities and functionality supported by the debugger
    /// </summary>
    public class ConnectionCaps
    {
        /// <summary>
        /// The debugger can change the value of registers
        /// </summary>
        public bool CanSetRegisters;

        /// <summary>
        /// The debugger can evaluate an arbitrary string and return a result
        /// </summary>
        public bool CanEvaluate;

        /// <summary>
        /// The debugger supports stepping out
        /// </summary>
        public bool CanStepOut;

        /// <summary>
        /// The debugger will ignore step over for 'ret' and 'jp' and will instead do a normal step
        /// </summary>
        public bool CanStepOverSelectively;

        /// <summary>
        /// Maximum number of breakpoints enabled at any one time
        /// </summary>
        public int MaxBreakpoints;
    }
}