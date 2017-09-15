﻿using System;
using System.Collections.Generic;
using Spectrum;

// todo: decide between throwing exceptions or returning success/failure, and make it consistent

namespace VSCodeDebugger
{
    /// <summary>
    /// Abstraction of interface to a Spectrum debugger
    /// </summary>
    public class Debugger
    {
        public Action OnPause;
        public Action OnContinue;

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

        public virtual string ReadMemory( ushort pAddress, int pLength )
        {
            return null;
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

        public virtual string CustomCommand( string pCommand )
        {
            return null;
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
