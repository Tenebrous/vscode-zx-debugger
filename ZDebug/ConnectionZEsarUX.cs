using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

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

        bool _requestedPause;
        bool _requestedStep;
        bool _requestedContinue;

        bool _requestedDisassembly;
        DisassemblySection _disassembling;
        string _disassemblyFilePath;
        List<DisassemblySection> _disassembledSections = new List<DisassemblySection>();

        bool _requestedMachine;
        string _machine;

        Dictionary<string, string> _registers = new Dictionary<string, string>();

        bool _requestedStack;
        List<string> _stack = new List<string>();
        
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
                ZMain.Log( "zesarux: error " + e );
                return false;
            }

            return _connected;
        }

        public void Stop()
        {
            ZMain.Log("zesarux: disconnecting...");

            Send( "exit" );
            Read();

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
            _requestedPause = true;
            Send( "enter-cpu-step" );
        }

        public void Continue()
        {
            _requestedPause = false;
            _requestedStep = false;
            Send( "exit-cpu-step" );
        }

        public void Step()
        {
            _requestedStep = true;
            Send( "cpu-step" );
        }

        public void StepOver()
        {
            _requestedStep = true;
            Send( "cpu-step-over" );
        }

        public void GetStackTrace()
        {
            // [15E6H 15E1H 0F3BH 107FH FF54H ]
            _requestedStack = true;
            Send( "get-stack-backtrace" );
            Read();
        }

        public void GetRegisters()
        {
            // [PC=0038 SP=ff4a BC=174b A=00 HL=107f DE=0006 IX=ffff IY=5c3a A'=00 BC'=0b21 HL'=ffff DE'=5cb9 I=3f R=22  F= Z P3H   F'= Z P     MEMPTR=15e6 DI IM1 VPS: 0 ]
            Send( "get-registers" );
            Read();
        }

        public void Disassemble( string pAddress, int pCount )
        {
            if( _disassemblyFilePath == null )
                _disassemblyFilePath = Path.Combine( Path.GetTempPath(), "Disassembly.z80" );

            _requestedDisassembly = true;
            Send( "disassemble " + pAddress + " " + pCount );
            Read();
            _requestedDisassembly = false;

            _disassembling = new DisassemblySection() { Start = 0xFFFFF };
            ParseDisassembly();
        }

        public string GetMachine()
        {
            _requestedMachine = true;
            Send( "get-current-machine" );
            Read();
            return _machine;
        }


        public Dictionary<string, string> Registers
        {
            get { return _registers; }
        }

        public string DisassemblyFilePath
        {
            get { return _disassemblyFilePath; }
        }

        //public string Disassembly
        //{
        //    get { return _disassembly.ToString(); }
        //}

        public string Machine
        {
            get { return _machine; }
        }

        public List<string> Stack
        {
            get { return _stack; }
        }


        StringBuilder _s = new StringBuilder();
        string _lastCommand;

        public bool Read()
        {
            if( !IsConnected )
                return false;

            var result = _stream.DataAvailable;

            while( _stream.DataAvailable )
                _s.Append((char)_stream.ReadByte());

            if( _s.Length > 0 )
            {
                var data = _s.ToString().Split( '\n' );

                foreach( var line in data )
                {
                    ZMain.Log( "zesarux: <- (" + _lastCommand + ") = [" + line + "]" );
                    Process( line );
                }

                _s.Clear();
            }

            return result;
        }

        void Process( string pResult )
        {
            switch( pResult )
            {
                case "command> ":

                    if(_requestedContinue)
                    {
                        _requestedContinue = false;
                        ZMain.Log("zesarux: cpu is now running");
                        OnContinued?.Invoke();
                    }

                    _lastCommand = null;

                    break;

                case "command@cpu-step> ":

                    if( _requestedPause )
                    {
                        _requestedPause = false;
                        _requestedStep = false;

                        ZMain.Log( "zesarux: cpu is now stopped (pause)" );
                        OnPaused?.Invoke();
                    }

                    if( _requestedStep )
                    {
                        _requestedStep = false;

                        ZMain.Log("zesarux: cpu is now stopped (step)");
                        OnStepped?.Invoke();
                    }

                    _lastCommand = null;

                    break;

                default:

                    if( pResult.StartsWith( "PC=" ) )
                        ParseRegisters( pResult );
                    else if( _requestedStack )
                    {
                        ParseStack( pResult );
                        _requestedStack = false;
                    }
                    else if( _requestedDisassembly && _disassemblyFilePath != null )
                        ParseDisassemblyLine( pResult );
                    else if( _requestedMachine )
                    {
                        _machine = pResult;
                        _requestedMachine = false;
                    }

                    break;
            }
        }

        void SetRegister( string pRegister, string pValue )
        {
            if (_registers.ContainsKey(pRegister))
                if (_registers[pRegister] != pValue)
                    if (OnRegisterChange != null)
                        OnRegisterChange(pRegister, pValue);

            _registers[pRegister] = pValue;
        }

        void ParseRegisters( string pData )
        {
            // [PC=15f8 SP=ff4a BC=0b21 A=00 HL=5cb8 DE=5ca8 IX=ffff IY=5c3a A'=00 BC'=174b HL'=107
            //  DE'=0006 I=3f R=3a  F= Z P3H   F'= Z P     MEMPTR=15f7 EI IM1 VPS: 0 ]
            var registers = new Regex(
                "(PC|SP|BC|A|HL|DE|IX|IY|A'|BC'|HL'|DE'|I|R)=(.*?) "
            );

            var matches = registers.Matches(pData);
            foreach (Match match in matches)
            {
                ZMain.Log(match.ToString());
                foreach (var g in match.Groups)
                    ZMain.Log("   [" + g.ToString() + "]");

                var register = match.Groups[1].ToString();
                var value = match.Groups[2].ToString();

                SetRegister(register, value);
            }

            var flags = new Regex(
                "(F'|F)=(.{8}) "
            );

            matches = flags.Matches(pData);
            foreach (Match match in matches)
            {
                ZMain.Log(match.ToString());
                foreach (var g in match.Groups)
                    ZMain.Log("   [" + g.ToString() + "]");

                var register = match.Groups[1].ToString();
                var value = match.Groups[2].ToString().Trim();

                SetRegister(register, value);
            }
        }

        void ParseStack( string pData )
        {
            // [15E6H 15E1H 0F3BH 107FH FF54H]
            var split = pData.Split( new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );

            _stack.Clear();
            foreach( var frame in split )
                if( frame.EndsWith( "H" ) )
                    _stack.Add( frame.Substring( 0, frame.Length-1 ) );
                else
                    _stack.Add( frame );
        }

        void ParseDisassemblyLine( string pLine )
        {
            var data = pLine.Split( new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries );

            var address = UnHex( data[0] );

            _disassembling.Lines.Add(  new DisassemblyLine() { Address = address, Code = data[1] } );
            _disassembling.Start = Math.Min( _disassembling.Start, address );
            _disassembling.End   = Math.Max( _disassembling.End,   address );
        }

        void ParseDisassembly()
        {
            // todo: merge the freshly-disassembled stuff into the list and write to file
        }

        int UnHex( string pHex )
        {
            return Convert.ToInt32( pHex, 16 );
        }

        public bool Send( string pMessage )
        {
            if (!Start())
                return false;

            ZMain.Log("zesarux: -> [" + pMessage + "]");

            _lastCommand = pMessage;

            var bytes = System.Text.Encoding.ASCII.GetBytes( pMessage + "\n\n" );
            _stream.Write( bytes, 0, bytes.Length );
            _stream.Flush();

            Thread.Sleep( 150 );

            Read();

            return true;
        }

        public bool IsConnected
        {
            get { return _client != null && _client.Connected; }
        }
    }

    class DisassemblySection
    {
        public int Start;
        public int End;
        public List<DisassemblyLine> Lines;
    }

    class DisassemblyLine
    {
        public int Address;
        public string Code;
    }
}
