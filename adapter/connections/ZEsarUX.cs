using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Spectrum;
using ZXDebug;
using Convert = ZXDebug.Convert;

namespace ZEsarUX
{
    public class Connection : ZXDebug.Connection
    {
        TcpClient _client;
        NetworkStream _stream;
        bool _connected;

        // [PC=0038 SP=ff4a BC=174b A=00 HL=107f DE=0006 IX=ffff IY=5c3a A'=00 BC'=0b21 HL'=ffff DE'=5cb9 I=3f R=22  F= Z P3H   F'= Z P     MEMPTR=15e6 DI IM1 VPS: 0 ]
        //  ^^^^^^^ ^^^^^^^ ^^^^^^^ ^^^^ ^^^^^^^ ^^^^^^^ ^^^^^^^ ^^^^^^^ ^^^^^ ^^^^^^^^ ^^^^^^^^ ^^^^^^^^ ^^^^ ^^^^                         ^^^^^^^^^^^
        Regex _regexRegisters = new Regex(
            @"(?i)(?'register'[a-z']*)=(?'value'[0-9a-f].*?)(?:\s)",
            RegexOptions.Compiled
        );

        // [PC=0038 SP=ff4a BC=174b A=00 HL=107f DE=0006 IX=ffff IY=5c3a A'=00 BC'=0b21 HL'=ffff DE'=5cb9 I=3f R=22  F= Z P3H   F'= Z P     MEMPTR=15e6 DI IM1 VPS: 0 ]
        //                                                                                                           ^^^^^^^^^^ ^^^^^^^^^^^
        Regex _regexFlags = new Regex(
            @"(?i)(?'register'F\'?)=(?'s'.{1})(?'z'.{1})(?'bit5'.{1})(?'pv'.{1})(?'bit3'.{1})(?'h'.{1})(?'n'.{1})(?'c'.{1})",
            RegexOptions.Compiled
        );

        // Segment 1
        // Long name: ROM 0
        // Short name: O0
        // Start: 0H
        // End: 1FFFH
        Regex _regexPages = new Regex(
            @"(Segment (?'index'\d*))|(Long name: (?'type'.*?)\s(?'number'.*))|(Short name: (?'shortname'.*))|(Start: (?'startaddr'.*))|(End: (?'endaddr'.*))",
            RegexOptions.Compiled
        );
        
        // Breakpoints: On
        // Enabled 1: PC=000Dh
        // Disabled 2: None
        Regex _regexBreakpoints = new Regex(
            @"(?'state'Enabled|Disabled)\s(?'number'\d+):",
            RegexOptions.Compiled
        );


        [Flags] enum DebugSettings // from https://sourceforge.net/p/zesarux/code/ci/master/tree/remote.c#l766
        {
            ShowAllRegistersAfterStep = 1,
            ShowNext8AfterStep = 2,
            DoNotAddLPrefixToLabels = 4,
            ShowOpcodeBytes = 8,
            RepeatLastCommandWithEnter = 16,
            StepOverInterrupt = 32
        }

        public override ConnectionCaps ConnectionCaps { get; } = new ConnectionCaps()
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

