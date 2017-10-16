using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VSCode;
using ZXDebug.SourceMapper;
using StackFrame = VSCode.StackFrame;

namespace ZXDebug
{
    public class HandleVSCode : Loggable
    {
        Session _session;

        public HandleVSCode( Session session )
        {
            _session = session;
        }

        int _vscLineBase;
        int _internalLineBase;
        int _sourcemapLineBase;

        public void Configure()
        {
            // add vscode output for logged events
            Logging.OnLog += SendLog;

            
            // vscode events
            _session.VSCode.InitializeEvent        += Initialize;
            _session.VSCode.DisconnectEvent        += Disconnect;
            _session.VSCode.LaunchEvent            += Launch;
            _session.VSCode.AttachEvent            += Attach;
            _session.VSCode.ConfigurationDoneEvent += ConfigurationDone;

            _session.VSCode.PauseEvent             += Pause;
            _session.VSCode.ContinueEvent          += Continue;
            _session.VSCode.StepOverEvent          += StepOver;
            _session.VSCode.StepInEvent            += StepIn;
            _session.VSCode.StepOutEvent           += StepOut;
   
            _session.VSCode.GetThreadsEvent        += GetThreads;
            _session.VSCode.GetStackTraceEvent     += GetStackTrace;
            _session.VSCode.GetScopesEvent         += GetScopes;
   
            _session.VSCode.GetVariablesEvent      += GetVariables;
            _session.VSCode.SetVariableEvent       += SetVariable;
            _session.VSCode.GetCompletionsEvent    += GetCompletions;
            _session.VSCode.EvaluateEvent          += Evaluate;
            _session.VSCode.SetBreakpointsEvent    += SetBreakpoints;


            // handle custom events not part of the standard vscode protocol
            var custom = new CustomRequests( _session.VSCode );
            custom.GetDefinitionEvent    += Custom_GetDefinition;
            custom.GetHoverEvent         += Custom_GetHoverEvent;
            custom.SetNextStatementEvent += Custom_SetNextStatement;
            custom.WatchMemoryEvent      += Custom_WatchMemory;
        }

        void Initialize( Request request, VSCode.Capabilities caps )
        {
            _vscLineBase = (bool)request.arguments.linesStartAt1 ? 1 : 0;
            _internalLineBase = 0;
            _sourcemapLineBase = 1;

            caps.supportsConfigurationDoneRequest = true;
            caps.supportsCompletionsRequest = true;
            caps.supportsEvaluateForHovers = true;
        }

        void Continue( Request request )
        {
            _session.Machine.Continue();
        }

        void Pause( Request request )
        {
            _session.Machine.Pause();
        }

        byte[] _tempMemStepOver = new byte[1];
        void StepOver( Request request )
        {
            _session.VSCode.Send( request );

            if( _session.Connection.ConnectionCaps.CanStepOverSelectively )
            {
                // debugger is well-behaved when it comes to stepping over jr,jp and ret
                _session.Machine.StepOver();
                return;
            }

            // deal with debuggers that don't deal with jr,jp and ret propertly when stepping over

            var b = _session.Machine.Memory.Read( _session.Machine.Registers.PC, _tempMemStepOver, 0, 1 );

            switch( _tempMemStepOver[0] )
            {
                case 0x18: // JR
                //case 0x20: // JR NZ
                //case 0x28: // JR Z
                //case 0x30: // JR NC
                //case 0x38: // JR C

                //case 0xC2: // JP NZ
                case 0xC3: // JP
                //case 0xCA: // JP Z
                //case 0xD2: // JP NC
                //case 0xDA: // JP C
                //case 0xE2: // JP PO
                //case 0xE9: // JP (HL)
                //case 0xEA: // JP PE
                //case 0xF2: // JP P
                //case 0xFA: // JP M

                case 0xC0: // RET NZ
                case 0xC8: // RET Z
                case 0xC9: // RET
                case 0xD0: // RET NC
                case 0xD8: // RET C
                case 0xE0: // RET PO
                case 0xE8: // RET PE
                case 0xF0: // RET P
                case 0xF8: // RET M

                    LogDebug( "Doing step instead of step-over as current instr=" + _tempMemStepOver[0].ToHex() );
                    _session.Machine.Step();
                    return;

                default:
                    break;
            }

            _session.Machine.StepOver();
        }

        void StepIn( Request request )
        {
            _session.VSCode.Send( request );
            _session.Machine.Step();
        }

