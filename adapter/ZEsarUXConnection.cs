using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Z80Machine;

namespace VSCodeDebugger
{
    public class ZEsarUXConnection : IDebuggerConnection
    {
        public Action<List<string>> OnData;

        TcpClient _client;
        NetworkStream _stream;
        bool _connected;

        public bool Start()
        {
            if( _connected )
                return true;

            _client = new TcpClient();

            try
            {
                Log.Write( Log.Severity.Message, "zesarux: connecting..." );
                _client.Connect( "127.0.0.1", 10000 );
                _stream = _client.GetStream();
                _connected = true;

                ReadAll();

                var version = SendAndReceiveSingle("get-version");
                Log.Write( Log.Severity.Message, "zesarux: connected (" + version + ")." );

                ReadAll();
                Setup();
            }
            catch( Exception e )
            {
                Log.Write( Log.Severity.Error, "zesarux: connection error: " + e );
                return false;
            }

            return _connected;
        }


        public bool Pause()
        {
            SendAndReceiveSingle( "enter-cpu-step" );
            return true;
        }


        public bool Continue()
        {
            SendAndReceiveSingle( "exit-cpu-step" );
            return true;
        }


        public bool StepOver()
        {
            _wasRunning = true;
            SendAndReceiveSingle( "cpu-step-over" );
            return true;
        }


        public bool Step()
        {
            _wasRunning = true;
            SendAndReceiveSingle( "cpu-step" );
            return true;
        }


        public bool Stop()
        {
            Log.Write( Log.Severity.Message, "zesarux: disconnecting..." );

            if( _stream != null )
                SendAndReceiveSingle( "exit" );

            if( _stream != null )
            {
                _stream.Close();
                _stream = null;
            }

            if( _client != null )
            {
                _client.Close();
                _client = null;
            }

            Log.Write( Log.Severity.Message, "zesarux: disconnected." );

            return true;
        }



        public void Setup()
        {
            // from https://sourceforge.net/p/zesarux/code/ci/master/tree/remote.c#l766
            //
            // "Set debug settings on remote command protocol.It's a numeric value with bitmask with different meaning:
            //   Bit 0: show all cpu registers on cpu stepping or only pc+opcode.
            //   Bit 1: show 8 next opcodes on cpu stepping.
            //   Bit 2: Do not add a L preffix when searching source code labels.
            //   Bit 3: Show bytes when debugging opcodes"

            SendAndReceiveSingle( "set-debug-settings 8" );

            SendAndReceiveSingle( "set-memory-zone -1" );
        }

        public string GetMachine()
        {
            return _machine = SendAndReceiveSingle( "get-current-machine" );
        }

        public void GetMemoryPages( Memory pMemory )
        {
            var pages = SendAndReceiveSingle( "get-memory-pages" );
            // RO1 RA5 RA2 RA7 SCR5 PEN

            var split = pages.Split( new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );

            pMemory.ClearMemoryMap();

            int sixteenkpage = 0;

            foreach( var part in split )
            {
                if( part == "PEN" )
                    pMemory.PagingEnabled = true;
                else if( part == "PDI" )
                    pMemory.PagingEnabled = false;
                else if( part.StartsWith( "RO" ) )
                {
                    var bank = pMemory.ROM( int.Parse( part.Substring( 2 ) ) );
                    pMemory.SetAddressBank( (ushort)( sixteenkpage * 0x4000 ), (ushort)( sixteenkpage * 0x4000 + 0x3FFF ), bank );
                    sixteenkpage++;
                }
                else if( part.StartsWith( "RA" ) )
                {
                    var bank = pMemory.RAM( int.Parse( part.Substring( 2 ) ) );
                    pMemory.SetAddressBank( (ushort)( sixteenkpage * 0x4000 ), (ushort)( sixteenkpage * 0x4000 + 0x3FFF ), bank );
                    sixteenkpage++;
                }
            }
        }

        public string GetMemory( ushort pAddress, int pLength )
        {
            var memory = SendAndReceiveSingle( "read-memory " + pAddress + " " + pLength );
            return memory;
        }

        public void GetStack( Stack pStack )
        {
            pStack.Clear();
            var stack = SendAndReceiveSingle( "get-stack-backtrace" );

            // [15E6H 15E1H 0F3BH 107FH FF54H]
            var split = stack.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            pStack.Add( _registers.PC );

            foreach( var frame in split )
                if( frame.EndsWith("H") )
                    pStack.Add( (ushort)( Format.FromHex( frame.Substring( 0, frame.Length - 1 ) ) - 3 ) );
                else
                    pStack.Add( (ushort)(int.Parse( frame ) - 3) );
        }

        Registers _registers;
        public void GetRegisters( Registers pRegisters )
        {
            _registers = pRegisters;
            ParseRegisters( pRegisters, SendAndReceiveSingle( "get-registers" ) );
        }

        public void SetRegister( Registers pRegisters, string pRegister, ushort pValue )
        {
            var result = SendAndReceiveSingle( "set-register " + pRegister + "=" + pValue );
            ParseRegisters( pRegisters, result );
        }

