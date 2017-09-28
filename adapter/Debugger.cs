﻿using System;
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
        
        /// <summary>
        /// Ask the device for information regarding the current memory mapping
        /// e.g. which pages are in which slots
        /// </summary>
        /// <param name="pMemory">Memory class to be updated with the new configuration</param>
        public virtual void RefreshMemoryPages( Memory pMemory )
        {
        }

        /// <summary>
        /// Read the device's memory at the address specified into the provided buffer
        /// </summary>
        /// <param name="pAddress">Memory address</param>
        /// <param name="pBuffer">Byte array to be filled from the device</param>
        /// <param name="pLength">Number of bytes to retrieve</param>
        /// <returns>Number of bytes which were successfully read</returns>
        public virtual int ReadMemory( ushort pAddress, byte[] pBuffer, int pLength )
        {
            return 0;
        }


        /// <summary>
        /// Connect to the device
        /// </summary>
        /// <returns>true if connection was successful</returns>
        public virtual bool Connect()
        {
            return false;
        }


        /// <summary>
        /// Disconnect from the device
        /// </summary>
        /// <returns>true if disconnection was successful</returns>
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
               
        /// <summary>
        /// Tell the device to pause execution
        /// </summary>
        /// <returns>true if the request succeeded</returns>
        public virtual bool Pause()
        {
            return false;
        }

        /// <summary>
        /// Tell the device to continue execution
        /// </summary>
        /// <returns>true if the request succeeded</returns>
        public virtual bool Continue()
        {
            return false;
        }

        /// <summary>
        /// Tell the device to execute the next instruction, and return control when it has finished.
        /// For example, in the case of a CALL, the subroutine will be executed and control will be returned when the subroutine has completed.
        /// </summary>
        /// <returns>true if the request succeeded</returns>
        public virtual bool StepOver()
        {
            return false;
        }

        /// <summary>
        /// Tell the device to execute the next instruction.
        /// </summary>
        /// <returns>true if the request succeeded</returns>
        public virtual bool Step()
        {
            return false;
        }

        /// <summary>
        /// Tell the device to return from the current subroutine.
        /// </summary>
        /// <returns>true if the request succeeded</returns>
        public virtual bool StepOut()
        {
            return false;
        }

        /// <summary>
        /// Update the device with the specified breakpoints.  The list contains the entire list of required breakpoints.
        /// </summary>
        /// <param name="pBreakpoints">List of breakpoints</param>
        /// <returns>true if the request succeeded</returns>
        public virtual bool SetBreakpoints( Breakpoints pBreakpoints )
        {
            return false;
        }

        /// <summary>
        /// Add a single breakpoint.
        /// </summary>
        /// <param name="pBreakpoints">List of all breakpoints, excluding the one to be added.</param>
        /// <param name="pBreakpoint">New breakpoint to be added</param>
        /// <returns>true if the request succeeded</returns>
        public virtual bool SetBreakpoint( Breakpoints pBreakpoints, Breakpoint pBreakpoint )
        {
            return false;
        }

        /// <summary>
        /// Remove all breakpoints.
        /// </summary>
        /// <param name="pBreakpoints">List of previous breakpoints</param>
        /// <returns>true if the request succeeded</returns>
        public virtual bool RemoveBreakpoints( Breakpoints pBreakpoints )
        {
            return false;
        }

        /// <summary>
        /// Remove the specified breakpoint.
        /// </summary>
        /// <param name="pBreakpoints">List of all breakpoints, including the one to be removed</param>
        /// <param name="pBreakpoint">Breakpoint to be removed</param>
        /// <returns></returns>
        public virtual bool RemoveBreakpoint( Breakpoints pBreakpoints, Breakpoint pBreakpoint )
        {
            return false;
        }

        /// <summary>
        /// Send a custom command as a string to the device
        /// </summary>
        /// <param name="pCommand">Command to be executed</param>
        /// <param name="pResults">List of strings to be populated with the return values from the device - can be null, in which case a new list will be created</param>
        /// <returns>The list of string results - this will be a copy of pResults if it was provided</returns>
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

    /// <summary>
    /// A disassembled instruction
    /// </summary>
    public class InstructionLine
    {
        public BankID Bank;
        public ushort Address;
        public int FileLine;
        public Disassembler.Instruction Instruction;
        public Breakpoint Breakpoint;
    }

    /// <summary>
    /// Describes capabilities, abilities and functionality supported by the debugger
    /// </summary>
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
 