        void StepOut( Request request )
        {
            if( _session.Connection.ConnectionCaps.CanStepOut )
            {
                _session.VSCode.Send( request );
                _session.Machine.StepOut();
            }
            else
                _session.VSCode.Send( request, errorMsg: "Step Out is not supported" );
        }

        void Launch( Request request, string json )
        {
            UpdateSettings( json );

            _session.Machine.Start();

            if( _session.Settings.StopOnEntry )
                _session.Machine.Pause();
        }

        void Attach( Request request, string json )
        {
            UpdateSettings( json );

            _session.Machine.Start();

            if( _session.Settings.StopOnEntry )
                _session.Machine.Pause();
        }

        void UpdateSettings( string json )
        {
            _session.Settings.SetLayer( Settings.LayerEnum.LaunchAttach, json );
            _session.Settings.Resolve( _session.Machine.Caps );
        }

        void ConfigurationDone( Request request )
        {
        }

        void GetThreads( Request request )
        {
            _session.VSCode.Send(
                request,
                new ThreadsResponseBody(
                    new List<Thread>()
                    {
                        new Thread( 1, "Main" )
                    }
                )
            );
        }


        HashSet<byte> _callerOpcode3 = new HashSet<byte>()
        { 0xC4, 0xCC, 0xCD, 0xD4, 0xDC, 0xE4, 0xEC, 0xF4, 0xFC };

        HashSet<byte> _callerOpcode2 = new HashSet<byte>()
        {  };

        HashSet<byte> _callerOpcode1 = new HashSet<byte>()
        { 0xC7, 0xCF, 0xD7, 0xDF, 0xE7, 0xEF, 0xF7, 0xFF };

        List<ushort> _stackAddresses = new List<ushort>();
        List<StackFrame> _stackFrames = new List<StackFrame>();
        void GetStackTrace( Request request )
        {
            _session.Machine.Registers.Read();

            if( _session.Machine.Memory.ReadConfiguration() )
                _session.Machine.Memory.Log();

            // disassemble from current PC
            var disassemblyUpdated = _session.Machine.UpdateDisassembly( _session.Machine.Registers.PC );

            var originBytes = new byte[4];

            _stackAddresses.Clear();
            _stackFrames.Clear();

            // add current PC as an entry to the stack frame
            _stackAddresses.Add( _session.Machine.Registers.PC );

            var stackPos = _session.Machine.Registers.SP;

            // read bytes from SP onwards for analysis of the addresses
            var stackBytes = new byte[20];
            var maxBytes = Math.Min( stackBytes.Length, 0xFFFF - stackPos );
            var bytes = _session.Machine.Memory.Read( _session.Machine.Registers.SP, stackBytes, 0, maxBytes );

            // turn the bytes into ushorts
            for( var i = 0; i < bytes; i += 2 )
                _stackAddresses.Add( (ushort)( ( stackBytes[i + 1] << 8 ) | stackBytes[i] ) );

            // now check out each address
            for( var i = 0; i < _stackAddresses.Count; i++ )
            {
                // note: entry at i=0 is PC, so we don't need to get mem and we always show it

                var stackFrameId = i + 1;
                var addr = _stackAddresses[i];

                AddressDetails addressDetails = null;
                bool isCode = false;

                var symbolIcon = "";

                if( i == 0 )
                {
                    // always try to get symbol for PC
                    addressDetails = _session.Machine.GetAddressDetails( addr );
                    isCode = true;
                }
                else
                {
                    _session.Machine.Memory.Read( (ushort)( addr - 3 ), originBytes, 0, 3 );

                    if( _callerOpcode3.Contains( originBytes[0] ) )
                    {
                        addr -= 3;
                        addressDetails = _session.Machine.GetAddressDetails( addr );
                        isCode = true;
                        symbolIcon = " ↑";

                        // // we can get the original destination for the call here:
                        // var callDest = (ushort) ( caller[2] << 8 | caller[1] );
                        // var callDestSymbol = GetPreviousSymbol( callDest, ref disassemblyUpdated );
                        // if( callDestSymbol != null )
                        // {
                        //     if( _stackFrames.Count > 0 )
                        //         _stackFrames[_stackFrames.Count - 1].name = callDestSymbol + " -> " + _stackFrames[_stackFrames.Count - 1].name;
                        // }
                    }
                    else if( _callerOpcode3.Contains( originBytes[1] ) )
                    {
                        addr -= 2;
                        addressDetails = _session.Machine.GetAddressDetails( addr );
                        isCode = true;
                    }
                    else if( _callerOpcode1.Contains( originBytes[2] ) )
                    {
                        addr -= 1;
                        addressDetails = _session.Machine.GetAddressDetails( addr );
                        isCode = true;
                        symbolIcon += " ↖";
                    }

                    _stackAddresses[i] = addr;
                }

                if( addressDetails?.Labels != null && addressDetails.LabelledAddress != addressDetails.Address )
                    disassemblyUpdated |= _session.Machine.UpdateDisassembly( addressDetails.LabelledAddress );

                var style = i == 0 ? "subtle" : "normal";

                var text = addressDetails?.Labels?[0].Name ?? addr.ToHex();

                // no stepping through source at the moment
                if( addressDetails?.Source != null && 1 == 0 )
                {
                    // got source 
                    
                    _stackFrames.Add(
                        new StackFrame(
                            stackFrameId,
                            addressDetails.GetRelativeText() + " " + symbolIcon,
                            new Source(
                                null,
                                Path.GetFullPath( Path.Combine( _session.Settings.ProjectFolder, addressDetails.Source.File.Filename ) )
                            ),
                            addressDetails.Source.Line,
                            0,
                            style
                        )
                    );
                }
                else if( addressDetails != null )
                {
                    // no source, but probably labels

                    _stackFrames.Add(
                        new StackFrame(
                            stackFrameId,
                            addressDetails.GetRelativeText() + " " + symbolIcon,
                            DisassemblySource,
                            0,
                            0,
                            style
                        )
                    );
                }
                else if( isCode )
                {
                    // no labels, but it's code

                    _stackFrames.Add(
                        new StackFrame(
                            stackFrameId,
                            text + " " + symbolIcon,
                            DisassemblySource,
                            0,
                            0,
                            style
                        )
                    );
                }
                else
                {
                    // not code, just a raw value

                    _stackFrames.Add(
                        new StackFrame(
                            stackFrameId,
                            addr.ToHex(),
                            StackSource,
                            0,
                            0,
                            style
                        )
                    );
                }
            }

            if( disassemblyUpdated )
                _session.Machine.WriteDisassemblyFile( DisassemblyFile );

            foreach( var frame in _stackFrames )
                if( frame.source == DisassemblySource && frame.line == 0 )
                    frame.line = _session.Machine.GetLineOfAddressInDisassembly( _stackAddresses[frame.id - 1] ) + 1;

            _session.VSCode.Send(
                request,
                new StackTraceResponseBody(
                    _stackFrames
                )
            );

            foreach( var m in _memWatches )
                Check_MemoryWatch( m.Value );
        }

