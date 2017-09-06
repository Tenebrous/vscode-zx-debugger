using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace ZDebug
{
    public class ConnectionZEsarUX
    {
        public Action OnPaused;
        public Action OnContinued;
        public Action OnStepped;
        public Action<string, string> OnRegisterChange;

        TcpClient _client;
        NetworkStream _stream;
        bool _connected;

        string _disassemblyFilePath;
        List<DisassemblySection> _disassembledSections = new List<DisassemblySection>();

        Dictionary<string, int> _registers = new Dictionary<string, int>();

        
        public bool Start()
        {
            if( _connected )
                return true;

            _client = new TcpClient();

            try
            {
                ZMain.Log( "zesarux: connecting..." );
                _client.Connect( "127.0.0.1", 10000 );
                _stream = _client.GetStream();
                _connected = true;
                ZMain.Log( "zesarux: connected." );
            }
            catch ( Exception e )
            {
                ZMain.Log( "zesarux: connection error: " + e );
                return false;
            }

            return _connected;
        }


        public void Stop()
        {
            ZMain.Log("zesarux: disconnecting...");

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

            ZMain.Log("zesarux: disconnected.");
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

            _stack.Add( PC );

            foreach (var frame in split)
                if( frame.EndsWith("H") )
                    _stack.Add( UnHex(frame.Substring(0, frame.Length - 1)) );
                else
                    _stack.Add( UnHex(frame) );

            return _stack;
        }


        public Dictionary<string,int> GetRegisters()
        {
            var registers = SimpleCommand( "get-registers" );

            // [PC=0038 SP=ff4a BC=174b A=00 HL=107f DE=0006 IX=ffff IY=5c3a A'=00 BC'=0b21 HL'=ffff DE'=5cb9 I=3f R=22  F= Z P3H   F'= Z P     MEMPTR=15e6 DI IM1 VPS: 0 ]

            var regMatch = new Regex("(PC|SP|BC|A|HL|DE|IX|IY|A'|BC'|HL'|DE'|I|R)=(.*?) ");

            var matches = regMatch.Matches(registers);
            foreach (Match match in matches)
            {
                var register = match.Groups[1].ToString();
                var value = match.Groups[2].ToString();

                SetRegister(register, UnHex(value));
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

        public void GetPorts()
        {
            var ports = Command( "get-io-ports" );
        }


        public void UpdateDisassembly( int pAddress )
        {
            if( _disassemblyFilePath == null )
                _disassemblyFilePath = Path.Combine( Path.GetTempPath(), "Disassembly.z80" );

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
                    var address = UnHex( parts[0] );

                    newSection.Start = Math.Min( newSection.Start, address );
                    newSection.End   = Math.Max( newSection.End,   address );

                    newSection.Lines.Add( 
                        new DisassemblyLine()
                        {
                            Address = address,
                            Code = parts[1]
                        }
                    );
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
                tmp.Add( string.Format( "-{0:X4}-{1:X4}-", section.Start, section.End ) );
                foreach( var line in section.Lines )
                {
                    tmp.Add( string.Format( "{0:X4} {1}", line.Address, line.Code ) );
                    line.FileLine = tmp.Count;
                }

                tmp.Add( "" );
            }

            File.WriteAllLines( _disassemblyFilePath, tmp );
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


        public Dictionary<string, int> Registers
        {
            get { return _registers; }
        }


        public int PC
        {
            get
            {
                int pc;
                _registers.TryGetValue("PC", out pc);
                return pc;
            }
        }


        public string DisassemblyFilePath
        {
            get { return _disassemblyFilePath; }
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
            ZMain.Log("zesarux: -> [" + pCommand + "]");

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
                ZMain.Log( "zesarux: timed out waiting for data" );
                return null;
            }

            var lines = ReadAll();

            // log the received data
            // _tempReceiveLines.ForEach( pLine => ZMain.Log( "zesarux: <- [" + pLine + "]" ) );

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

                _tempReadProcessLines.AddRange(_tempReadString.ToString().Split(new [] { '\n' }, StringSplitOptions.RemoveEmptyEntries));


                // look for magic words
                foreach( var line in _tempReadProcessLines )
                {
                    ZMain.Log( "zesarux: <- [" + line + "]" );

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

        void SetRegister( string pRegister, int pValue )
        {
            //if (_registers.ContainsKey(pRegister))
            //    if (_registers[pRegister] != pValue)
            //        if (OnRegisterChange != null)
            //            OnRegisterChange(pRegister, pValue);

            _registers[pRegister] = pValue;
        }

        int UnHex( string pHex )
        {
            return Convert.ToInt32( pHex, 16 );
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
