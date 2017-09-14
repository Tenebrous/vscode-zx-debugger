using System.Collections.Generic;
using Spectrum;

namespace VSCodeDebugger
{
    public interface IDebuggerConnection
    {
        void   GetRegisters( Registers pRegisters );
        void   SetRegister( Registers pRegisters, string pRegister, ushort pValue );

        void   GetMemoryPages( Memory pMemory );
        string GetMemory( ushort pAddress, int pLength );

        void   GetStack( Stack pStack );

        bool   Start();
        bool   Stop();
               
        bool   Pause();
        bool   Continue();
               
        bool   StepOver();
        bool   Step();
        void   Disassemble( ushort pAddress, int pCount, List<AssemblyLine> pOutput );
    }

    public class AssemblyLine
    {
        public ushort Address;
        public byte[] Opcodes;
        public string Text;
    }
}
