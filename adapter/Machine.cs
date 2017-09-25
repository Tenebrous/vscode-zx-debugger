using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
        public SourceMaps SourceMaps { get; } = new SourceMaps();
        public Disassembler Disassembler { get; } = new Disassembler();
        public Breakpoints Breakpoints { get; }

        public Machine( Debugger pConnection )
        {
            Connection = pConnection;
            Registers = new Registers(this);
            Memory    = new Memory(this);
            Stack     = new Stack(this);
            Breakpoints = new Breakpoints(this);

            Connection.PausedEvent += Connection_OnPause;
            Connection.ContinuedEvent += Connection_OnContinue;
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

        public bool StepOut()
        {
            OnContinue?.Invoke();
            return Connection.StepOut();
        }

        public bool Stop()
        {
            return Connection.Disconnect();
        }



        /////////////////
        // 

        class DisasmBank : Bank
        {
            public Dictionary<ushort, InstructionLine> Lines = new Dictionary<ushort, InstructionLine>();
            public List<InstructionLine> SortedLines = new List<InstructionLine>();
        }

        Dictionary<BankID, DisasmBank> _disasmBanks = new Dictionary<BankID, DisasmBank>();
        List<InstructionLine> _tempDisasm = new List<InstructionLine>();
        string _disassemblyMemoryMap = "";

        public bool UpdateDisassembly( ushort pAddress, string pFilename )
        {
            if( UpdateDisassemblyInternal( pAddress, pFilename ) || _disassemblyMemoryMap != Memory.ToString() )
            {
                WriteDisassemblyFile( pFilename );
                return true;
            }

            return false;
        } 

        bool UpdateDisassemblyInternal( ushort pAddress, string pFilename )
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

                if( ShouldFinishDisassembling( line.Instruction.Bytes ) )
                    break;

                address += (ushort)line.Instruction.Length;
            }

            if( !needDisasm )
                return false;
            //
            ////


            var opcodes = new byte[50];
            Connection.ReadMemory( pAddress, opcodes, opcodes.Length );

            var index = 0;

            try
            {
                Disassembler.Instruction instruction;

                while( ( instruction = Disassembler.Disassemble( opcodes, index ) ) != null && _tempDisasm.Count < 30 )
                {
                    _tempDisasm.Add( new InstructionLine()
                        {
                            Bank = minBank.ID,
                            Address = (ushort)(pAddress + index),
                            Instruction = instruction
                        } 
                    );
                    index += instruction.Length;
                }
            }
            catch( Exception e )
            {
                Log.Write( Log.Severity.Error, e.ToString() );
                throw;
            }

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
                    line.Address = offset;
                    lines[offset] = line;
                    bank.SortedLines.Add( line );
                }

                if( ShouldFinishDisassembling( line.Instruction.Bytes ) )
                    break;
            }

            _tempDisasm.Clear();

            return true;
        }

        Dictionary<int, InstructionLine> _linesToDisasm = new Dictionary<int, InstructionLine>();
        HashSet<BankID> _tempBankDone = new HashSet<BankID>();
        void WriteDisassemblyFile( string pFilename )
        {
            _linesToDisasm.Clear();
            _disassemblyMemoryMap = Memory.ToString();

            if( File.Exists( pFilename ) )
                File.SetAttributes( pFilename, 0 );

            var lineNumber = 0;
            using( var stream = new StreamWriter( pFilename ) )
            {
                _tempBankDone.Clear();
                foreach( var slot in Memory.Slots )
                {
                    if( !_disasmBanks.TryGetValue( slot.Bank.ID, out var bank ) )
                        continue;

                    bank.LastAddress = slot.Min;
                    bank.IsPagedIn = true;

                    if( bank.Lines.Count == 0 )
                        continue;

                    if( slot.ID >= 0 )
                    {
                        lineNumber++;
                        stream.WriteLine( "Slot_{0} ({1}-{2}):", slot.ID, slot.Min.ToHex(), slot.Max.ToHex() );
                    }

                    _tempBankDone.Add( slot.Bank.ID );

                    bank.SortedLines.Sort( ( pLeft, pRight ) => pLeft.Address.CompareTo( pRight.Address ) );
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

                    bank.IsPagedIn = false;

                    if( bank.Lines.Count == 0 )
                        continue;

                    if( !doneHeader )
                    {
                        lineNumber++;
                        stream.WriteLine( "Not currently paged in:" );
                        doneHeader = true;
                    }

                    bank.SortedLines.Sort( ( pLeft, pRight ) => pLeft.Address.CompareTo( pRight.Address ) );
                    WriteDisasmLines( stream, bank, bank.LastAddress, ref lineNumber );
                }
            }

            File.SetAttributes( pFilename, FileAttributes.ReadOnly );
        }

        StringBuilder _tempLabelBuilder = new StringBuilder();
        void WriteDisasmLines( TextWriter pStream, DisasmBank pBank, ushort pOffset, ref int pLineNumber )
        {
            if( !string.IsNullOrWhiteSpace( pBank.Name ) && pBank.Name != "ALL" )
            {
                pLineNumber++;

                if( pBank.IsPagedIn )
                    pStream.WriteLine( "  {0}", pBank.Name );
                else
                    pStream.WriteLine( "  {0} ({1})", pBank.Name, pBank.LastAddress.ToHex() );
            }

            _tempLabelBuilder.Clear();
            var prev = pBank.SortedLines[0].Address;
            foreach( var line in pBank.SortedLines )
            {
                if( line.Address - prev > 1 )
                {
                    pLineNumber++;
                    pStream.WriteLine();
                }
                
                prev = (ushort) ( line.Address + line.Instruction.Length );

                var symbol = SourceMaps.Find( pBank.ID, (ushort)(line.Address + pOffset) );

                if( symbol?.Labels != null && symbol.Labels.Count > 0 )
                {
                    _tempLabelBuilder.Append( ' ', 4 );
                    _tempLabelBuilder.Append( symbol.Labels[0] );
                    _tempLabelBuilder.Append( ':' );

                    if( !string.IsNullOrWhiteSpace( symbol.Comment ) || symbol.File != null || symbol.Map != null )
                    {
                        // line up comment with start of mnemonic at col 21
                        _tempLabelBuilder.Append( ' ', 20 - _tempLabelBuilder.Length );
                        _tempLabelBuilder.Append( ';' );

                        if( !string.IsNullOrWhiteSpace( symbol.Comment ) )
                        {
                            _tempLabelBuilder.Append( ' ' );
                            _tempLabelBuilder.Append( symbol.Comment );
                        }

                        if( symbol.File != null )
                        {
                            _tempLabelBuilder.Append( ' ' );
                            _tempLabelBuilder.Append( symbol.File.Filename );
                            _tempLabelBuilder.Append( ':' );
                            _tempLabelBuilder.Append( symbol.Line );
                        }

                        if( symbol.Map != null )
                        {
                            _tempLabelBuilder.Append( ' ' );
                            _tempLabelBuilder.Append( '(' );
                            _tempLabelBuilder.Append( Path.GetFileName( symbol.Map.Filename ) );
                            _tempLabelBuilder.Append( ')' );
                        }
                    }
                }

                if( _tempLabelBuilder.Length > 0 )
                {
                    if( Disassembler.Settings.BlankLineBeforeLabel )
                    {
                        pLineNumber++;
                        pStream.WriteLine();
                    }

                    pLineNumber++;
                    pStream.WriteLine( _tempLabelBuilder.ToString() );
                    _tempLabelBuilder.Clear();
                }

                pLineNumber++;
                pStream.WriteLine( "      {0:X4} {1,-8} {2}",
                    line.Address + pOffset,
                    Format.ToHex( line.Instruction.Bytes ),
                    FormatInstruction( line, pBank.ID, pOffset )
                );

                line.FileLine = pLineNumber;

                _linesToDisasm[pLineNumber] = line;
            }
        }

        string FormatInstruction( InstructionLine pLine, BankID pBankID, ushort pOffset )
        {
            var instruction = pLine.Instruction;

            string comment = null;
            var text = instruction.Text;

            if( instruction.Operands != null && instruction.Operands.Length > 0 )
            {
                foreach( var op in instruction.Operands )
                {
                    int offset;
                    int absOffset;
                    char sign;
                    SourceMap.SourceAddress symbol;

                    switch( op.Type )
                    {
                        case Disassembler.Operand.TypeEnum.Imm8:
                            text = text.ReplaceFirst( "{b}", ( (byte) op.Value ).ToHex() );
                            break;

                        case Disassembler.Operand.TypeEnum.Imm16:
                            text = text.ReplaceFirst( "{w}", op.Value.ToHex() );
                            break;

                        case Disassembler.Operand.TypeEnum.Index:
                            offset = (int) op.Value;
                            sign = '+';

                            if( ( op.Value & 0x80 ) == 0x80 )
                            {
                                offset = -(byte) ~(byte) ( op.Value - 1 );
                                sign = '-';
                            }

                            absOffset = Math.Abs( offset );
                            text = text.ReplaceFirst( "{+i}", sign + ( (byte) absOffset ).ToHex() );

                            break;

                        case Disassembler.Operand.TypeEnum.CodeRel:
                            offset = op.Value;
                            sign = '+';

                            if( ( op.Value & 0x80 ) == 0x80 )
                            {
                                offset = -(byte) ~(byte) ( op.Value - 1 );
                                sign = '-';
                            }

                            var addr = (ushort) ( pLine.Address + offset + pLine.Instruction.Length );
                            symbol = Symbol( pBankID, addr );
                            absOffset = Math.Abs( offset );

                            if( symbol != null )
                            {
                                text = text.ReplaceFirst( "{+b}", symbol.Labels[0] );
                                comment = sign + ( (byte) absOffset ).ToHex() + " " + addr.ToHex() + " ";
                            }
                            else
                            {
                                text = text.ReplaceFirst( "{+b}", addr.ToHex() );
                                comment = sign + ( (byte) absOffset ).ToHex();
                            }

                            break;

                        case Disassembler.Operand.TypeEnum.DataAddr:

                            symbol = Symbol( pBankID, op.Value );

                            if( symbol != null )
                            {
                                text = text.ReplaceFirst( "{data}", symbol.Labels[0] );
                                comment = op.Value.ToHex();
                            }
                            else
                                text = text.ReplaceFirst( "{data}", op.Value.ToHex() );

                            break;

                        case Disassembler.Operand.TypeEnum.CodeAddr:

                            symbol = Symbol( pBankID, op.Value );

                            if( symbol != null )
                            {
                                text = text.ReplaceFirst( "{code}", symbol.Labels[0] );
                                comment = op.Value.ToHex();
                            }
                            else
                                text = text.ReplaceFirst( "{code}", op.Value.ToHex() + " (" + pBankID + "/" + op.Value.ToHex() + "/" + op.Value + ")");

                            break;
                    }
                }
            }

            //// temporary test
            //
            var sym = Symbol( pBankID, (ushort)(pLine.Address + pOffset) );
            if( sym?.File != null )
            {
                comment = comment?.PadRight( 20 ) ?? "".PadRight(20);
                comment += sym.File.Filename + ":" + sym.Line;
            }
            //
            ////

            if( comment != null )
                text = text.PadRight( 30 ) + "; " + comment;

            return text;
        }


        SourceMap.SourceAddress Symbol( BankID pBankID, ushort pAddress )
        {
            var found = SourceMaps.Find( pBankID, pAddress );

            if( found == null )
                found = SourceMaps.Find( BankID.Unpaged(), pAddress );

            if( found == null )
            {
                var slot = Memory.GetSlot( pAddress );
                pBankID = slot.Bank.ID;
                found = SourceMaps.Find( pBankID, pAddress );
            }

            //Log.Write( Log.Severity.Message, pBankID + ":" + pAddress.ToHex() + " " +  );

            return found;
        }

        //public void UpdateDisassembly( List<AssemblyLine> pList, string pFilename )
        //{
        //    // add later
        //}

        DisasmBank GetDisasmBank( BankID pBankID )
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
                return line.FileLine;

            return 0;
        }

        public bool PreloadDisassembly( ushort pAddress, string pFilename )
        {
            var slot = Memory.GetSlot( pAddress );
            var bank = GetDisasmBank( slot.Bank.ID );
            var offset = pAddress - slot.Min;

            if( !bank.Lines.TryGetValue( (ushort)offset, out var line ) )
                return false;

            if( line.Instruction.Operands != null && line.Instruction.Operands.Length > 0 )
                if( line.Instruction.Operands[0].Type == Disassembler.Operand.TypeEnum.CodeAddr )
                    return UpdateDisassembly( line.Instruction.Operands[0].Value, pFilename );

            return false;
        }

        bool ShouldFinishDisassembling( byte[] pOpcodes )
        {
            // temporarily end disasm on a RET to see if it simplifies things

            if( pOpcodes.Length > 0 )
                if( pOpcodes[0] == 0xC9 )
                    return true;

            return false;
        }

        public InstructionLine GetLineFromDisassemblyFile( int pLineNumber )
        {
            if( _linesToDisasm.TryGetValue( pLineNumber, out var result ) )
                return result;

            return null;
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

        public bool IsValidRegister( string pRegister )
        {
            try
            {
                var x = this[pRegister];
                return true;
            }
            catch
            {
                return false;
            }
        }

        HashSet<string> _wordRegs = new HashSet<string>()
        {
            "PC", "SP", "AF", "BC", "DE", "HL",  "AF'", "BC'", "DE'", "HL'", "IX", "IY"
        };

        public int Size( string pRegister )
        {
            return _wordRegs.Contains( pRegister.ToUpper() ) ? 2 : 1;
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
			    switch( pRegister.ToUpper() )
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
                switch( pRegister.ToUpper() )
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
        Dictionary<BankID, Bank> _banks = new Dictionary<BankID, Bank>();

        public List<Slot> Slots { get; } = new List<Slot>();

        Machine _machine;
        public Memory( Machine pMachine )
        {
            _machine = pMachine;
        }

        public Slot GetSlot( ushort pAddress )
        {
            int slotIndex;
            ushort slotAddress;
            int slotSize;

            if( PagingEnabled )
            {
                slotSize = SlotSize;
                slotIndex = (int) ( pAddress / SlotSize );
                slotAddress = (ushort) ( slotIndex * SlotSize );
            }
            else
            {
                slotSize = 0x10000;
                slotIndex = -1;
                slotAddress = 0;
            }

            if( _slots.TryGetValue( slotIndex, out var slot ) )
                return slot;

            slot = new Slot() { ID = slotIndex, Min = slotAddress, Max = (ushort) ( slotAddress + slotSize - 1 ) };
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

        public Bank Bank( BankID pID )
        {
            if( !_banks.TryGetValue( pID, out var result ) )
                result = new Bank() { ID = pID };

            return result;
        }

        public int Get( ushort pAddress, int pLength, byte[] pBuffer )
        {
            return _machine.Connection.ReadMemory( pAddress, pBuffer, pLength );
        }

        public void GetMapping()
        {
            _machine.Connection.RefreshMemoryPages( this );
        }

        StringBuilder _tempToString = new StringBuilder();
        public override string ToString()
        {
            _tempToString.Clear();
            foreach( var kvp in _slots )
            {
                if( _tempToString.Length > 0 )
                    _tempToString.Append( ' ' );

                _tempToString.Append( kvp.Key + ":" + kvp.Value.Bank.ID );
            }

            return _tempToString.ToString();
        }
    }

    public struct BankID
    {
        public enum TypeEnum
        {
            All  = 0,
            ROM  = 1,
            Bank = 2,
            Div  = 3
        }

        public readonly TypeEnum Type;
        public readonly int Number;

        public BankID( TypeEnum pType, int pNumber = 0 )
        {
            Type = pType;
            Number = pNumber;
        }

        static Regex _parseBank = new Regex( @"(?'type'BANK|DIV)_(?'number'\d*)(_(?'part'L|H))?" );
        public BankID( string pBank )
        {
            Type = TypeEnum.All;
            Number = 0;

            var match = _parseBank.Match( pBank );
            if( match.Success )
            {
                Number = int.Parse( match.Groups["number"].Value );

                var type = match.Groups["type"].Value;
                var part = match.Groups["part"].Value;

                if( type == "ROM" )
                    Type = TypeEnum.ROM;
                else if( type == "BANK" )
                    Type = TypeEnum.Bank;
                else if( type == "DIV" )
                    Type = TypeEnum.Div;
            }
        }

        public BankID( string pType, int pNumber )
        {
            Type = TypeEnum.All;

            if( string.Compare( pType, "ROM", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.ROM;
            else if( string.Compare( pType, "BANK", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.Bank;
            else if( string.Compare( pType, "DIV", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.Div;

            Number = pNumber;
        }

        //public static implicit operator int(BankID pValue)
        //{
        //    if( pValue.Type == BankType.ROM )
        //        return -2 - pValue.Number;

        //    if( pValue.Type == BankType.All )
        //        return -1;

        //    return pValue.Number;
        //}

        //public static implicit operator BankID( int pValue )
        //{
        //    if( pValue < -1 )
        //        return new BankID() { Type = BankType.ROM, Number = -2 - pValue };

        //    if( pValue == -1 )
        //        return new BankID() { Type = BankType.All };

        //    return new BankID() { Type = BankType.Bank, Number = pValue };
        //}

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Type.GetHashCode();
                hash = hash * 23 + Number.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            if( Type == TypeEnum.ROM )
                return "ROM_" + Number;

            if( Type == TypeEnum.Bank )
                return "BANK_" + Number;

            if( Type == TypeEnum.Div )
                return "DIV_" + Number;

            return "ALL";
        }

        public static BankID ROM( int pID )
        {
            return new BankID( BankID.TypeEnum.ROM, pID );
        }

        public static BankID Bank( int pID )
        {
            return new BankID( BankID.TypeEnum.Bank, pID );
        }

        public static BankID Unpaged()
        {
            return new BankID( BankID.TypeEnum.All );
        }
    }

    public class Bank
    {
        public BankID ID;
        public bool   IsPagedIn;
        public ushort LastAddress;
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

    public class Breakpoints : IEnumerable<Breakpoint>
    {
        Machine _machine;

        Dictionary<int, Breakpoint> _breakpoints = new Dictionary<int, Breakpoint>();

        public Breakpoints( Machine pMachine )
        {
            _machine = pMachine;
        }

        int GetFreeID()
        {
            int id = 0;

            while( _breakpoints.ContainsKey( id ) )
                id++;

            return id;
        }

        public Breakpoint Add( InstructionLine pLine )
        {
            if( pLine.Breakpoint != null )
                return pLine.Breakpoint;

            var bp = new Breakpoint() { Index = GetFreeID(), Line = pLine };

            if( _machine.Connection.SetBreakpoint( this, bp ) )
            {
                _breakpoints.Add( bp.Index, bp );
                pLine.Breakpoint = bp;
            }

            return bp;
        }

        public void Remove( InstructionLine pLine )
        {
            if( pLine.Breakpoint == null )
                return;

            _machine.Connection.RemoveBreakpoint( this, pLine.Breakpoint );

            _breakpoints.Remove( pLine.Breakpoint.Index );
        }

        public void Remove( Breakpoint pBreakpoint )
        {
            _machine.Connection.RemoveBreakpoint( this, pBreakpoint );

            _breakpoints.Remove( pBreakpoint.Index );
            
            pBreakpoint.Line.Breakpoint = null;
            pBreakpoint.Line = null;
        }

        public void Clear()
        {
            _machine.Connection.RemoveBreakpoints( this );

            foreach( var b in _breakpoints )
            {
                if( b.Value.Line != null )
                    b.Value.Line.Breakpoint = null;
            }

            _breakpoints.Clear();
        }

        public void Commit()
        {
            _machine.Connection.SetBreakpoints( this );
        }

        public IEnumerator<Breakpoint> GetEnumerator()
        {
            return _breakpoints.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class Breakpoint
    {
        public int Index;
        public InstructionLine Line;
    }
}