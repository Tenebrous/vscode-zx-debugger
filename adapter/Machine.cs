using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using ZXDebug;

namespace Spectrum
{
    public class Machine
    {
        public Debugger Connection;

        public Action OnPause;
        public Action OnContinue;

        public Registers Registers { get; }
        public Memory Memory { get; }
        public Stack Stack { get; }

        public Machine( Debugger pConnection )
        {
            Connection = pConnection;
            Registers = new Registers(this);
            Memory    = new Memory(this);
            Stack     = new Stack(this);

            Connection.OnPause += Connection_OnPause;
            Connection.OnContinue += Connection_OnContinue;
        }

        /////////////////
        // events from debugger connection

        void Connection_OnPause()
        {
            OnPause?.Invoke();
        }

        void Connection_OnContinue()
        {
            OnContinue?.Invoke();
        }


        /////////////////
        // actions

        public bool Start()
        {
            return Connection.Connect();
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
            OnContinue?.Invoke();
            return Connection.StepOver();
        }

        public bool Step()
        {
            OnContinue?.Invoke();
            return Connection.Step();
        }

        public bool Stop()
        {
            return Connection.Disconnect();
        }



        /////////////////
        // 

        class DisasmLine
        {
            public ushort Offset;
            public byte[] Opcodes;
            public string Text;
            public int LineNumber;
        }

        class DisasmBank : Bank
        {
            public Dictionary<ushort, DisasmLine> Lines = new Dictionary<ushort, DisasmLine>();
            public List<DisasmLine> SortedLines = new List<DisasmLine>();
        }

        Dictionary<int, DisasmBank> _disasmBanks = new Dictionary<int, DisasmBank>();
            
        List<AssemblyLine> _tempDisasm = new List<AssemblyLine>();

        HashSet<int> _tempBankDone = new HashSet<int>();

        public bool UpdateDisassembly( ushort pAddress, string pFilename )
        {
            // note: assumes disassembly can only cover a maximum of two slots


            // find starting slot & bank
            var minSlot = Memory.GetSlot( pAddress );
            var minBank = GetDisasmBank( minSlot.Bank.ID );


            //// don't update disassembly if we have at least 10 instructions worth already
            //
            var needDisasm = false;
            var address = (ushort)(pAddress - minSlot.Min);

            for( var i = 0; i < 10; i++ )
            {
                if( !minBank.Lines.TryGetValue( address, out var line ) )
                {
                    // we don't have data at this address so we need to disassemble to get it
                    needDisasm = true;
                    break;
                }

                if( TerminateDisassembly( line.Opcodes ) )
                    break;

                address += (ushort)line.Opcodes.Length;
            }

            if( !needDisasm )
                return false;
            //
            ////


            _tempDisasm.Clear();
            Connection.Disassemble( pAddress, 30, _tempDisasm );


            if( _tempDisasm.Count == 0 )
                return false;


            var maxLine = _tempDisasm[_tempDisasm.Count - 1];
            var maxSlot = Memory.GetSlot( maxLine.Address );
            var maxBank = GetDisasmBank( maxSlot.Bank.ID );

            foreach( var line in _tempDisasm )
            {
                var slot = line.Address <= minSlot.Max ? minSlot : maxSlot;
                var bank = line.Address <= minSlot.Max ? minBank : maxBank;
                var lines = bank.Lines;

                var offset = (ushort) ( line.Address - slot.Min );
                if( !lines.ContainsKey( offset ) )
                {
                    var dline = new DisasmLine()
                    {
                        Offset = offset,
                        Opcodes = line.Opcodes,
                        Text = line.Text
                    };

                    lines[offset] = dline;
                    bank.SortedLines.Add( dline );
                }

                if( TerminateDisassembly( line.Opcodes ) )
                    break;
            }

            if( File.Exists(pFilename) )
                File.SetAttributes( pFilename, 0 );

            var lineNumber = 0;
            using( var stream = new StreamWriter( pFilename ) )
            {
                _tempBankDone.Clear();
                foreach( var slot in Memory.Slots )
                {
                    if( !_disasmBanks.TryGetValue( slot.Bank.ID, out var bank ) )
                        continue;

                    if( bank.Lines.Count == 0 )
                        continue;

                    lineNumber++;
                    stream.WriteLine( "Slot{0} ({1:X4}-{2:X4}):", slot.ID, slot.Min, slot.Max );

                    _tempBankDone.Add( slot.Bank.ID );
                    
                    bank.SortedLines.Sort( ( pLeft, pRight ) => pLeft.Offset.CompareTo( pRight.Offset ) );
                    WriteDisasmLines( stream, bank, slot.Min, ref lineNumber );

                    lineNumber++;
                    stream.WriteLine();
                }

                var doneHeader = false;
                foreach( var kvpBank in _disasmBanks )
                {
                    var bank = kvpBank.Value;

                    if( _tempBankDone.Contains( bank.ID ) )
                        continue;

                    if( bank.Lines.Count == 0 )
                        continue;

                    if( !doneHeader )
                    {
                        lineNumber++;
                        stream.WriteLine( "Not currently paged in:" );
                        doneHeader = true;
                    }

                    bank.SortedLines.Sort( ( pLeft, pRight ) => pLeft.Offset.CompareTo( pRight.Offset ) );
                    WriteDisasmLines( stream, bank, 0, ref lineNumber );
                }
            }

            File.SetAttributes( pFilename, FileAttributes.ReadOnly );

            _tempDisasm.Clear();

            return true;
        }

