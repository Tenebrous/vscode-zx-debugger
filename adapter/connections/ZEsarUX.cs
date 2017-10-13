using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Spectrum;
using ZXDebug;
using ZXDebug.SourceMapper;
using Convert = ZXDebug.Convert;

namespace ZEsarUX
{
    public class Connection : ZXDebug.Connection
    {
        TcpClient _client;
        NetworkStream _stream;
        bool _connected;

        Logging.Severity _protocolLogLevel = Logging.Severity.Verbose;

        [Flags] enum DebugSettings // from https://sourceforge.net/p/zesarux/code/ci/master/tree/remote.c#l766
        {
            ShowAllRegistersAfterStep = 1,
            ShowNext8AfterStep = 2,
            DoNotAddLPrefixToLabels = 4,
            ShowOpcodeBytes = 8,
            RepeatLastCommandWithEnter = 16,
            StepOverInterrupt = 32
        }

        public override ConnectionCaps ConnectionCaps => new ConnectionCaps()
        {
            CanSetRegisters = true,
            CanEvaluate = true,
            CanStepOut = false,
            //CanStepOverSelectively = true, // wait for updated ZEsarUX
            MaxBreakpoints = 10 // assume 10 until proven otherwise
        };

        public override bool Connect()
        {
            if( _connected )
                return true;

            _client = new TcpClient();

            try
            {
                LogMessage( "Connecting..." );
                _client.Connect( "127.0.0.1", 10000 );
                _stream = _client.GetStream();
                _connected = true;

                // clear initial buffer ready for setup
                ReadAll();

                Setup();

                OnConnected();
            }
            catch( Exception e )
            {
                LogError( "Connection error: " + e );
                throw new Exception( "Cannot connect to ZEsarUX", e );
            }

            return _connected;
        }

        public override bool Disconnect()
        {
            LogMessage( "Disconnecting..." );

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

            LogMessage( "Disconnected" );

            return true;
        }

        Dictionary<string, string> _machineToType = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase )
        {
            { /* MK14     */ "MK14",                         "" },
            { /* ZX80     */ "ZX-80",                        "" },
            { /* ZX81     */ "ZX-81",                        "" },
            { /* 16k      */ "Spectrum 16k",                 "16k" },
            { /* 48k      */ "Spectrum 48k",                 "48k" },
            { /* 128k     */ "Spectrum 128k",                "128k" },
            { /* QL       */ "QL",                           "" },
            { /* P2       */ "Spectrum +2",                  "128k +2" },
            { /* P2F      */ "Spectrum +2 (French)",         "128k +2" },
            { /* P2S      */ "Spectrum +2 (Spanish)",        "128k +2" },
            { /* P2A40    */ "Spectrum +2A (ROM v4.0)",      "128k +2a" },
            { /* P2A41    */ "Spectrum +2A (ROM v4.1)",      "128k +2a" },
            { /* P2AS     */ "Spectrum +2A (Spanish)",       "128k +2a" },
            { /* TS2068   */ "Timex TS 2068",                "" },
            { /* Inves    */ "Inves Spectrum+",              "" },
            { /* 48ks     */ "Spectrum 48k (Spanish)",       "48k" },
            { /* 128ks    */ "Spectrum 128k (Spanish)",      "128k" },
            { /* TK90X    */ "Microdigital TK90X",           "" },
            { /* TK90XS   */ "Microdigital TK90X (Spanish)", "" },
            { /* TK95     */ "Microdigital TK95",            "" },
            { /* Z88      */ "Cambridge Z88",                "" },
            { /* Sam      */ "Sam Coupe",                    "" },
            { /* Pentagon */ "Pentagon",                     "" },
            { /* Chloe140 */ "Chloe 140 SE",                 "" },
            { /* Chloe280 */ "Chloe 280 SE",                 "" },
            { /* Chrome   */ "Chrome",                       "" },
            { /* Prism    */ "Prism",                        "" },
            { /* ZXUNO    */ "ZX-Uno",                       "" },
            { /* TSConf   */ "ZX-Evolution TS-Conf",         "" },
            { /* TBBlue   */ "TBBlue/ZX Spectrum Next",      "tbblue next" },
            { /* TBBlue   */ "TBBlue",                       "tbblue next" },
            { /* ACE      */ "Jupiter Ace",                  "" },
            { /* CPC464   */ "Amstrad CPC 464",              "" }
        };
        public override void ReadMachineCaps( MachineCaps pCaps )
        {
            var hardware = SendAndReceiveSingle( "get-current-machine" );

            pCaps.Clear();

            if( !_machineToType.TryGetValue( hardware, out var capList ) )
            {
                Logging.Write( Logging.Severity.Error, "Unknown machine '" + hardware + "'" );
                return;
            }

            var split = capList.Split( new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );

            foreach( var item in split )
                pCaps.Has[item] = true;
        }

        public override void ReadRegisters( Registers registers )
        {
            ParseRegisters( registers, SendAndReceiveSingle( "get-registers" ) );
        }

        public override void SetRegister( Registers registers, string register, ushort value )
        {
            var result = SendAndReceiveSingle( "set-register " + register + "=" + value );
            ParseRegisters( registers, result );
        }