        Regex _matchRegisters = new Regex("(?i)([a-z']*)=([0-9a-f].*?)(?: )");
        Regex _matchFlags = new Regex("(?i)(F'?)=(.{8}) ");
        void ParseRegisters( Registers pRegisters, string pString )
        {
            // [PC=0038 SP=ff4a BC=174b A=00 HL=107f DE=0006 IX=ffff IY=5c3a A'=00 BC'=0b21 HL'=ffff DE'=5cb9 I=3f R=22  F= Z P3H   F'= Z P     MEMPTR=15e6 DI IM1 VPS: 0 ]

            var matches = _matchRegisters.Matches(pString);
            foreach( Match match in matches )
            {
                var register = match.Groups[1].ToString();
                var value = Format.FromHex(match.Groups[2].ToString());

                try
                {
                    pRegisters[register] = value;
                }
                catch
                {
                    // ignore
                }
            }

            matches = _matchFlags.Matches(pString);
            foreach( Match match in matches )
            {
                var register = match.Groups[1].ToString();
                var value = match.Groups[2].ToString().Trim();
            
            //    SetRegister(register, value);
            }
        }

        public void UpdateDisassembly( int pAddress )
        {
            var lines = SendAndReceive( "disassemble " + pAddress + " " + 30 );

            if( lines == null )
                return;

            //var defaultBank = _memory.GetBankAtAddress( 0x0000 );

            //foreach( var line in lines )
            //{
            //    var parts = line.Trim().Split( new [] {' '}, 2, StringSplitOptions.RemoveEmptyEntries );
            //    var address = UnHex(parts[0]);
            //    var bank = defaultBank;

            //    if( _memory.PagingEnabled )
            //        bank = _memory.GetBankAtAddress( address );

            //}
        }

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


        public int FindLine( int pAddress )
        {
//            foreach( var section in _disassembledSections )
//                foreach( var line in section.Lines )
//                    if( line.Address == pAddress )
//                        return line.FileLine;

            return 0;
        }


        public bool IsConnected
        {
            get { return _client != null && _client.Connected; }
        }


        bool _wasRunning;
        bool _isRunning;
        public bool IsRunning
        {
            get { return _isRunning; }
        }

        public string DisassemblyFile
        {
            get { return Path.Combine( _tempFolder, "disasm.zdis" ); }
        }

        string _machine;
        public string Machine
        {
            get { return _machine; }
        }

        string _tempFolder;
        public string TempFolder
        {
            set { _tempFolder = value; }
            get { return _tempFolder; }
        }

        public bool Read()
        {
            // read any unsolicited messages coming from zesarux

            if( !IsConnected )
                return false;

            var result = ReadAll();

            if( result.Count > 0 )
                OnData?.Invoke( result );

            return result.Count > 0;
        }


        public List<string> SendAndReceive( string pCommand )
        {
            if( !Start() )
                return null;

            _tempReceiveLines.Clear();


            // send command to zesarux
            Log.Write( Log.Severity.Debug, "zesarux: (out) [" + pCommand + "]" );

            var bytes = Encoding.ASCII.GetBytes(pCommand + "\n");
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();


            // limit how long we wait for the reply to start
            var x = new Stopwatch();
            x.Start();

            // wait for data to start coming back
            while ( !_stream.DataAvailable && x.Elapsed.Seconds < 2 )
            {
            }

            if( !_stream.DataAvailable )
            {
                Log.Write( Log.Severity.Message, "zesarux: timed out waiting for data" );
                return null;
            }

            var lines = ReadAll();

            // log the received data
            lines.ForEach(
                pLine =>
                {
                    if( pLine.StartsWith( "error", StringComparison.InvariantCultureIgnoreCase ) ) throw new Exception( "ZEsarUX reports: " + pLine );
                    Log.Write( Log.Severity.Verbose, "zesarux: <- [" + pLine + "]" );
                }
            );

            return _tempReceiveLines;
        }

        string SendAndReceiveSingle( string pCommand )
        {
            var result = SendAndReceive( pCommand );

            if( result == null || result.Count == 0 )
                return "";

            return result[0];
        }

        void Send( string pCommand )
        {
            // clear buffer
            ReadAll();

        }

        byte[] _tempReadBytes = new byte[4096];
        StringBuilder _tempReadString = new StringBuilder();
        List<string> _tempReadProcessLines = new List<string>();
        List<string> _tempReceiveLines = new List<string>();
        List<string> ReadAll()
        {
            _tempReadString.Clear();
            _tempReadProcessLines.Clear();
            _tempReceiveLines.Clear();

            if( _stream.DataAvailable )
            {

                var wait = new Stopwatch();

                do
                {
                    // read all the data until none left
                    if( _stream.DataAvailable )
                    {
                        var read = _stream.Read( _tempReadBytes, 0, _tempReadBytes.Length );
                        _tempReadString.Append( Encoding.ASCII.GetString( _tempReadBytes, 0, read ) );
                        wait.Restart();
                    }
                }
                while( wait.ElapsedMilliseconds < 10 );

                _tempReadProcessLines.AddRange(_tempReadString.ToString().Split(new [] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));


                // look for magic words
                foreach( var line in _tempReadProcessLines )
                {
                    Log.Write( Log.Severity.Debug, "zesarux: (in) [" + line + "]" );

                    if( line.StartsWith( "command> " ) )
                        _isRunning = true;
                    else if( line.StartsWith( "command@cpu-step> " ) )
                        _isRunning = false;
                    else
                        _tempReceiveLines.Add( line );
                }

            }

            return _tempReceiveLines;
        }
    }
}