        string _lastGetMemoryPages;
        string _lastGetPagingState;
        List<string> _tempMemoryConfig;
        public override bool ReadMemoryConfiguration( Memory memory )
        {
            var currentPages = SendAndReceiveSingle( "get-memory-pages" );
            var currentPagingState = SendAndReceiveSingle( "get-paging-state" );

            // don't get verbose page list if the simple page lists haven't changed
            if( _lastGetMemoryPages == currentPages && _lastGetPagingState == currentPagingState )
                return false;

            _lastGetMemoryPages = currentPages;
            _lastGetPagingState = currentPagingState;

            memory.ClearConfiguration();
            memory.PagingEnabled = true;

            // now get the verbose page lists
            var lines = SendAndReceive( "get-memory-pages verbose", _tempMemoryConfig );

            int count = 0;
            string indexStr = null;
            string typeStr = null;
            string numberStr = null;
            string shortnameStr = null;
            string startAddrStr = null;
            string endAddrStr = null;

            foreach( var line in lines )
            {
                var match = _regexPages.Match( line );

                if( !match.Success )
                    continue;

                count += UpdateFromRegexGroup( match.Groups, "index",     ref indexStr     );
                count += UpdateFromRegexGroup( match.Groups, "type",      ref typeStr      );
                count += UpdateFromRegexGroup( match.Groups, "number",    ref numberStr    );
                count += UpdateFromRegexGroup( match.Groups, "shortname", ref shortnameStr );
                count += UpdateFromRegexGroup( match.Groups, "startaddr", ref startAddrStr );
                count += UpdateFromRegexGroup( match.Groups, "endaddr",   ref endAddrStr   );

                if( count != 6 )
                    continue;

                if( typeStr == "System" )
                {
                    typeStr = numberStr;
                    numberStr = "0";
                }

                var number = int.Parse( numberStr );
                var startAddr = Convert.Parse( startAddrStr );
                var endAddr = Convert.Parse( endAddrStr );
                var length = (ushort)(endAddr - startAddr + 1);

                var bankID = new BankID( typeStr, number, BankID.PartEnum.All );
                memory.SetAddressBank( startAddr, length, memory.Bank(bankID) );

                indexStr = typeStr = numberStr = shortnameStr = startAddrStr = endAddrStr = null;
                count = 0;
            }

            return true;
        }

        int UpdateFromRegexGroup( GroupCollection group, string groupName, ref string value )
        {
            if( group[groupName].Success && value == null )
            {
                value = group[groupName].Value;
                return 1;
            }

            return 0;
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

            ReadBreakpoints();

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


        void ParseRegisters( Registers pRegisters, string pString )
        {
            var matches = _regexRegisters.Matches(pString);
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

            matches = _regexFlags.Matches(pString);
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
            return SendAndReceive( cmd, results );
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

        bool RemoveBreakpoints( HashSet<int> list )
        {
            foreach( var bp in list )
                SendAndReceiveSingle( "disable-breakpoint " + bp );
            
            list.Clear();

            return true;
        }

        void Setup()
        {
            GetVersion();

            SendAndReceiveSingle( "set-debug-settings " + (int)( DebugSettings.ShowOpcodeBytes | DebugSettings.StepOverInterrupt ) );
            SendAndReceiveSingle( "set-memory-zone -1" );
            SendAndReceiveSingle( "enable-breakpoints", pRaiseErrors: false );

            ReadBreakpoints();
            RemoveBreakpoints( _enabledBreakpoints );

            ReadBreakpoints();
        }

        void GetVersion()
        {
            var version = SendAndReceiveSingle("get-version");
            LogMessage( "Connected (" + version + ")" );
        }

        List<string> _tempReadBreakpoints;
        void ReadBreakpoints()
        {
            LogDebug( "Getting enabled breakpoints ..." );

            var count = 0;
            _tempReadBreakpoints = SendAndReceive( "get-breakpoints", _tempReadBreakpoints );

            foreach( var breakpoint in _tempReadBreakpoints )
            {
                var match = _regexBreakpoints.Match( breakpoint );
                if( match.Success )
                {
                    var num = int.Parse( match.Groups["number"].Value );
                    if( num > count ) count = num;

                    if( match.Groups["state"].Value == "Enabled" )
                    {
                        _enabledBreakpoints.Add( num );
                        LogDebug( "  " + breakpoint );
                    }
                }
            }

            LogDebug( "... done" );

            ConnectionCaps.MaxBreakpoints = count;
        }

        HashSet<int> _enabledBreakpoints = new HashSet<int>();
        void SetSingleBreakpoint( Breakpoint pBreakpoint )
        {
            var addr = pBreakpoint.Bank.PagedAddress + pBreakpoint.Line.Offset;
            SendAndReceiveSingle( string.Format( $"set-breakpoint {pBreakpoint.Index + 1} PC={addr:X4}h" ) );
            SendAndReceiveSingle( "enable-breakpoint " + ( pBreakpoint.Index + 1 ), pRaiseErrors: false );
        }

        Logging.Severity ProtocolLogLevel { get; set; } = Logging.Severity.Verbose;

        protected override string LogPrefix
        {
            get { return "ZEsarUX"; }
        }
    }
}
