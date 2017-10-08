using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Spectrum;
using ZXDebug;
using Convert = ZXDebug.Convert;

namespace ZEsarUX
{
    public class Connection : MachineConnection
    {
        TcpClient _client;
        NetworkStream _stream;
        bool _connected;

        Log.Severity _protocolLogLevel = Log.Severity.Verbose;

        [Flags] enum DebugSettings // from https://sourceforge.net/p/zesarux/code/ci/master/tree/remote.c#l766
        {
            ShowAllRegistersAfterStep = 1,
            ShowNext8AfterStep = 2,
            DoNotAddLPrefixToLabels = 4,
            ShowOpcodeBytes = 8,
            RepeatLastCommandWithEnter = 16,
            StepOverInterrupt = 32
        }

        public override Meta Meta => new Meta()
        {
            CanSetRegisters = true,
            CanEvaluate = true,
            CanStepOut = false,
            //CanStepOverSensibly = true // wait for updated ZEsarUX
            MaxBreakpoints = 10
        };
        
        public override bool Connect()
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

                // clear initial buffer ready for setup
                ReadAll();

                Setup();
            }
            catch( Exception e )
            {
                Log.Write( Log.Severity.Error, "zesarux: connection error: " + e );
                throw new Exception( "Cannot connect to ZEsarUX", e );
            }

