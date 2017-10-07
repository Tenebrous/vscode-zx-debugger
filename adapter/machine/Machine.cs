using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ZXDebug;
using ZXDebug.SourceMapper;
using File = System.IO.File;

namespace Spectrum
{
    public class Machine
    {
        public Debugger Connection;

        public delegate void PausedHandler();
        public event PausedHandler PausedEvent;

        public delegate void ContinuedHandler();
        public event ContinuedHandler ContinuedEvent;

        public delegate void DisassemblyUpdatedHandler();
        public event DisassemblyUpdatedHandler DisassemblyUpdatedEvent;

        public Registers Registers { get; }
        public Memory Memory { get; }
        public Stack Stack { get; }
        public Maps SourceMaps { get; } = new Maps();
        public Disassembler Disassembler { get; } = new Disassembler();
        public Breakpoints Breakpoints { get; }

        public Machine( Debugger pConnection )
        {
            Connection  = pConnection;
            Registers   = new Registers(this);
            Memory      = new Memory(this);
            Stack       = new Stack(this);
            Breakpoints = new Breakpoints(this);

            Connection.PausedEvent    += Connection_OnPause;
            Connection.ContinuedEvent += Connection_OnContinue;
        }

        /////////////////
        // events from debugger connection

        void Connection_OnPause()
        {
            PausedEvent?.Invoke();
        }

        void Connection_OnContinue()
        {
            ContinuedEvent?.Invoke();
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
            return Connection.StepOver();
        }

        public bool Step()
        {
            return Connection.Step();
        }

        public bool StepOut()
        {
            return Connection.StepOut();
        }

        public bool Stop()
        {
            return Connection.Disconnect();
        }


        public class DisasmBank
        {
            public BankID ID;
            public Dictionary<ushort, DisasmLine> Lines = new Dictionary<ushort, DisasmLine>();
            public List<DisasmLine> SortedLines = new List<DisasmLine>();
        }

        /// <summary>
        /// A disassembled instruction
        /// </summary>
        public class DisasmLine
        {
            public DisasmBank Bank;

            /// <summary>
            /// Offset from start of bank
            /// </summary>
            public ushort Offset;
            public int FileLine;
            public Disassembler.Instruction Instruction;
            public Breakpoint Breakpoint;
        }


        Dictionary<BankID, DisasmBank> _disasmBanks = new Dictionary<BankID, DisasmBank>();
        List<DisasmLine> _tempDisasm = new List<DisasmLine>();
        string _disassemblyMemoryMap = "";

        Stack<DisasmRequest> _disasmAddresses = new Stack<DisasmRequest>();
        HashSet<ushort> _disasmAddressesDone = new HashSet<ushort>();
        public bool UpdateDisassembly( ushort pAddress, string pFilename = null )
        {
            _disasmAddresses.Clear();
            _disasmAddresses.Push( new DisasmRequest( 2, pAddress ) );

            _disasmAddressesDone.Clear();

            var updated = false;

            while( _disasmAddresses.Count > 0 )
            {
                var item = _disasmAddresses.Pop();
                if( _disasmAddressesDone.Add( item.Address ) )
                    updated |= UpdateDisassemblyInternal( item.Address, _disasmAddresses, item.Depth );
            }

            if( updated || _disassemblyMemoryMap != Memory.ToString() )
            {
                Log.Write( Log.Severity.Message, "Disasm: " + pAddress + " -> " + string.Join( ", ", _disasmAddressesDone ) );

                if( pFilename != null )
                    WriteDisassemblyFile( pFilename );

                return true;
            }

            return false;
        }

