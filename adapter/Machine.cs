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

        /// <summary>
        /// Request an update to the registers from the device
        /// </summary>
        public void Get()
        {
            _machine.Connection.GetRegisters( this );
        }


        /// <summary>
        /// Send a register update to the device
        /// </summary>
        /// <param name="pRegister"></param>
        /// <param name="pValue"></param>
        public void Set( string pRegister, string pValue )
        {
            _machine.Connection.SetRegister( this, pRegister, Format.Parse( pValue ) );
        }

        /// <summary>
        /// Get/Set the buffered value of the selected register
        /// </summary>
        /// <param name="pRegister"></param>
        /// <returns></returns>
        public ushort this[string pRegister]
        {
            get
            {
			    switch( pRegister )
			    {
				    case "A":   return (ushort) _machine.Registers.A;
                                            
				    case "HL":  return (ushort) _machine.Registers.HL;
				    case "H":   return (ushort) ((_machine.Registers.HL & 0xFF00) >> 8 );
				    case "L":   return (ushort) ( _machine.Registers.HL & 0x00FF);
				    case "BC":  return (ushort) _machine.Registers.BC;
				    case "B":   return (ushort) ( (_machine.Registers.BC & 0xFF00) >> 8 );
				    case "BL":  return (ushort) ( _machine.Registers.BC & 0x00FF);
				    case "DE":  return (ushort) _machine.Registers.DE;
				    case "D":   return (ushort) ( (_machine.Registers.DE & 0xFF00) >> 8 );
				    case "E":   return (ushort) ( _machine.Registers.DE & 0x00FF);
                                            
				    case "A'":  return (ushort) _machine.Registers.AltA;
				    case "HL'": return (ushort) _machine.Registers.AltHL;
				    case "H'":  return (ushort) ((_machine.Registers.AltHL & 0xFF00) >> 8 );
				    case "L'":  return (ushort) (_machine.Registers.AltHL & 0x00FF);
				    case "BC'": return (ushort) _machine.Registers.AltBC;
				    case "B'":  return (ushort) ((_machine.Registers.AltBC & 0xFF00) >> 8 );
				    case "C'":  return (ushort) (_machine.Registers.AltBC & 0x00FF);
				    case "DE'": return (ushort) _machine.Registers.AltDE;
				    case "D'":  return (ushort) ((_machine.Registers.AltDE & 0xFF00) >> 8 );
				    case "E'":  return (ushort) (_machine.Registers.AltDE & 0x00FF);
                                            
				    case "IX":  return (ushort) _machine.Registers.IX;
				    case "IXH": return (ushort) ((_machine.Registers.IX & 0xFF00) >> 8 );
				    case "IXL": return (ushort) (_machine.Registers.IX & 0x00FF);
                                            
				    case "IY":  return (ushort) _machine.Registers.IY;
				    case "IYH": return (ushort) ((_machine.Registers.IY & 0xFF00) >> 8 );
				    case "IYL": return (ushort) (_machine.Registers.IY & 0x00FF);
                                            
				    case "PC":  return (ushort) _machine.Registers.PC;
				    case "SP":  return (ushort) _machine.Registers.SP;
                                            
				    case "I":   return (ushort) _machine.Registers.I;
				    case "R":   return (ushort) _machine.Registers.R;
			    }

                throw new Exception( "Unknown register '" + pRegister + "'" );
            }

            set
            {
                switch( pRegister )
                {
                    case "A":   A     = (byte)value; return;
                    case "HL":  HL    = value;       return;
                    case "BC":  BC    = value;       return;
                    case "DE":  DE    = value;       return;
                    case "A'":  AltA  = (byte)value; return;
                    case "HL'": AltHL = value;       return;
                    case "BC'": AltBC = value;       return;
                    case "DE'": AltDE = value;       return;
                    case "IX":  IX    = value;       return;
                    case "IY":  IY    = value;       return;
                    case "PC":  PC    = value;       return;
                    case "SP":  SP    = value;       return;
                    case "I":   I     = (byte)value; return;
                    case "R":   R     = (byte)value; return;
                }

                throw new Exception( "Unknown register '" + pRegister + "'" );
            }
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