using System.Collections.Generic;
using Spectrum;

namespace VSCodeDebugger
{
    /// <summary>
    /// Abstraction of interface to a Spectrum debugger
    /// </summary>
    public class Debugger
    {
        /// <summary>
        /// Returns details on what this debugger provides
        /// </summary>
        /// <returns></returns>
        public virtual Meta GetMeta()
        {
            return new Meta();
        }

        /// <summary>
        /// Retrieve the current state of the registers and update the provided pRegisters class
        /// </summary>
        /// <param name="pRegisters">Class to be updated</param>
        public virtual void GetRegisters( Registers pRegisters )
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

        public virtual void GetMemoryPages( Memory pMemory )
        {
        }

        public virtual string GetMemory( ushort pAddress, int pLength )
        {
            return null;
        }

        public virtual void GetStack( Stack pStack )
        {
        }

        public virtual bool Connect()
        {
            return false;
        }

        public virtual bool Disconnect()
        {
            return false;
        }

        public virtual bool IsConnected { get; }
               
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

        public virtual List<AssemblyLine> Disassemble( ushort pAddress, int pCount, List<AssemblyLine> pOutput = null )
        {
            return pOutput;
        }

        public virtual StateChange GetLastStateChange()
        {
            return StateChange.None;
        }

        public virtual void ClearLastStateChange()
        {
        }
    }

    public class AssemblyLine
    {
        public ushort Address;
        public byte[] Opcodes;
        public string Text;
    }

    public enum StateChange
    {
        None,
        Stopped,
        Started
    }

    public class Meta
    {
        public bool CanSetRegisters;
    }
}