        void GetScopes( Request request, int frameId )
        {
            var addr = _stackAddresses[frameId - 1];
            _session.Machine.UpdateDisassembly( addr, DisassemblyFile );

            var scopes = new List<Scope>();

            foreach( var value in _session.Values.Children )
            {
                scopes.Add(
                    new Scope(
                        value.Name,
                        value.ID
                    )
                );
            }

            _session.VSCode.Send( request, new ScopesResponseBody( scopes ) );

            if( _stackFrames[frameId - 1].source != DisassemblySource )
            {
                var disasmLine = _session.Machine.GetLineOfAddressInDisassembly( addr );
                if( disasmLine > 0 )
                {
                    _session.VSCode.Send(
                        new Event(
                            "setDisassemblyLine",
                            new { line = disasmLine }
                        )
                    );
                }
            }
        }

        void GetCompletions( Request request, int frameId, int line, int column, string text )
        {
            //Log.Write( Logging.Severity.Error, pRequest.arguments.ToString() );
        }

        void Evaluate( Request request, int frameId, string context, string expression, bool wantHex, ref string result )
        {
            switch( context )
            {
                case "repl":
                    result = Evaluate_REPL( request, expression );
                    break;

                case "hover":
                    result = Evaluate_Hover( request, expression );
                    break;

                default:
                    result = Evaluate_Variable( request, expression );
                    break;
            }
        }

        string Evaluate_REPL( Request request, string expression )
        {
            return string.Join( "\n", _session.Connection.CustomCommand( expression ) );
        }