            return _connected;
        }

        void GetVersion()
        {
            var version = SendAndReceiveSingle("get-version");
            Log.Write( Log.Severity.Message, "zesarux: connected (" + version + ")" );
        }

        Regex _breakpointCounter = new Regex(
            @"(?'number'\d+):",
            RegexOptions.Compiled
        );

        void GetBreakpointCount()
        {
            var count = 0;
            var breakpoints = SendAndReceive( "get-breakpoints" );

            foreach( var breakpoint in breakpoints )
            {
                var match = _breakpointCounter.Match( breakpoint );
                if( match.Success )
                {
                    var num = int.Parse( match.Groups["number"].Value );
                    if( num > count ) count = num;
                }
            }

            Meta.MaxBreakpoints = count;
        }

        public override bool Disconnect()
        {
            Log.Write( Log.Severity.Message, "zesarux: disconnecting..." );

            if( _stream != null )
            {
                SendAndReceiveSingle( "disable-breakpoints" );
                SendAndReceiveSingle( "exit" );
            }

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

            Log.Write( Log.Severity.Message, "zesarux: disconnected" );

            return true;
        }


        public void Setup()
        {
            GetVersion();
            GetBreakpointCount();

            var debugSettings = DebugSettings.ShowOpcodeBytes | DebugSettings.StepOverInterrupt;
            SendAndReceiveSingle( "set-debug-settings " + (int)debugSettings );
            SendAndReceiveSingle( "set-memory-zone -1" );
            SendAndReceiveSingle( "enable-breakpoints", pRaiseErrors: false );

            InitBreakpoints();
            ReadBreakpoints();
        }


        public override bool Pause()
        {
            Send( "enter-cpu-step" );
            return true;
        }


        public override bool Continue()
        {
            _isRunning = true;

            OnContinued();
            Send( "run" );

            return true;
        }


        public override bool StepOver()
        {
            _isRunning = true;

            OnContinued();
            Send( "cpu-step-over" );

            return true;
        }


        public override bool Step()
        {
            _isRunning = true;

            OnContinued();
            Send( "cpu-step" );

            return true;
        }

        void SetSingleBreakpoint( Breakpoint pBreakpoint )
        {
            var addr = pBreakpoint.Bank.LastAddress + pBreakpoint.Line.Offset;
            SendAndReceiveSingle( string.Format($"set-breakpoint {pBreakpoint.Index+1} PC={addr:X4}h") );
            SendAndReceiveSingle( "enable-breakpoint " + (pBreakpoint.Index+1), pRaiseErrors: false );
        }

        HashSet<int> _enabledBreakpoints = new HashSet<int>();

        public override bool SetBreakpoints( Breakpoints breakpoints )
        {
            // remove those no longer set
            foreach( var current in _enabledBreakpoints )
            {
                var remove = true;
                foreach( var wanted in breakpoints )
                {
                    if( wanted.Index == current )
                    {
                        remove = false;
                        break;
                    }
                }

                if( remove )
                    SendAndReceiveSingle( "disable-breakpoint " + (current+1) );
            }

            foreach( var b in breakpoints )
            {
                _enabledBreakpoints.Add( b.Index );
                SetSingleBreakpoint( b );
            }

            return true;
        }

        public override bool SetBreakpoint( Breakpoints breakpoints, Breakpoint breakpoint )
        {
            return true;
        }

        public override bool RemoveBreakpoints( Breakpoints breakpoints )
        {
            return true;
        }
        
        public override bool RemoveBreakpoint( Breakpoints breakpoints, Breakpoint breakpoint )
        {
            return true;
        }

        public bool InitBreakpoints()
        {
            _enabledBreakpoints.Clear();

            for( int i = 1; i < Meta.MaxBreakpoints; i++ )
                    SendAndReceiveSingle( "disable-breakpoint " + i );

            return true;
        }


        void ReadBreakpoints()
        {
            SendAndReceive( "get-breakpoints" );
        }


        public override void RefreshMemoryPages( Memory memory )
        {
            var pages = SendAndReceiveSingle( "get-memory-pages" );
            // RO1 RA5 RA2 RA7 SCR5 PEN

            if( string.IsNullOrWhiteSpace( pages ) )
            {
                // no mapping info, so probably 16k/48k etc
                memory.PagingEnabled = false;
                var bank = memory.Bank( BankID.Unpaged() );
                memory.SetAddressBank( 0x0000, 0xFFFF, bank );
                return;
            }

            var split = pages.Split( new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );

            ushort slotSize = memory.SlotSize;
            ushort slotPos = 0;

            memory.PagingEnabled = true;

            foreach( var part in split )
            {
                if( part == "PEN" )
                    memory.PagingEnabled = true;
                else if( part == "PDI" )
                    memory.PagingEnabled = false;
                else if( part.StartsWith( "RO" ) )
                {
                    var bank = memory.Bank( BankID.ROM( int.Parse( part.Substring( 2 ) ) ) );
                    memory.SetAddressBank( (ushort)( slotPos * slotSize ), (ushort)( slotPos * slotSize + slotSize - 1 ), bank );
                    slotPos++;
                }
                else if( part.StartsWith( "RA" ) )
                {
                    var bank = memory.Bank( BankID.Bank( int.Parse( part.Substring( 2 ) ) ) );
                    memory.SetAddressBank( (ushort)( slotPos * slotSize ), (ushort)( slotPos * slotSize + slotSize - 1 ), bank );
                    slotPos++;
                }
            }
        }

        public override int ReadMemory( ushort address, byte[] bytes, int start, int length )
        {
            var memory = SendAndReceiveSingle( "read-memory " + address + " " + length );

            for( var i = 0; i < length; i++ )
                bytes[i+start] = (byte)( ( Convert.FromHex( memory[i * 2] ) << 4 ) | Convert.FromHex( memory[i * 2 + 1] ) );

            return length;
        }

        public override void RefreshRegisters( Registers registers )
        {
            ParseRegisters( registers, SendAndReceiveSingle( "get-registers" ) );
        }

        public override void SetRegister( Registers registers, string register, ushort value )
        {
            var result = SendAndReceiveSingle( "set-register " + register + "=" + value );
            ParseRegisters( registers, result );
        }

        // [PC=0038 SP=ff4a BC=174b A=00 HL=107f DE=0006 IX=ffff IY=5c3a A'=00 BC'=0b21 HL'=ffff DE'=5cb9 I=3f R=22  F= Z P3H   F'= Z P     MEMPTR=15e6 DI IM1 VPS: 0 ]
        Regex _matchRegisters = new Regex(@"(?i)(?'register'[a-z']*)=(?'value'[0-9a-f].*?)(?:\s)");
        Regex _matchFlags = new Regex(@"(?i)(?'register'F\'?)=(?'s'.{1})(?'z'.{1})(?'bit5'.{1})(?'pv'.{1})(?'bit3'.{1})(?'h'.{1})(?'n'.{1})(?'c'.{1})");
        void ParseRegisters( Registers pRegisters, string pString )
        {
            var matches = _matchRegisters.Matches(pString);
            foreach( Match match in matches )
            {
                try
                {
                    pRegisters[match.Groups["register"].Value] = Convert.Parse( match.Groups["value"].Value, isHex: true );
                }
                catch
                {
                    // ignore
                }
            }

            matches = _matchFlags.Matches(pString);
            foreach( Match match in matches )
            {
                // match.Groups["register"].Value
                // match.Groups["s"].Value
                // match.Groups["z"].Value
                // match.Groups["bit5"].Value
                // match.Groups["h"].Value
                // match.Groups["bit3"].Value
                // match.Groups["n"].Value
                // match.Groups["c"].Value
            }
        }


        public override bool IsConnected
        {
            get { return _client != null && _client.Connected; }
        }


        bool _isRunning = true;
        public bool IsRunning
        {
            get { return _isRunning; }
        }

        string _hardware;
        public string Hardware
        {
            get { return _hardware; }
        }

        public string GetHardware()
        {
            return _hardware = SendAndReceiveSingle( "get-current-machine" );
        }

        public override bool Process()
        {
            // read any unsolicited messages coming from zesarux

            if( !IsConnected )
                return false;

            var wasRunning = _isRunning;

            var result = ReadAll();

            if( _isRunning != wasRunning )
            {
                if( _isRunning )
                    OnContinued();
                else
                    OnPaused();
            }

            //if( result.Count > 0 )
            //    OnData?.Invoke( result );

            return result.Count > 0;
        }

        public override List<string> CustomCommand( string cmd, List<string> results = null )
        {
            var result = SendAndReceive( cmd, results );
            return result;
        }

        List<string> SendAndReceive( string pCommand, List<string> pResults = null, bool pRaiseErrors = true )
        {
            if( !IsConnected )
                return null;

            pResults = pResults ?? new List<string>();
            pResults.Clear();

            Send( pCommand );


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

                if( pRaiseErrors )
                    throw new Exception( "ZEsarUX did not respond within 2 seconds" );

                return null;
            }

            ReadAll( pResults );

            // check for errors

            if( pRaiseErrors )
                pResults.ForEach(
                    pLine =>
                    {
                        if( pLine.StartsWith( "error", StringComparison.InvariantCultureIgnoreCase ) )
                            throw new Exception( "ZEsarUX reports: " + pLine );
                    }
                );

            return pResults;
        }

        string SendAndReceiveSingle( string pCommand, bool pRaiseErrors = true )
        {
            var result = SendAndReceive( pCommand, null, pRaiseErrors );

            if( result == null || result.Count == 0 )
                return "";

            return result[0];
        }

        public void Send( string pCommand )
        {
            // clear buffer
            ReadAll();

            Log.Write( _protocolLogLevel, "zesarux: (out) [" + pCommand + "]" );

            var bytes = Encoding.ASCII.GetBytes(pCommand + "\n");
            _stream.Write( bytes, 0, bytes.Length );
            _stream.Flush();
        }

        byte[] _tempReadBytes = new byte[4096];
        StringBuilder _tempReadString = new StringBuilder();
        List<string> _tempReadProcessLines = new List<string>();

        List<string> _tempReceiveLines = new List<string>();
        public List<string> ReadAll( List<string> pDestination = null )
        {
            pDestination = pDestination ?? _tempReceiveLines;

            _tempReadString.Clear();
            _tempReadProcessLines.Clear();

            pDestination.Clear();

            if( !_stream.DataAvailable )
                return pDestination;

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

            _tempReadProcessLines.AddRange(
                _tempReadString.ToString().Split(
                    new [] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries
                )
            );

            // look for magic words
            foreach( var line in _tempReadProcessLines )
            {
                Log.Write( _protocolLogLevel, "zesarux: (in)  [" + line + "]" );

                if( line.StartsWith( "command> " ) )
                    _isRunning = true;
                else if( line.StartsWith( "command@cpu-step> " ) )
                    _isRunning = false;
                else
                    pDestination.Add( line );
            }

            return pDestination;
        }
    }
}