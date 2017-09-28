using System;
using System.Collections.Generic;
using System.Dynamic;
using Spectrum;

// todo: decide between throwing exceptions or returning success/failure, and make it consistent

namespace ZXDebug
{
    /// <summary>
    /// Abstraction of interface to a Spectrum debugger
    /// </summary>
    public abstract class Debugger
    {
        public delegate void PausedHandler();
        public event PausedHandler PausedEvent;

        public delegate void ContinuedHandler();
        public event ContinuedHandler ContinuedEvent;

        /// <summary>
        /// Returns details on what this debugger provides
        /// </summary>
        /// <returns></returns>
        public virtual Meta Meta => new Meta();

        /// <summary>
        /// Retrieve the current state of the registers and update the provided pRegisters class
        /// </summary>
        /// <param name="pRegisters">Class to be updated</param>
        public virtual void RefreshRegisters( Registers pRegisters )
        {
        }

        /// <summary>
        /// Set a specific register
        /// </summary>
        /// <param name="pRegisters">Class of registers to be modified</param>
        /// <param name="pRegister">Name of register to modify e.g. "HL", "D'"</param>
        /// <param name="pValue">New value to apply to the register</param>
        public virtual void SetRegister( Registers pRegisters, string pRegister, ushort pValue )
        {
        }
        
        public virtual void RefreshMemoryPages( Memory pMemory )
        {
        }

        public virtual int ReadMemory( ushort pAddress, byte[] pBuffer, int pLength )
        {
            return 0;
        }


        /// <summary>
        /// Connect to the debugger
        /// </summary>
        /// <returns>true if connection was successful</returns>
        public virtual bool Connect()
        {
            return false;
        }

        public virtual bool Disconnect()
        {
            return false;
        }

        /// <summary>
        /// Process any incoming messages or other regular updates
        /// </summary>
        /// <returns></returns>
        public virtual bool Process()
        {
            return true;
        }

        public virtual bool IsConnected { get; } = false;
               
        public virtual bool Pause()
        {
            return false;
        }

        public virtual bool Continue()
        {
            return false;
        }
               
        public virtual bool StepOver()
        {
            return false;
        }

        public virtual bool Step()
        {
            return false;
        }

        public virtual bool StepOut()
        {
            return false;
        }

        public virtual bool SetBreakpoints( Breakpoints pBreakpoints )
        {
            return false;
        }

        public virtual bool SetBreakpoint( Breakpoints pBreakpoints, Breakpoint pBreakpoint )
        {
            return false;
        }

        public virtual bool RemoveBreakpoints( Breakpoints pBreakpoints )
        {
            return false;
        }

        public virtual bool RemoveBreakpoint( Breakpoints pBreakpoints, Breakpoint pBreakpoint )
        {
            return false;
        }

        public virtual List<string> CustomCommand( string pCommand, List<string> pResults = null )
        {
            return pResults;
        }

        public void OnPaused()
        {
            PausedEvent?.Invoke();
        }

        public void OnContinued()
        {
            ContinuedEvent?.Invoke();
        }
    }

    public class InstructionLine
    {
        public BankID Bank;
        public ushort Address;
        public int FileLine;
        public Disassembler.Instruction Instruction;
        public Breakpoint Breakpoint;
    }

    public class Meta
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
        public bool CanStepOverSensibly;

        /// <summary>
        /// Maximum number of breakpoints enabled at any one time
        /// </summary>
        public int MaxBreakpoints;
    }
}
 