        string Evaluate_Hover( Request request, string expression )
        {
            var s = new StringBuilder();
            s.Append( expression );

            var reg = _session.Machine.Registers;

            ushort value = 0;
            var isAddress = false;

            if( reg.IsValidRegister( expression ) )
            {
                if( reg.SizeOf( expression ) == 1 )
                {
                    value = _session.Machine.Registers[expression];

                    s.Append( " = " );
                    s.Append( ((byte)value).ToHex() );
                }
                else
                {
                    value = _session.Machine.Registers[expression];
                    isAddress = true;

                    s.Append( " = " );
                    s.Append( value.ToHex() );
                }
            }
            else
            {
                var sym = _session.Machine.SourceMaps.GetLabel(expression);

                if( sym != null )
                {
                    value = sym.Address;

                    s.Append( " = address " );
                    s.Append( sym.BankID );
                    s.Append( ' ' );
                    s.Append( value.ToHex() );

                    if( !string.IsNullOrWhiteSpace( sym.Comment ) )
                    {
                        s.Append( '\n' );
                        s.Append( '"' );
                        s.Append( sym.Comment );
                        s.Append( '"' );
                    }

                    s.Append( '\n' );
                    s.Append( "  from " );
                    s.Append( Path.GetFileName( sym.Map.Filename ) ); 

                    isAddress = true;
                }
            }

            if( isAddress )
            {
                s.Append( '\n' );
                s.Append( '\n' );

                var bytes = new byte[8];
                _session.Machine.Memory.Read( value, bytes, 0, bytes.Length );

                for( int i = 0; i < bytes.Length; i++ )
                {
                    s.Append( ( (ushort)( value + i ) ).ToHex() );
                    s.Append( ' ' );
                    s.Append( bytes[i].ToHex() );
                    s.Append( ' ' );
                    s.Append( bytes[i].ToBin() );
                    s.Append( '\n' );
                }
            }

            return s.ToString();
        }

        char[] _varSplitChar = new[] { ' ', ',' };
        byte[] _tempVar = new byte[1024];
        string Evaluate_Variable( Request request, string expression )
        {
            var result = "n/a";

            var parts = expression.Split( _varSplitChar, StringSplitOptions.RemoveEmptyEntries );

            var gotAddress = false;
            var gotLength = false;
            var gotData = false;
            var isPointer = false;
            var isRegister = false;
            ushort address = 0;
            var parsedLength = 0;
            var length = 0;

            foreach( string part in parts )
            {
                var text = part;

                if( !gotAddress )
                {
                    if( text.StartsWith( "(" ) && text.EndsWith( ")" ) )
                    {
                        isPointer = true;
                        text = text.Substring( 1, text.Length - 2 ).Trim();
                    }

                    if( _session.Machine.Registers.IsValidRegister( text ) )
                    {
                        address = _session.Machine.Registers[text];
                        length = 2;
                        gotLength = true;
                        isRegister = true;

                        if( !isPointer )
                        {
                            _tempVar[0] = (byte)( address & 0xFF );

                            if( length == 2 )
                            {
                                _tempVar[1] = _tempVar[0];
                                _tempVar[0] = (byte)( address >> 8 );
                            }

                            length = _session.Machine.Registers.SizeOf( text );
                            gotLength = true;
                            gotData = true;
                        }
                    }
                    else
                    {
                        address = Convert.Parse( text );
                        length = 1;
                        gotLength = true;
                    }

                    gotAddress = true;

                    continue;
                }

                if( gotAddress && int.TryParse( text, out parsedLength ) )
                {
                    length = Math.Max( 0, Math.Min( parsedLength, _tempVar.Length ) );
                    gotLength = true;

                    continue;
                }
            }

            if( gotAddress && gotLength && !gotData )
            {
                _session.Machine.Memory.Read( address, _tempVar, 0, length );
            }

            result = Convert.ToHex( _tempVar, length );

            if( isPointer && isRegister )
                result = $"({address.ToHex()}) {result}";

            return result;
        }

        Variable CreateVariableForValue( ValueTree value )
        {
            value.Refresh();

            return new Variable(
                value.Name,
                value.Formatted,
                "value",
                value.Children.Count == 0 ? -1 : value.ID,
                new VariablePresentationHint( "data" )
            );
        }

        void GetVariables( Request request, int reference, List<Variable> results )
        {
            var value = _session.Values.All( reference );

            if( value == null )
                return;

            value.Refresh();

            foreach( var child in value.Children )
                results.Add( CreateVariableForValue( child ) );
        }


        void SetVariable( Request request, Variable variable )
        {
            var value = _session.Values.AllByName( variable.name );
            value.Setter?.Invoke( value, variable.value );
        }