        byte[] _tempBytes = new byte[50];
        bool UpdateDisassemblyInternal( ushort pAddress, Stack<DisasmRequest> pRequests, int pMaxDepth )
        {
            // note: assumes disassembly can only cover a maximum of two memory slots

            // find starting slot & bank
            var minSlot = Memory.GetSlot( pAddress );
            var minBank = GetDisasmBank( minSlot.Bank.ID );
            
            if( !NeedDisassembly( pAddress, minSlot, minBank ) )
                return false;

            Connection.ReadMemory( pAddress, _tempBytes, _tempBytes.Length );

            var index = 0;

            try
            {
                Disassembler.Instruction instruction;

                while( ( instruction = Disassembler.Disassemble( _tempBytes, index ) ) != null && _tempDisasm.Count < 30 )
                {
                    var il = new DisasmLine()
                    {
                        Bank = minBank,
                        Offset = (ushort) ( pAddress + index ),
                        Instruction = instruction
                    };

                    _tempDisasm.Add( il );

                    index += instruction.Length;

                    // check for recursive disassembly
                    if( pMaxDepth > 0 )
                        PreloadDisassembly( il, pRequests, pMaxDepth-1 );
                }
            }
            catch( Exception e )
            {
                Log.Write( Log.Severity.Error, e.ToString() );
            }

            if( _tempDisasm.Count == 0 )
                return false;


            // record newly-disassembled lines

            var maxLine = _tempDisasm[_tempDisasm.Count - 1];
            var maxSlot = Memory.GetSlot( maxLine.Offset );
            var maxBank = GetDisasmBank( maxSlot.Bank.ID );

            foreach( var line in _tempDisasm )
            {
                var slot = line.Offset <= minSlot.Max ? minSlot : maxSlot;
                var bank = line.Offset <= minSlot.Max ? minBank : maxBank;
                var lines = bank.Lines;

                var offset = (ushort) ( line.Offset - slot.Min );
                if( !lines.ContainsKey( offset ) )
                {
                    line.Offset = offset;
                    lines[offset] = line;
                    bank.SortedLines.Add( line );
                }

                if( ShouldFinishDisassembling( line.Instruction.Bytes ) )
                    break;
            }

            _tempDisasm.Clear();

            return true;
        }

        bool NeedDisassembly( ushort pAddress, Slot pSlot, DisasmBank pBank )
        {
            var address = (ushort) ( pAddress - pSlot.Min );

            for( var i = 0; i < 10; i++ )
            {
                if( !pBank.Lines.TryGetValue( address, out var line ) )
                    return true;

                if( ShouldFinishDisassembling( line.Instruction.Bytes ) )
                    break;

                address += (ushort) line.Instruction.Length;
            }

            return false;
        }

        private void PreloadDisassembly( DisasmLine pDisasm, Stack<DisasmRequest> pAddressStack, int pDepth )
        {
            if( pDisasm.Instruction.Operands == null )
                return;

            foreach( var op in pDisasm.Instruction.Operands )
            {
                if( op.Type == Disassembler.Operand.TypeEnum.CodeAddr )
                    pAddressStack.Push( new DisasmRequest( pDepth, op.Value ) );
                else if( op.Type == Disassembler.Operand.TypeEnum.CodeRel )
                    pAddressStack.Push( new DisasmRequest( pDepth, (ushort)(pDisasm.Offset + (sbyte)op.Value ) ) );
            }
        }

        Dictionary<int, DisasmLine> _linesToDisasm = new Dictionary<int, DisasmLine>();
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

                    var memBank = Memory.Bank( slot.Bank.ID );
                    memBank.LastAddress = slot.Min;
                    memBank.IsPagedIn = true;

                    if( bank.Lines.Count == 0 )
                        continue;

                    if( slot.ID >= 0 )
                    {
                        lineNumber++;
                        stream.WriteLine( "Slot_{0} ({1}-{2}):", slot.ID, slot.Min.ToHex(), slot.Max.ToHex() );
                    }

                    _tempBankDone.Add( slot.Bank.ID );

                    bank.SortedLines.Sort( ( pLeft, pRight ) => pLeft.Offset.CompareTo( pRight.Offset ) );
                    WriteDisasmLines( stream, bank, slot.Min, ref lineNumber );

                    lineNumber++;
                    stream.WriteLine();
                }

