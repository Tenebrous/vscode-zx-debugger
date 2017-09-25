using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ZXDebug;
using ZXDebug.SourceMap;
using File = System.IO.File;

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
        public Maps SourceMaps { get; } = new Maps();
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

        public bool UpdateDisassembly( ushort pAddress, string pFilename = null )
        {
            if( UpdateDisassemblyInternal( pAddress, pFilename ) || _disassemblyMemoryMap != Memory.ToString() )
            {
                if( pFilename != null )
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
        public void WriteDisassemblyFile( string pFilename )
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
                        _tempLabelBuilder.Append( ' ', Math.Max( 20 - _tempLabelBuilder.Length, 0 ) );
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
                    Address symbol;

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
                                text = text.ReplaceFirst( "{code}", op.Value.ToHex() );

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


        Address Symbol( BankID pBankID, ushort pAddress )
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

        public bool PreloadDisassembly( ushort pAddress, string pFilename = null )
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
}