        void Disconnect( Request request )
        {
            _session.Machine.Stop();
            _session.VSCode.Stop();
            _session.Running = false;
        }


        HashSet<Spectrum.Breakpoint> _tempBreakpoints = new HashSet<Spectrum.Breakpoint>();
        List<VSCode.Breakpoint> _tempBreakpointsResponse = new List<VSCode.Breakpoint>();
        void SetBreakpoints( Request request )
        {
            string sourceName = request.arguments.source.name;

            if( sourceName != DisassemblySource.name )
                return;

            var max = _session.Connection.ConnectionCaps.MaxBreakpoints;

            _tempBreakpointsResponse.Clear();

            // record old ones
            _tempBreakpoints.Clear();
            foreach( var b in _session.Machine.Breakpoints )
                _tempBreakpoints.Add( b );

            // set new ones
            foreach( var breakpoint in request.arguments.breakpoints )
            {
                string error = null;

                int vscLineNumber = breakpoint.line;
                int internalLineNumber = RebaseLineNumber( vscLineNumber, _vscLineBase, _internalLineBase );

                Spectrum.Breakpoint bp = null;

                var line = _session.Machine.GetLineFromDisassemblyFile( internalLineNumber );

                if( line != null )
                {
                    bp = _session.Machine.Breakpoints.Add( line );
                    _tempBreakpoints.Remove( bp );
                }

                if( bp == null )
                    error = "Invalid location";
                else if( bp.Index < 0 || bp.Index >= max )
                    error = "A maximum of " + max + " breakpoints are supported";

                if( error == null )
                    _tempBreakpointsResponse.Add(
                        new VSCode.Breakpoint(
                            bp.Index,
                            true,
                            $"{bp.Line.Bank.ID}+{bp.Line.Offset.ToHex()} ({( (ushort)( bp.Bank.PagedAddress + bp.Line.Offset ) ).ToHex()})",
                            DisassemblySource,
                            vscLineNumber,
                            0,
                            vscLineNumber,
                            0
                        )
                    );
                else
                    _tempBreakpointsResponse.Add(
                        new VSCode.Breakpoint(
                            -1,
                            false,
                            error,
                            DisassemblySource,
                            vscLineNumber,
                            0,
                            vscLineNumber,
                            0
                        )
                    );
            }

            // remove those no longer set
            foreach( var b in _tempBreakpoints )
                _session.Machine.Breakpoints.Remove( b );


            // respond to vscode 

            _session.Machine.Breakpoints.Commit();

            _session.VSCode.Send(
                request,
                new SetBreakpointsResponseBody( _tempBreakpointsResponse )
            );
        }


        void Custom_GetDefinition( Request request, string file, int line, int column, string text, string symbol )
        {
            // note: line numbers are always 0-based and ignore _linesStartAt1

            Log( 
                Logging.Severity.Message, 
                $"GetDef: {file}:{line}:{column} [{symbol}] [{line}]"
            );

            // disasm file example:
            // 11      C00D C207C0   jp nz, s1_inner_loop          ; $C007


            //foreach( var m in _machine.SourceMapper )
            //{
            //    if( !m.Files.TryGetValue( pFile, out var file ) )
            //        continue;

            //    //if( !file.Lines.TryGetValue( pLine + 1, out var lineList ) )
            //    if( !file.Lines.TryGetValue( pLine + 1, out var addr ) )
            //        continue;

            //    int lowLine = int.MaxValue;
            //    int highLine = 0;

            //    // foreach( var addr in lineList )
            //    //{
            //        if( addr.Location == 0 )
            //            continue;

            //        var disasmLine = _machine.GetLineOfAddressInDisassembly( addr.BankID, addr.Location );

            //        if( disasmLine == 0 )
            //        {
            //            _machine.UpdateDisassembly( addr.Location, DisassemblyFile );
            //            disasmLine = _machine.GetLineOfAddressInDisassembly( addr.BankID, addr.Location );
            //        }

            //        if( disasmLine == 0 )
            //            continue;

            //        if( disasmLine < lowLine )
            //            lowLine = disasmLine;

            //        if( disasmLine > highLine )
            //            highLine = |;
            //    //}

            //    if( lowLine > highLine )
            //        return;

            //    _vscode.Send( 
            //        pRequest,
            //        new GetDisassemblyForSourceResponseBody( DisassemblyFile, lowLine-1, highLine-1 )
            //    );

            //    break;
            //}
        }