        static void WriteDisasmLines( TextWriter pStream, DisasmBank pBank, ushort pOffset, ref int pLineNumber )
        {
            pLineNumber++;
            pStream.WriteLine( "  {0}", pBank.Name );

            var prev = pBank.SortedLines[0].Offset;
            foreach( var line in pBank.SortedLines )
            {
                if( line.Offset - prev > 1 )
                {
                    pLineNumber++;
                    pStream.WriteLine();
                }

                prev = (ushort) ( line.Offset + line.Opcodes.Length );

                pLineNumber++;
                pStream.WriteLine( "    {0:X4} {1,-8} {2}",
                    line.Offset + pOffset,
                    Format.ToHex( line.Opcodes ),
                    line.Text
                );

                line.LineNumber = pLineNumber;
            }
        }

        //public void UpdateDisassembly( List<AssemblyLine> pList, string pFilename )
        //{
        //    // add later
        //}

        DisasmBank GetDisasmBank( int pBankID )
        {
            if( _disasmBanks.TryGetValue( pBankID, out var d ) )
                return d;

            d = new DisasmBank()
            {
                ID = pBankID
            };

            _disasmBanks[pBankID] = d;

            return d;
        }

        public int FindLine( ushort pAddress )
        {
            var slot = Memory.GetSlot( pAddress );
            var bank = GetDisasmBank( slot.Bank.ID );
            var offset = pAddress - slot.Min;

            if( bank.Lines.TryGetValue( (ushort)offset, out var line ) )
                return line.LineNumber;

            return 0;
        }

        public bool PreloadDisassembly( ushort pAddress, string pFilename )
        {
            var slot = Memory.GetSlot( pAddress );
            var bank = GetDisasmBank( slot.Bank.ID );
            var offset = pAddress - slot.Min;

            if( !bank.Lines.TryGetValue( (ushort)offset, out var line ) )
                return false;

            var opcodes = line.Opcodes;
            var preload = pAddress;

            switch( opcodes[0] )
            {
                case 0x18: // jr     ± byte
                case 0x20: // jr nz  ± byte
                case 0x28: // jr z   ± byte
                case 0x30: // jr nc  ± byte
                case 0x38: // jr c   ± byte
                    break;

                case 0xCA: // jp z     word
                case 0xC3: // jp       word
                case 0xC4: // call     word
                case 0xCC: // call z   word
                case 0xCD: // call     word
                case 0xD2: // jp nc    word
                case 0xD4: // call nc  word
                case 0xDA: // jp c     word
                case 0xDC: // call c   word
                case 0xE2: // jp po    word
                case 0xE4: // call po  word
                case 0xEA: // jp pe    word
                case 0xEC: // call pe  word
                case 0xF2: // jp p     word
                case 0xF4: // call p   word
                case 0xFA: // jp m     word
                case 0xFE: // call m   word
                    preload = (ushort)(opcodes[1] | opcodes[2] << 8);
                    break;

                case 0xE9: // jp (hl)
                    break;

                case 0xCF: // rst $08
                case 0xD7: // rst $10
                case 0xDF: // rst $18
                case 0xE7: // rst $20
                case 0xEF: // rst $28
                case 0xF7: // rst $30
                case 0xFF: // rst $38
                    break;
            }

            if( preload != pAddress )
                return UpdateDisassembly( preload, pFilename );

            return false;
        }

        bool TerminateDisassembly( byte[] pOpcodes )
        {
            // temporarily end disasm on a RET to see if it simplifies things

            if( pOpcodes.Length == 1 )
                if( pOpcodes[0] == 0xC9 )
                    return true;

            return false;
        }
    }

    public class Registers
    {
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

        public ushort PC;
        public ushort SP;

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
            _machine.Connection.RefreshRegisters( this );
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
                    //
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

