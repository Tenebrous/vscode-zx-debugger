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


        public virtual void RefreshStack( Stack pStack )
        {
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

        public virtual List<InstructionLine> Disassemble( ushort pAddress, int pCount, List<InstructionLine> pOutput = null )
        {
            return pOutput;
        }

        public virtual string CustomCommand( string pCommand )
        {
            return null;
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
        public ushort Address;
        public int    FileLine;
        public Disassembler.Instruction Instruction;
    }

    public class Meta
    {
        public bool CanSetRegisters;
        public bool CanEvaluate;
    }
}
 