        void Custom_GetHoverEvent( Request request, string file, int line, int column, string text, string symbol )
        {
            var sym = text.Substring( column - 1, text.Length + 2 );

            Log(
                Logging.Severity.Message,
                $"GetHover: {file}:{line}:{column} [{symbol}] [{sym}] [{line}]"
            );

            _session.VSCode.Send( 
                request,
                new HoverResponseBody( "from .cs")
            );
        }


        void Custom_SetNextStatement( Request request, string file, int line )
        {
            // note: line numbers are always 0-based and ignore _linesStartAt1

            var disasmLine = _session.Machine.GetLineFromDisassemblyFile( line );

            if( disasmLine == null )
            {
                _session.VSCode.Send( request, errorMsg: "Invalid line" );
                return;
            }

            var memBank = _session.Machine.Memory.Bank( disasmLine.Bank.ID );
            if( !memBank.IsPagedIn )
                throw new Exception( "Cannot set PC to that address as it isn't currently paged in." );

            _session.Machine.Registers.Set( "PC", (ushort)( memBank.PagedAddress + disasmLine.Offset ) );

            _session.VSCode.Send( request );

            _session.VSCode.Refresh();
        }

        class MemWatch
        {
            public string ID;
            public ushort Address;
            public int Length;
            public byte[] Data;
        }

        Dictionary<string, MemWatch> _memWatches = new Dictionary<string, MemWatch>();
        void Custom_WatchMemory( Request request, string id, string address, string length )
        {
            _session.VSCode.Ack( request );

            var watch = new MemWatch()
            {
                ID = id,
                Address = Convert.Parse( address ),
                Length = Convert.Parse( length )
            };

            _memWatches[id] = watch;
            Check_MemoryWatch( watch );
        }

        void Check_MemoryWatch( MemWatch watch )
        {
            var bytes = new byte[watch.Length];
            _session.Machine.Memory.Read( watch.Address, bytes );

            bool changed = false;

            if( watch.Data == null )
            {
                changed = true;
            }
            else
            {
                for( int i = 0; i < bytes.Length; i++ )
                {
                    if( watch.Data[i] != bytes[i] )
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if( !changed )
                return;

            var s = new StringBuilder();

            if( watch.Data != null )
            {
                s.Append( watch.Address.ToHex() );
                s.Append( ": " );

                bool inChange = false;

                for( int i = 0; i < bytes.Length; i++ )
                {
                    var prechanged = i > 0 && bytes[i - 1] != watch.Data[i - 1];
                    var thischanged = bytes[i] != watch.Data[i];
                    var postchanged = i < bytes.Length - 1 && bytes[i + 1] != watch.Data[i + 1];

                    if( !prechanged && thischanged )
                        s.Append( "<" );
                    else if( !prechanged && !thischanged )
                        s.Append( " " );

                    s.Append( $"{bytes[i]:X2}" );

                    if( thischanged && postchanged )
                        s.Append( "-" );
                    else if( thischanged && !postchanged )
                        s.Append( ">" );
                }

                s.Append( "\n" );
            }
            else
            {
                s.Append( watch.Address.ToHex() );
                s.Append( ":  " );
                s.Append( Convert.ToHex( bytes, " " ) );
                s.Append( "\n" );
            }

            watch.Data = bytes;

            _session.VSCode.Send(
                new Event( "memoryWatchUpdated", 
                new
                {
                    id = watch.ID,
                    data = s.ToString()
                } )
            );
        }

        public void SendLog( Logging.Severity severity, string msg )
        {
            var type = severity == Logging.Severity.Error ? OutputEvent.OutputEventType.stderr : OutputEvent.OutputEventType.stdout;
            _session.VSCode?.Send( new OutputEvent( type, msg + "\n" ) );
        }

        int RebaseLineNumber( int line, int fromStart, int toStart )
        {
            return line - fromStart + toStart;
        }

        string _disassemblyFile;
        string DisassemblyFile
        {
            get { return _disassemblyFile = _disassemblyFile ?? Path.Combine( _session.Settings.TempFolder, "disasm.zdis" ); }
        }


        Source _stackSource;
        Source StackSource
        {
            get { return _stackSource = _stackSource ?? new Source( "#", "", 0, Source.SourcePresentationHintEnum.deemphasize ); }
        }


        Source _disassemblySource;
        Source DisassemblySource
        {
            get { return _disassemblySource = _disassemblySource ?? new Source( " ", DisassemblyFile, 0, Source.SourcePresentationHintEnum.normal ); }
        }
    }
}