                    //
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

                    //
				    case "IXH": return (ushort) IXH;
				    case "IXL": return (ushort) IXL;
				    case "IX":  return (ushort) ((IXH << 8) | IXL);

				    case "IYH": return (ushort) IYH;
				    case "IYL": return (ushort) IYL;
				    case "IY":  return (ushort) ((IYH << 8) | IYL);

                    //                     
				    case "PC":  return (ushort) PC;
				    case "SP":  return (ushort) SP;
                                            
				    case "I":   return (ushort) I;
				    case "R":   return (ushort) R;

                    default:
			            throw new Exception( "Unknown register '" + pRegister + "'" );
			    }
            }

            set
            {
                switch( pRegister )
                {
                    //
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

                    //
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

                    //
                    case "IXH": IXH   = (byte)value;          return;
                    case "IXL": IXL   = (byte)value;          return;
                    case "IX":  IXH   = (byte)(value >> 8);     
                                IXL   = (byte)(value & 0xFF); return;

                    case "IYH": IYH   = (byte)value;          return;
                    case "IYL": IYL   = (byte)value;          return;
                    case "IY":  IYH   = (byte)(value >> 8);     
                                IYL   = (byte)(value & 0xFF); return;

                    //
                    case "PC":  PC    = value;                return;
                    case "SP":  SP    = value;                return;
                             
                    case "I":   I     = (byte)value;          return;
                    case "R":   R     = (byte)value;          return;

                    default:
                        throw new Exception( "Unknown register '" + pRegister + "'" );
                }
            }
        }

    }


    public class Memory
    {
        public bool   PagingEnabled;
        public ushort SlotSize = 0x4000;

        Dictionary<int, Slot> _slots = new Dictionary<int, Slot>();
        Dictionary<int, Bank> _banks = new Dictionary<int, Bank>();

        public List<Slot> Slots { get; } = new List<Slot>();

        Machine _machine;
        public Memory( Machine pMachine )
        {
            _machine = pMachine;
        }

        public Slot GetSlot( ushort pAddress )
        {
            var slotIndex = (int)(pAddress / SlotSize);
            var slotAddress = (ushort)(slotIndex * SlotSize);

            if( _slots.TryGetValue( slotIndex, out var slot ) )
                return slot;

            slot = new Slot() { ID = slotIndex, Min = slotAddress, Max = (ushort)( slotAddress + SlotSize - 1 ) };
            _slots[slotIndex] = slot;

            Slots.Add( slot );
            Slots.Sort( ( pLeft, pRight ) => pLeft.Min.CompareTo( pRight.Min ) );

            return slot;
        }

        public void ClearMemoryMap()
        {
            //_slots.Clear();
            //_banks.Clear();
        }

        public void SetAddressBank( ushort pMin, ushort pMax, Bank pBank )
        {
            GetSlot( pMin ).Bank = pBank;
            _banks[pBank.ID] = pBank;
        }

        Bank Bank( int pID, bool pIsROM )
        {
            if( pIsROM )
                pID = -2 - pID;

            if( !_banks.TryGetValue( pID, out var result ) )
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
            return _machine.Connection.ReadMemory( pAddress, pLength );
        }
        
        public void GetMapping()
        {
            _machine.Connection.RefreshMemoryPages( this );
        }
    }

    public struct BankID
    {
        public enum BankType
        {
            NA  = 0,
            ROM = 1,
            RAM = 2
        }

        public BankType Type;
        public int Number;

        public BankID( BankType pType, int pNumber = 0 )
        {
            Type = pType;
            Number = pNumber;
        }

        public static implicit operator int(BankID pValue)
        {
            if( pValue.Type == BankType.ROM )
                return -2 - pValue.Number;

            if( pValue.Type == BankType.NA )
                return -1;

            return pValue.Number;
        }

        public static implicit operator BankID( int pValue )
        {
            if( pValue < -1 )
                return new BankID() { Type = BankType.ROM, Number = -2 - pValue };

            if( pValue == -1 )
                return new BankID() { Type = BankType.NA };

            return new BankID() { Type = BankType.RAM, Number = pValue };
        }

        public override int GetHashCode()
        {
            return ((int)this).GetHashCode();
        }

        public override string ToString()
        {
            if( Type == BankType.ROM )
                return "ROM_" + Number;

            if( Type == BankType.RAM )
                return "RAM_" + Number;

            return "";
        }
    }

    public class Bank
    {
        public BankID ID;

        public string Name => ID.ToString();
    }

    public class Slot
    {
        public int    ID;
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
            _machine.Connection.RefreshStack( this );
        }
    }
}