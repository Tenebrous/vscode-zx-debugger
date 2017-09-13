using System;
using System.Collections.Generic;
using VSCodeDebugger;

namespace Spectrum
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


        /////////////////
        // stack

        Stack _stack;
        public Stack Stack
        {
            get { return _stack; }
        }


        /////////////////
        // 

        public void UpdateDisassembly( ushort pAddress )
        {
            foreach( var line in Connection.Disassemble( pAddress, 30 ) )
            {
                
            }

            // var defaultBank = _machine..GetBankAtAddress( 0x0000 );

            //foreach( var line in lines )
            //{
            //    var parts = line.Trim().Split( new [] {' '}, 2, StringSplitOptions.RemoveEmptyEntries );
            //    var address = Format.FromHex(parts[0]);
            //    var bank = defaultBank;

            //    if( _memory.PagingEnabled )
            //        bank = _memory.GetBankAtAddress( address );
            //}

            //public void UpdateDisassembly2( int pAddress )
            //{
            //    foreach( var section in _disassembledSections )
            //        if( pAddress >= section.Start && pAddress <= section.End - 10 )
            //            return;

            //    var lines = Command( "disassemble " + pAddress + " " + 30 );

            //    if( lines != null )
            //    {
            //        var section = new DisassemblySection() { Start = 0xFFFFF };

            //        foreach( var line in lines )
            //        {
            //            var parts = line.Trim().Split( new [] {' '}, 2, StringSplitOptions.RemoveEmptyEntries );
            //            var address = UnHex(parts[0]);

            //            section.Start = Math.Min( section.Start, address );
            //            section.End   = Math.Max( section.End, address );

            //            section.Lines.Add(
            //                new DisassemblyLine()
            //                {
            //                    Address = address,
            //                    Code = parts[1]
            //                }
            //            );

            //            // stop disassembling at hard RET (just testing to see if that makes things clearer)
            //            if( parts[1].Substring( 0, 2 ) == "C9" )
            //                break;
            //        }

            //        // look to see if we cover two existing sections, whereby we'll merge them
            //        for( int i = 0; i < _disassembledSections.Count - 1; i++ )
            //            if( section.Start <= _disassembledSections[i].End 
            //             && section.End   >= _disassembledSections[i+1].Start )
            //            {
            //                _disassembledSections[i].End = _disassembledSections[i + 1].End;
            //                _disassembledSections[i].Lines.AddRange( _disassembledSections[i+1].Lines );
            //                _disassembledSections.RemoveAt( i + 1 );
            //                break;
            //            }


            //        // find relevant section to add lines to
            //        DisassemblySection addTo = null;
            //        foreach( var otherSection in _disassembledSections )
            //            if( section.End >= otherSection.Start && section.Start <= otherSection.End )
            //            {
            //                addTo = otherSection;
            //                break;
            //            }

            //        if( addTo == null )
            //        {
            //            // created new section
            //            _disassembledSections.Add( section );
            //        }
            //        else
            //        {
            //            // merge with existing section

            //            foreach( var line in section.Lines )
            //            {
            //                bool exists = false;

            //                foreach ( var otherLine in addTo.Lines )
            //                    if( line.Address == otherLine.Address )
            //                    {
            //                        exists = true;
            //                        break;
            //                    }

            //                if( !exists )
            //                    addTo.Lines.Add( line );
            //            }

            //            addTo.Lines.Sort( ( pLeft, pRight ) => pLeft.Address.CompareTo( pRight.Address ) );
            //        }


            //        // re-sort sections
            //        _disassembledSections.Sort( ( pLeft, pRight ) => pLeft.Start.CompareTo( pRight.Start ) );
            //    }

            //    int lastBank = -1;
            //    var tmp = new List<string>();
            //    foreach( var section in _disassembledSections )
            //    {
            //        foreach( var line in section.Lines )
            //        {
            //            if( _memory.PagingEnabled )
            //            {
            //                var bank = _memory.GetMapForAddress( (ushort) line.Address );
            //                if( bank != lastBank )
            //                {
            //                    if( bank < -1 )
            //                        tmp.Add( string.Format( "ROM_{0:D2}", -1 - bank ) );
            //                    else if( bank >= 0 )
            //                        tmp.Add( string.Format( "BANK_{0:D2}", bank ) );

            //                    lastBank = bank;
            //                }
            //            }

            //            tmp.Add( string.Format( "  {0:X4} {1}", line.Address, line.Code ) );

            //            line.FileLine = tmp.Count;
            //        }

            //        tmp.Add( "" );
            //        lastBank = -1;
            //    }

            //    File.WriteAllLines( DisassemblyFile, tmp );
            //}
        }

        public int FindLine( ushort pUshort )
        {
            return 0;
        }
    }

    public class Registers
    {
        public ushort PC;
        public ushort SP;

        public byte   A;
        public byte   F;
        public byte   B;
        public byte   C;
        public byte   D;
        public byte   E;
        public byte   H;
        public byte   L;

        public byte   AltA;
        public byte   AltF;
        public byte   AltB;
        public byte   AltC;
        public byte   AltD;
        public byte   AltE;
        public byte   AltH;
        public byte   AltL;

        public byte   IXH;
        public byte   IXL;
        public byte   IYH;
        public byte   IYL;

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
                    ///
				    case "A":   return (ushort) A;
                    case "F":   return (ushort) F;
                    case "AF":  return (ushort) ((A << 8) | F);

				    case "B":   return (ushort) B;
				    case "C":   return (ushort) C;
				    case "BC":  return (ushort) ((B << 8) | C);

				    case "D":   return (ushort) D;
				    case "E":   return (ushort) E;
				    case "DE":  return (ushort) ((D << 8) | E);

				    case "H":   return (ushort) H;
				    case "L":   return (ushort) L;
				    case "HL":  return (ushort) ((H << 8) | L);

                    ///
				    case "A'":  return (ushort) AltA;
				    case "F'":  return (ushort) AltF;
                    case "AF'": return (ushort) ((AltA << 8) | AltF);

				    case "B'":  return (ushort) AltB;
				    case "C'":  return (ushort) AltC;
				    case "BC'": return (ushort) ((AltB << 8) | AltC);

				    case "D'":  return (ushort) AltD;
				    case "E'":  return (ushort) AltE;
				    case "DE'": return (ushort) ((AltD << 8) | AltE);

				    case "H'":  return (ushort) AltH;
				    case "L'":  return (ushort) AltL;
				    case "HL'": return (ushort) ((AltH << 8) | AltL);

                    ///
				    case "IXH": return (ushort) IXH;
				    case "IXL": return (ushort) IXL;
				    case "IX":  return (ushort) ((IXH << 8) | IXL);

				    case "IYH": return (ushort) IYH;
				    case "IYL": return (ushort) IYL;
				    case "IY":  return (ushort) ((IYH << 8) | IYL);

                    ///                     
				    case "PC":  return (ushort) PC;
				    case "SP":  return (ushort) SP;
                                            
				    case "I":   return (ushort) I;
				    case "R":   return (ushort) R;
			    }

                throw new Exception( "Unknown register '" + pRegister + "'" );
            }

            set
            {
                switch( pRegister )
                {
                    ///
                    case "A":   A     = (byte)value;          return;

                    case "B":   B     = (byte)value;          return;
                    case "C":   C     = (byte)value;          return;
                    case "BC":  B     = (byte)(value >> 8);     
                                C     = (byte)(value & 0xFF); return;

                    case "D":   D     = (byte)value;          return;
                    case "E":   E     = (byte)value;          return;
                    case "DE":  D     = (byte)(value >> 8);     
                                E     = (byte)(value & 0xFF); return;

                    case "H":   H     = (byte)value;          return;
                    case "L":   L     = (byte)value;          return;
                    case "HL":  H     = (byte)(value >> 8);     
                                L     = (byte)(value & 0xFF); return;

                    ///
                    case "A'":  AltA  = (byte)value;          return;

                    case "B'":  AltB  = (byte)value;          return;
                    case "C'":  AltC  = (byte)value;          return;
                    case "BC'": AltB  = (byte)(value >> 8);     
                                AltC  = (byte)(value & 0xFF); return;

                    case "D'":  AltD  = (byte)value;          return;
                    case "E'":  AltE  = (byte)value;          return;
                    case "DE'": AltD  = (byte)(value >> 8);     
                                AltE  = (byte)(value & 0xFF); return;

                    case "H'":  AltH  = (byte)value;          return;
                    case "L'":  AltL  = (byte)value;          return;
                    case "HL'": AltH  = (byte)(value >> 8);     
                                AltL  = (byte)(value & 0xFF); return;

                    ///
                    case "IXH": IXH   = (byte)value;          return;
                    case "IXL": IXL   = (byte)value;          return;
                    case "IX":  IXH   = (byte)(value >> 8);     
                                IXL   = (byte)(value & 0xFF); return;

                    case "IYH": IYH   = (byte)value;          return;
                    case "IYL": IYL   = (byte)value;          return;
                    case "IY":  IYH   = (byte)(value >> 8);     
                                IYL   = (byte)(value & 0xFF); return;

                    ///
                    case "PC":  PC    = value;                return;
                    case "SP":  SP    = value;                return;
                             
                    case "I":   I     = (byte)value;          return;
                    case "R":   R     = (byte)value;          return;
                }

                throw new Exception( "Unknown register '" + pRegister + "'" );
            }
        }

    }


    public class Memory
    {
        public bool   PagingEnabled;
        public ushort SlotSize = 0x4000;

        Dictionary<ushort, Slot> _slots = new Dictionary<ushort, Slot>();
        Dictionary<int, Bank> _banks = new Dictionary<int, Bank>();

        Machine _machine;
        public Memory( Machine pMachine )
        {
            _machine = pMachine;
        }

        public Slot GetSlot( ushort pAddress )
        {
            ushort slotIndex = (ushort)(pAddress / SlotSize);
            Slot slot;

            if( !_slots.TryGetValue( slotIndex, out slot ) )
            {
                slot = new Slot() { Min = slotIndex, Max = (ushort)(slotIndex + SlotSize - 1) };
                _slots[slotIndex] = slot;
            }

            return slot;
        }

        public void ClearMemoryMap()
        {
            _slots.Clear();
            _banks.Clear();
        }

        public void SetAddressBank( ushort pMin, ushort pMax, Bank pBank )
        {
            GetSlot( pMin ).Bank = pBank;
            _banks[pBank.ID] = pBank;
        }

        Bank Bank( int pID, bool bROM )
        {
            Bank result;

            if( bROM )
                pID = -2 - pID;

            if( !_banks.TryGetValue( pID, out result ) )
                result = new Bank() { ID = pID };

            return result;
        }

        public Bank ROM( int pID )
        {
            return Bank( pID, true );
        }

        public Bank RAM( int pID )
        {
            return Bank( pID, false );
        }

        public string Get( ushort pAddress, int pLength )
        {
            return _machine.Connection.GetMemory( pAddress, pLength );
        }

        
        public void GetMapping()
        {
            _machine.Connection.GetMemoryPages( this );
        }
    }

    public class Bank
    {
        // 0, 1, 2 etc = bank #
        // -1 = default
        // -2 = rom 0
        // -3 = rom 1
        public int    ID;
    }

    public class Slot

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

        public void Get()
        {
            _machine.Connection.GetStack( this );
        }
    }
}