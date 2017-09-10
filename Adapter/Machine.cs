using System;
using System.Collections.Generic;
using VSCodeDebugger;

namespace Z80Machine
{
    public class Machine
    {
        // the class used to actually retrieve the data can be abstracted out at some point
        // but for now we'll tie directly to the ZEsarUX connection class
        public IDebuggerConnection Connection;

        public Machine( IDebuggerConnection pConnection )
        {
            Connection = pConnection;
            _registers = new Registers(this);
            _memory    = new Memory(this);
            _stack     = new Stack(this);
        }


        /////////////////
        // actions

        public bool Start()
        {
            return Connection.Start();
        }

        public bool Pause()
        {
            return Connection.Pause();
        }

        public bool Continue()
        {
            return Connection.Continue();
        }

        public bool StepOver()
        {
            return Connection.StepOver();
        }

        public bool Step()
        {
            return Connection.Step();
        }

        public bool Stop()
        {
            return Connection.Stop();
        }


        /////////////////
        // registers

        Registers _registers;
        public Registers Registers
        {
            get { return _registers; }
        }

        public void RefreshRegisters()
        {
            Connection.GetRegisters( _registers );
        }

        public void SetRegister( string pRegister, string pValue )
        {
            ushort value;

            try
            {
                if( pValue.StartsWith( "$" ) )
                    value = Convert.ToUInt16( pValue.Substring( 1 ), 16 );
                else if( pValue.StartsWith( "0x" ) )
                    value = Convert.ToUInt16( pValue.Substring( 2 ), 16 );
                else
                    value = Convert.ToUInt16( pValue );

                Connection.SetRegister( _registers, pRegister, value );
            }
            catch( Exception e )
            {
                Log.Write( Log.Severity.Error, e.ToString() );
                throw;
            }

        }



        /////////////////
        // memory

        Memory _memory;
        public Memory Memory
        {
            get { return _memory; }
        }

        public void RefreshMemoryPages()
        {
            Connection.GetMemoryPages( _memory );
        }

        public string GetMemory( ushort pAddress, int pLength )
        {
            return Connection.GetMemory( pAddress, pLength );
        }


        /////////////////
        // stack

        Stack _stack;
        public Stack Stack
        {
            get { return _stack; }
        }

        public void RefreshStack()
        {
            Connection.GetStack( _stack );
        }
    }


    public class Registers
    {
        public ushort PC;
        public ushort SP;

        public byte   A;
        public ushort BC;
        public ushort DE;
        public ushort HL;

        public byte   AltA;
        public ushort AltBC;
        public ushort AltDE;
        public ushort AltHL;

        public ushort IX;
        public ushort IY;

        public byte   I;
        public byte   R;

        Machine _machine;
        public Registers( Machine pMachine )
        {
            _machine = pMachine;
        }

        public void Refresh()
        {
            _machine.RefreshRegisters();
        }

        public void Set( string pRegister, string pValue )
        {
            _machine.SetRegister( pRegister, pValue );
        }
    }


    public class Memory
    {
        public bool PagingEnabled;

        Dictionary<int, Bank> _banks = new Dictionary<int, Bank>();
        List<Map> _pages = new List<Map>();

        Machine _machine;
        public Memory( Machine pMachine )
        {
            _machine = pMachine;
        }

        public Bank GetBankAtAddress( ushort pAddress )
        {
            foreach( var page in _pages )
                if( pAddress >= page.Min && pAddress <= page.Max )
                    return page.Bank;

            return null;
        }

        public void ClearMemoryMap()
        {
            _pages.Clear();
        }

        public void SetAddressBank( ushort pMin, ushort pMax, Bank pBank )
        {
            _pages.Add( new Map() { Min = pMin, Max = pMax, Bank = pBank } );
        }

        public Bank Bank( int pID )
        {
            Bank result;

            if( !_banks.TryGetValue( pID, out result ) )
                result = new Bank() { ID = pID };

            return result;
        }

        public Bank ROM( int pID )
        {
            return Bank( -2 - pID );
        }

        public Bank RAM( int pID )
        {
            return Bank( pID );
        }

        public string Get( ushort pAddress, int pLength )
        {
            return _machine.GetMemory( pAddress, pLength );
        }
    }

    public class Bank
    {
        // 0, 1, 2 etc = bank #
        // -1 = default
        // -2 = rom 1
        // -3 = rom 2
        public int ID;
        public ushort BaseAddress;
    }

    public class Map
    {
        public ushort Min;
        public ushort Max;
        public Bank   Bank;
    }

    public class Stack : List<ushort>
    {
        Machine _machine;
        public Stack( Machine pMachine )
        {
            _machine = pMachine;
        }

        public void Refresh()
        {
            _machine.RefreshStack();
        }
    }
}