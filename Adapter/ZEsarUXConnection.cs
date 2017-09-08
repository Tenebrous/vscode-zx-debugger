using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using VSCodeDebugAdapter;

namespace ZEsarUXDebugger
{
    public class ZEsarUXConnection
    {
        public Action OnPaused;
        public Action OnContinued;
        public Action OnStepped;
        public Action<string, string> OnRegisterChange;

        TcpClient _client;
        NetworkStream _stream;
        bool _connected;
        
        List<DisassemblySection> _disassembledSections = new List<DisassemblySection>();

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

                var version = SimpleCommand("get-version");
                Log.Write( Log.Severity.Message, "zesarux: connected (" + version + ")." );

                ReadAll();
                Setup();
            }
            catch ( Exception e )
            {
                Log.Write( Log.Severity.Error, "zesarux: connection error: " + e );
                return false;
            }

            return _connected;
        }


        public void Stop()
        {
            Log.Write( Log.Severity.Message, "zesarux: disconnecting..." );

            if( _stream != null )
                SimpleCommand( "exit" );

            if ( _stream != null )
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
        }


        public void Pause()
        {
            SimpleCommand( "enter-cpu-step" );
        }


        public void Continue()
        {
            SimpleCommand( "exit-cpu-step" );
        }


        public void Step()
        {
            _wasRunning = true;
            SimpleCommand( "cpu-step" );
        }


        public void StepOver()
        {
            _wasRunning = true;
            SimpleCommand( "cpu-step-over" );
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

            SimpleCommand("set-debug-settings 8");
        }

        public string GetMachine()
        {
            return _machine = SimpleCommand("get-current-machine");
        }


        public List<int> GetStackTrace()
        {
            var stack = SimpleCommand( "get-stack-backtrace" );

            // [15E6H 15E1H 0F3BH 107FH FF54H]
            var split = stack.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            _stack.Clear();

            _stack.Add( _registers.PC );

            foreach (var frame in split)
                if( frame.EndsWith("H") )
                    _stack.Add( UnHex(frame.Substring(0, frame.Length - 1)) - 3 );
                else
                    _stack.Add( UnHex(frame) - 3 );

            return _stack;
        }

        public class RegistersClass
        {
            public ushort PC;
            public ushort SP;

            public byte A;
            public ushort BC;
            public ushort DE;
            public ushort HL;

            public byte AltA;
            public ushort AltBC;
            public ushort AltDE;
            public ushort AltHL;

            public ushort IX;
            public ushort IY;

            public byte I;
            public byte R;
        }

        public RegistersClass GetRegisters()
        {
            var reg = _registers;

            var regString = SimpleCommand( "get-registers" );
            // [PC=0038 SP=ff4a BC=174b A=00 HL=107f DE=0006 IX=ffff IY=5c3a A'=00 BC'=0b21 HL'=ffff DE'=5cb9 I=3f R=22  F= Z P3H   F'= Z P     MEMPTR=15e6 DI IM1 VPS: 0 ]

            var regMatch = new Regex("(PC|SP|BC|A|HL|DE|IX|IY|A'|BC'|HL'|DE'|I|R)=(.*?) ");

            var matches = regMatch.Matches(regString);
            foreach (Match match in matches)
            {
                var register = match.Groups[1].ToString();
                var value = UnHex(match.Groups[2].ToString());

                switch( register )
                {
                    case "PC":
                        reg.PC = value;
                        break;

                    case "SP":
                        reg.SP = value;
                        break;

                    case "BC":
                        reg.BC = value;
                        break;

                    case "A":
                        reg.A = (byte)value;
                        break;

                    case "HL":
                        reg.HL = value;
                        break;

                    case "DE":
                        reg.DE = value;
                        break;

                    case "IX":
                        reg.IX = value;
                        break;

                    case "IY":
                        reg.IY = value;
                        break;

                    case "A'":
                        reg.AltA = (byte)value;
                        break;

                    case "BC'":
                        reg.AltBC = value;
                        break;

                    case "HL'":
                        reg.AltHL = value;
                        break;

                    case "DE'":
                        reg.AltDE = value;
                        break;

                    case "I":
                        reg.I = (byte)value;
                        break;

                    case "R":
                        reg.R = (byte)value;
                        break;

                }
            }

            //var flags = new Regex( "(F'|F)=(.{8}) " );
            //
            //matches = flags.Matches(pData);
            //foreach (Match match in matches)
            //{
            //    var register = match.Groups[1].ToString();
            //    var value = match.Groups[2].ToString().Trim();
            //
            //    SetRegister(register, value);
            //}

            return _registers;
        }

        public void Dump( Value pValue )
        {

        }

        public void GetPorts()
        {
            var ports = Command( "get-io-ports" );
        }

        public string GetMemory( ushort pAddress, int pLength )
        {
            var memory = SimpleCommand( "read-memory " + pAddress + " " + pLength );
            return memory;
        }


        public void UpdateDisassembly( int pAddress )
        {
            var file = Path.Combine( Path.GetTempPath(), "Disassembly.z80" );

            foreach( var section in _disassembledSections )
                if( pAddress >= section.Start && pAddress <= section.End - 10 )
                    return;

            var newSection = new DisassemblySection() { Start = 0xFFFFF };

            var lines = Command("disassemble " + pAddress + " " + 30);

            if( lines != null )
            {
                foreach( var line in lines )
                {
                    var parts = line.Split( new [] {' '}, 2, StringSplitOptions.RemoveEmptyEntries );

                    var address = UnHex(parts[0]);

                    newSection.Start = Math.Min(newSection.Start, address);
                    newSection.End = Math.Max(newSection.End, address);

                    newSection.Lines.Add(
                        new DisassemblyLine()
                        {
                            Address = address,
                            Code = parts[1]
                        }
                    );

                    // stop disassembling at hard RET (just testing to see if that makes things clearer)
                    if( parts[1].Substring( 0, 2 ) == "C9" )
                        break;
                }


                // look to see if we cover two existing sections, whereby we'll merge them
                for( int i = 0; i < _disassembledSections.Count - 1; i++ )
                    if( newSection.Start <= _disassembledSections[i].End && newSection.End >= _disassembledSections[i+1].Start )
                    {
                        _disassembledSections[i].End = _disassembledSections[i + 1].End;
                        _disassembledSections[i].Lines.AddRange( _disassembledSections[i+1].Lines );
                        _disassembledSections.RemoveAt( i + 1 );
                        break;
                    }

                
                // find relevant section to add lines to
                DisassemblySection add = null;
                foreach( var section in _disassembledSections )
                    if( newSection.End >= section.Start && newSection.Start <= section.End )
                    {
                        add = section;
                        break;
                    }

                if( add == null )
                {
                    // created new section
                    _disassembledSections.Add( newSection );
                }
                else
                {
                    // merge with existing section
                    
                    foreach( var line in newSection.Lines )
                    {
                        bool exists = false;

                        foreach ( var otherLine in add.Lines )
                            if( line.Address == otherLine.Address )
                            {
                                exists = true;
                                break;
                            }

                        if( !exists )
                            add.Lines.Add( line );
                    }

                    add.Lines.Sort( ( pLeft, pRight ) => pLeft.Address.CompareTo( pRight.Address ) );
                }

                // re-sort sections
                _disassembledSections.Sort( ( pLeft, pRight ) => pLeft.Start.CompareTo( pRight.Start ) );
            }

            var tmp = new List<string>();
            foreach( var section in _disassembledSections )
            {
                foreach( var line in section.Lines )
                {
                    tmp.Add( string.Format( "{0:X4} {1}", line.Address, line.Code ) );
                    line.FileLine = tmp.Count;
                }

                tmp.Add( "" );
            }

            File.WriteAllLines( DisassemblyFile, tmp );
        }

        public int FindLine( int pAddress )
        {
            foreach( var section in _disassembledSections )
                foreach( var line in section.Lines )
                    if( line.Address == pAddress )
                        return line.FileLine;

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

        public enum RunningStateChangeEnum
        {
            NoChange,
            Stopped,
            Started
        }

        public RunningStateChangeEnum StateChange
        {
            get
            {
                var result = RunningStateChangeEnum.NoChange;

                if( _wasRunning && !_isRunning )
                    result = RunningStateChangeEnum.Stopped;
                else if( !_wasRunning && _isRunning )
                    result = RunningStateChangeEnum.Started;

                _wasRunning = _isRunning;

                return result;
            }
        }


        RegistersClass _registers = new RegistersClass();
        public RegistersClass Registers
        {
            get { return _registers; }
        }

        public string DisassemblyFile
        {
            get { return Path.Combine( _tempFolder, "disasm.z80" ); }
        }

        string _machine;
        public string Machine
        {
            get { return _machine; }
        }

        List<int> _stack = new List<int>();
        public List<int> Stack
        {
            get { return _stack; }
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

            return result.Count > 0;
        }


        List<string> Command( string pCommand )
        {
            if( !Start() )
                return null;

            _tempReceiveLines.Clear();


            // send command to zesarux
            Log.Write( Log.Severity.Debug, "zesarux: (out) [" + pCommand + "]" );

            var bytes = Encoding.ASCII.GetBytes(pCommand + "\n");
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();


            // limit how long we wait
            var x = new Stopwatch();
            x.Start();

            // wait for data to start coming back
            while ( !_stream.DataAvailable && x.Elapsed.Seconds < 2 )
                ;
            
            if( !_stream.DataAvailable )
            {
                Log.Write( Log.Severity.Message, "zesarux: timed out waiting for data" );
                return null;
            }

            var lines = ReadAll();

            // log the received data
            // _tempReceiveLines.ForEach( pLine => Log.Write( "zesarux: <- [" + pLine + "]" ) );

            return _tempReceiveLines;
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

        string SimpleCommand( string pCommand )
        {
            var result = Command( pCommand );

            if( result == null || result.Count == 0 )
                return "";

            return result[0];
        }

        ushort UnHex( string pHex )
        {
            return Convert.ToUInt16( pHex, 16 );
        }
    }

    class DisassemblySection
    {
        public int Start;
        public int End;
        public List<DisassemblyLine> Lines = new List<DisassemblyLine>();
    }

    class DisassemblyLine
    {
        public int Address;
        public string Code;
        public int FileLine;
    }
}