                var doneHeader = false;
                foreach( var kvpBank in _disasmBanks )
                {
                    var disasmBank = kvpBank.Value;

                    if( _tempBankDone.Contains( disasmBank.ID ) )
                        continue;

                    var memBank = Memory.Bank( disasmBank.ID );
                    memBank.IsPagedIn = false;

                    if( disasmBank.Lines.Count == 0 )
                        continue;

                    if( !doneHeader )
                    {
                        lineNumber++;
                        stream.WriteLine( "Not currently paged in:" );
                        doneHeader = true;
                    }

                    disasmBank.SortedLines.Sort( ( pLeft, pRight ) => pLeft.Offset.CompareTo( pRight.Offset ) );
                    WriteDisasmLines( stream, disasmBank, memBank.LastAddress, ref lineNumber );
                }
            }

            File.SetAttributes( pFilename, FileAttributes.ReadOnly );

            DisassemblyUpdatedEvent?.Invoke();
        }

        StringBuilder _tempLabel = new StringBuilder();
        void WriteDisasmLines( TextWriter pStream, DisasmBank pBank, ushort pBankAddress, ref int pLineNumber )
        {
            if( pBank.ID.Type != BankID.TypeEnum.All )
            {
                pLineNumber++;

                var memBank = Memory.Bank( pBank.ID );

                if( memBank.IsPagedIn )
                    pStream.WriteLine( "  {0}:", memBank.ID );
                else
                    pStream.WriteLine( "  {0} ({1}):", memBank.ID, memBank.LastAddress.ToHex() );
            }

            var prev = pBank.SortedLines[0].Offset;
            foreach( var line in pBank.SortedLines )
            {
                var doneBlank = false;

                if( line.Offset - prev > 1 )
                {
                    pLineNumber++;
                    pStream.WriteLine();
                    doneBlank = true;
                }
                
                prev = (ushort) ( line.Offset + line.Instruction.Length );

                var labelledItem = GetLabels( pBank.ID, (ushort)(line.Offset + pBankAddress) );

                if( labelledItem != null )
                {
                    var doneComment = false;

                    foreach( var label in labelledItem.Labels )
                    {
                        _tempLabel.Append( ' ', 4 );
                        _tempLabel.Append( label.Name );
                        _tempLabel.Append( ':' );

                        if( !doneComment )
                        {
                            if( !string.IsNullOrWhiteSpace( label.Comment ) )
                            {
                                // line up comment with start of mnemonics at col 21
                                _tempLabel.Append( ' ', Math.Max( 20 - _tempLabel.Length, 0 ) );
                                _tempLabel.Append( ';' );
                            }

                            if( !string.IsNullOrWhiteSpace( label.Comment ) )
                            {
                                _tempLabel.Append( ' ' );
                                _tempLabel.Append( label.Comment );
                            }

                            doneComment = true;
                        }

                        if( _tempLabel.Length > 0 )
                        {
                            if( !doneBlank && Disassembler.Settings.BlankLineBeforeLabel )
                            {
                                pLineNumber++;
                                pStream.WriteLine();
                                doneBlank = true;
                            }

                            pLineNumber++;
                            pStream.WriteLine( _tempLabel.ToString() );

                            _tempLabel.Clear();
                        }
                    }
                }

                pStream.WriteLine( "      {0:X4} {1,-8} {2}",
                    line.Offset + pBankAddress,
                    Format.ToHex( line.Instruction.Bytes ),
                    FormatInstruction( line, pBank.ID, pBankAddress )
                );

                line.FileLine = pLineNumber;

                _linesToDisasm[pLineNumber++] = line;
            }
        }

        string FormatInstruction( DisasmLine pLine, BankID pBankID, ushort pOffset )
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
                    List<Label> labels;

                    switch( op.Type )
                    {
                        case Disassembler.Operand.TypeEnum.Imm8:
                            text = text.ReplaceFirst( "{b}", ( (byte) op.Value ).ToHex() );
                            break;

                        case Disassembler.Operand.TypeEnum.Imm16:
                            text = text.ReplaceFirst( "{w}", op.Value.ToHex() );
                            break;

                        case Disassembler.Operand.TypeEnum.Index:
                            offset = (sbyte)op.Value;
                            sign = offset < 0 ? '-' : '+';
                            absOffset = Math.Abs( offset );
                            text = text.ReplaceFirst( "{+i}", $"{sign}{( (byte) absOffset ).ToHex()}" );
                            break;

                        case Disassembler.Operand.TypeEnum.CodeRel:
                            offset = (sbyte)op.Value;
                            sign = offset < 0 ? '-' : '+';

                            var addr = (ushort) ( pOffset + pLine.Offset + offset + pLine.Instruction.Length );

                            labels = GetLabels( pBankID, addr )?.Labels;
                            absOffset = Math.Abs( offset );

                            if( labels != null )
                            {
                                text = text.ReplaceFirst( "{+b}", labels[0].Name );
                                comment = $"{sign}{( (byte) absOffset ).ToHex()} {addr.ToHex()} ";
                            }
                            else
                            {
                                text = text.ReplaceFirst( "{+b}", addr.ToHex() );
                                comment = $"{sign}{( (byte) absOffset ).ToHex()}";
                            }

                            break;

                        case Disassembler.Operand.TypeEnum.DataAddr:

                            labels = GetLabels( pBankID, op.Value )?.Labels;

                            if( labels != null )
                            {
                                text = text.ReplaceFirst( "{data}", labels[0].Name );
                                comment = op.Value.ToHex();
                            }
                            else
                                text = text.ReplaceFirst( "{data}", op.Value.ToHex() );

                            break;

                        case Disassembler.Operand.TypeEnum.CodeAddr:

                            labels = GetLabels( pBankID, op.Value )?.Labels;

                            if( labels != null )
                            {
                                text = text.ReplaceFirst( "{code}", labels[0].Name );
                                comment = op.Value.ToHex();
                            }
                            else
                                text = text.ReplaceFirst( "{code}", op.Value.ToHex() );

                            break;
                    }
                }
            }

            if( comment != null )
                text = text.PadRight( 30 ) + "; " + comment;

            return text;
        }

        List<AddressDetails> _tempAddressDetails = new List<AddressDetails>();
        public AddressDetails GetAddressDetails( BankID pBankID, ushort pAddress, ushort pMaxLabelDistance )
        {
            // get address details of address by checking three levels:
            //  specified bank + specified address
            //  'all' bank + specified address
            //  bank currently paged in to specified address's slot + specified address

            _tempAddressDetails.Clear();
            _tempAddressDetails.Add( SourceMaps.GetAddressDetails( pBankID, pAddress, pMaxLabelDistance ) );

            if( pBankID != BankID.Unpaged() )
                _tempAddressDetails.Add( SourceMaps.GetAddressDetails( BankID.Unpaged(), pAddress, pMaxLabelDistance ) );

            var curBank = Memory.GetMappedBank( pAddress );
            if( pBankID != curBank )
                _tempAddressDetails.Add( SourceMaps.GetAddressDetails( curBank, pAddress, pMaxLabelDistance ) );

            // now merge the details so move any Source and Label info present to the first entry
            for( var i = 1; i < _tempAddressDetails.Count; i++ )
            {
                // shuffle up source info
                if( _tempAddressDetails[0].Source == null && _tempAddressDetails[i].Source != null )
                    _tempAddressDetails[0].Source = _tempAddressDetails[0].Source  ?? _tempAddressDetails[i].Source;

                // shuffle up labels & associated info
                if( _tempAddressDetails[0].Labels == null && _tempAddressDetails[i].Labels != null )
                {
                    _tempAddressDetails[0].Labels = _tempAddressDetails[i].Labels;
                    _tempAddressDetails[0].LabelledAddress = _tempAddressDetails[i].LabelledAddress;
                    _tempAddressDetails[0].LabelledSource = _tempAddressDetails[i].LabelledSource;
                }
            }

            return _tempAddressDetails[0];
        }

        public Maps.GetLabelsResult GetLabels( BankID pBankID, ushort pAddress )
        {
            return SourceMaps.GetLabels( pBankID, pAddress )
                ?? SourceMaps.GetLabels( BankID.Unpaged(), pAddress )
                ?? SourceMaps.GetLabels( Memory.GetMappedBank( pAddress ), pAddress );
        }


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

        public int GetLineOfAddressInDisassembly( ushort pAddress )
        {
            var slot = Memory.GetSlot( pAddress );
            var bank = GetDisasmBank( slot.Bank.ID );
            var offset = pAddress - slot.Min;

            bank.Lines.TryGetValue( (ushort)offset, out var line );

            return line?.FileLine ?? 0;
        }

        public int GetLineOfAddressInDisassembly( BankID pBank, ushort pAddress )
        {
            DisasmBank bank;

            if( !_disasmBanks.TryGetValue( pBank, out bank ) )
            {
                var slot = Memory.GetSlot( pAddress );
                bank = GetDisasmBank( slot.Bank.ID );
            }

            var offset = pAddress - Memory.Bank( pBank ).LastAddress;

            bank.Lines.TryGetValue( (ushort)offset, out var line );

            return line?.FileLine ?? 0;
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

        public DisasmLine GetLineFromDisassemblyFile( int pLineNumber )
        {
            if( _linesToDisasm.TryGetValue( pLineNumber, out var result ) )
                return result;

            return null;
        }

        struct DisasmRequest
        {
            public int Depth;
            public ushort Address;

            public DisasmRequest( int pDepth, ushort pAddress )
            {
                Depth = pDepth;
                Address = pAddress;
            }

            public override string ToString()
            {
                return $"{Depth}:{Address}";
            }
        }
    }
}