        string _lastMemoryConfiguration = null;
        public override bool ReadMemoryConfiguration( Memory memory )
        {
            var pages = SendAndReceiveSingle( "get-memory-pages" );

            if( _lastMemoryConfiguration == pages )
                return false;

            _lastMemoryConfiguration = pages;

            // RO1 RA5 RA2 RA7 SCR5 PEN
            // O0 O1 A10 A11 A4 A5 A14 A15 SCR

            if( string.IsNullOrWhiteSpace( pages ) )
            {
                // no mapping info, so probably 16k/48k etc
                memory.PagingEnabled = false;
                var bank = memory.Bank( BankID.Unpaged() );
                memory.SetAddressBank( 0x0000, 0xFFFF, bank );
                return true;
            }

            var split = pages.Split( new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );

            ushort alignedAddress = 0;

            memory.PagingEnabled = true;

            memory.ClearConfiguration();

            foreach( var part in split )
            {
                if( part == "PEN" )
                    memory.PagingEnabled = true;
                else if( part == "PDI" )
                    memory.PagingEnabled = false;
                else if( part.StartsWith( "RO" ) )
                {
                    var all = BankID.ROM( int.Parse( part.Substring( 2 ) ) );
                    var low = memory.Bank( all.Low );
                    var high = memory.Bank( all.High );

                    memory.SetAddressBank( alignedAddress, 0x2000, low );
                    alignedAddress += 0x2000;

                    memory.SetAddressBank( alignedAddress, 0x2000, high );
                    alignedAddress += 0x2000;
                }
                else if( part.StartsWith( "RA" ) )
                {
                    var all = BankID.Bank( int.Parse( part.Substring( 2 ) ) );
                    var low = memory.Bank( all.Low );
                    var high = memory.Bank( all.High );

                    memory.SetAddressBank( alignedAddress, 0x2000, low );
                    alignedAddress += 0x2000;

                    memory.SetAddressBank( alignedAddress, 0x2000, high );
                    alignedAddress += 0x2000;
                }
                if( part.StartsWith( "O" ) )
                {
                    var bank = memory.Bank( BankID.ROM( int.Parse( part.Substring( 1 ) ) ) );
                    memory.SetAddressBank( alignedAddress, 0x2000, bank );
                    alignedAddress += 0x2000;
                }
                else if( part.StartsWith( "A" ) )
                {
                    var bank = memory.Bank( BankID.Bank( int.Parse( part.Substring( 1 ) ) ) );
                    memory.SetAddressBank( alignedAddress, 0x2000, bank );
                    alignedAddress += 0x2000;
                }
            }

            return true;
        }


        public override int ReadMemory( ushort address, byte[] bytes, int start = 0, int length = 0 )
        {
            if( length == 0 )
                length = bytes.Length - start;

            var memory = SendAndReceiveSingle( "read-memory " + address + " " + length );

            for( var i = 0; i < length; i++ )
                bytes[i + start] = (byte)( ( Convert.FromHex( memory[i * 2] ) << 4 ) | Convert.FromHex( memory[i * 2 + 1] ) );

            return length;
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
                LogMessage( "Timed out waiting for data" );

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

        void Send( string pCommand )
        {
            // clear buffer
            ReadAll();

            Log( ProtocolLogLevel, "(out) [" + pCommand + "]" );

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
                Log( ProtocolLogLevel, "(in)  [" + line + "]" );

                if( line.StartsWith( "command> " ) )
                    _isRunning = true;
                else if( line.StartsWith( "command@cpu-step> " ) )
                    _isRunning = false;
                else
                    pDestination.Add( line );
            }

            return pDestination;
        }

        public bool InitBreakpoints()
        {
            _enabledBreakpoints.Clear();

            for( int i = 1; i < ConnectionCaps.MaxBreakpoints; i++ )
                SendAndReceiveSingle( "disable-breakpoint " + i );

            return true;
        }


        void ReadBreakpoints()
        {
            SendAndReceive( "get-breakpoints" );
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

        void GetVersion()
        {
            var version = SendAndReceiveSingle("get-version");
            LogMessage( "Connected (" + version + ")" );
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

            ConnectionCaps.MaxBreakpoints = count;
        }

        HashSet<int> _enabledBreakpoints = new HashSet<int>();
        void SetSingleBreakpoint( Breakpoint pBreakpoint )
        {
            var addr = pBreakpoint.Bank.PagedAddress + pBreakpoint.Line.Offset;
            SendAndReceiveSingle( string.Format( $"set-breakpoint {pBreakpoint.Index + 1} PC={addr:X4}h" ) );
            SendAndReceiveSingle( "enable-breakpoint " + ( pBreakpoint.Index + 1 ), pRaiseErrors: false );
        }

        public Logging.Severity ProtocolLogLevel
        {
            get
            {
                return _protocolLogLevel;
            }

            set
            {
                _protocolLogLevel = value;
            }
        }

        protected override string LogPrefix
        {
            get { return "ZEsarUX"; }
        }
    }
}
