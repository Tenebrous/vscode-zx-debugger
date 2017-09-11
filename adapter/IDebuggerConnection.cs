using Z80Machine;

namespace VSCodeDebugger
{
    public interface IDebuggerConnection
    {
        void GetRegisters( Registers pRegisters );
        void SetRegister( Registers pRegisters, string pRegister, ushort pValue );

        void GetMemoryPages( Memory pMemory );
        string GetMemory( ushort pAddress, int pLength );

        void GetStack( Stack pStack );

        bool Start();
        bool Stop();

        bool Pause();
        bool Continue();

        bool StepOver();
        bool Step();
    }
}
