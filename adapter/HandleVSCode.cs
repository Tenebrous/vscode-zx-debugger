using System;
using System.Collections.Generic;
using System.IO;
using VSCode;
using ZXDebug.SourceMapper;

namespace ZXDebug
{
    public class HandleVSCode
    {
        public Session Session;

        public HandleVSCode( Session session )
        {
            Session = session;
        }

        bool _linesStartAt1;

        public void Configure()
        {
            // add vscode output for logged events
            Log.OnLog += SendLog;

            
            // vscode events
            Session.VSCode.InitializeEvent        += Initialize;
            Session.VSCode.DisconnectEvent        += Disconnect;
            Session.VSCode.LaunchEvent            += Launch;
            Session.VSCode.AttachEvent            += Attach;
            Session.VSCode.ConfigurationDoneEvent += ConfigurationDone;

            Session.VSCode.PauseEvent             += Pause;
            Session.VSCode.ContinueEvent          += Continue;
            Session.VSCode.StepOverEvent          += StepOver;
            Session.VSCode.StepInEvent            += StepIn;
            Session.VSCode.StepOutEvent           += StepOut;
   
            Session.VSCode.GetThreadsEvent        += GetThreads;
            Session.VSCode.GetStackTraceEvent     += GetStackTrace;
            Session.VSCode.GetScopesEvent         += GetScopes;
   
            Session.VSCode.GetVariablesEvent      += GetVariables;
            Session.VSCode.SetVariableEvent       += SetVariable;
            Session.VSCode.GetCompletionsEvent    += GetCompletions;
            Session.VSCode.EvaluateEvent          += Evaluate;
            Session.VSCode.SetBreakpointsEvent    += SetBreakpoints;


            // handle custom events not part of the standard vscode protocol
            var custom = new CustomRequests( Session.VSCode );
            custom.GetDefinitionEvent    += CustomGetDefinition;
            custom.SetNextStatementEvent += CustomSetNextStatement;
        }


        void Initialize( Request request, VSCode.Capabilities capabilities )
        {
            _linesStartAt1 = (bool)request.arguments.linesStartAt1;

            capabilities.supportsConfigurationDoneRequest = true;
            capabilities.supportsCompletionsRequest = true;
        }

        void Continue( Request request )
        {
            Session.Machine.Continue();
        }

        void Pause( Request request )
        {
            Session.Machine.Pause();
        }

        byte[] _tempMemStepOver = new byte[1];
        void StepOver( Request request )
        {
            Session.VSCode.Send( request );

            if( Session.Device.Meta.CanStepOverSensibly )
            {
                // debugger is well-behaved when it comes to stepping over jr,jp and ret
                Session.Machine.StepOver();
                return;
            }

            // deal with debuggers that don't deal with jr,jp and ret propertly when stepping over

            var b = Session.Machine.Memory.Get( Session.Machine.Registers.PC, _tempMemStepOver, 0, 1 );

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

                    Log.Write( Log.Severity.Debug, "Doing step instead of step-over as current instr=" + _tempMemStepOver[0].ToHex() );
                    Session.Machine.Step();
                    return;

                default:
                    break;
            }

            Session.Machine.StepOver();
        }

        void StepIn( Request request )
        {
            Session.VSCode.Send( request );
            Session.Machine.Step();
        }

        void StepOut( Request request )
        {
            if( Session.Device.Meta.CanStepOut )
            {
                Session.VSCode.Send( request );
                Session.Machine.StepOut();
            }
            else
                Session.VSCode.Send( request, errorMsg: "Step Out is not supported" );
        }

        void Launch( Request request, string json )
        {
            Initialise( json );
            SaveDebug();

            Session.Machine.Start();

            if( Session.Settings.StopOnEntry )
                Session.Machine.Pause();
        }

        void Attach( Request request, string json )
        {
            Initialise( json );
            SaveDebug();

            Session.Machine.Start();

            if( Session.Settings.StopOnEntry )
                Session.Machine.Pause();
        }

        void ConfigurationDone( Request request )
        {
        }

        void GetThreads( Request request )
        {
            Session.VSCode.Send(
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
            Session.Machine.Registers.Get();
            Session.Machine.Memory.GetMapping();

            // disassemble from current PC
            var disassemblyUpdated = Session.Machine.UpdateDisassembly( Session.Machine.Registers.PC );

            var stackBytes = new byte[20];
            var caller = new byte[4];

            _stackAddresses.Clear();
            _stackFrames.Clear();

            // add current PC as an entry to the stack frame
            _stackAddresses.Add( Session.Machine.Registers.PC );

            // get stack pos and limit how many bytes we read if we would go higher than 0xFFFF
            var stackPos = Session.Machine.Registers.SP;
            var maxBytes = Math.Min( 20, 0xFFFF - stackPos );

            // read bytes from SP onwards for analysis of the addresses
            var bytes = Session.Machine.Memory.Get( Session.Machine.Registers.SP, stackBytes, 0, maxBytes );

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
                    addressDetails = Session.Machine.GetAddressDetails( addr );
                    isCode = true;
                }
                else
                {
                    Session.Machine.Memory.Get( (ushort)( addr - 3 ), caller, 0, 3 );

                    if( _callerOpcode3.Contains( caller[0] ) )
                    {
                        addr -= 3;
                        addressDetails = Session.Machine.GetAddressDetails( addr );
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
                    else if( _callerOpcode3.Contains( caller[1] ) )
                    {
                        addr -= 2;
                        addressDetails = Session.Machine.GetAddressDetails( addr );
                        isCode = true;
                    }
                    else if( _callerOpcode1.Contains( caller[2] ) )
                    {
                        addr -= 1;
                        addressDetails = Session.Machine.GetAddressDetails( addr );
                        isCode = true;
                        symbolIcon += " ↖";
                    }

                    _stackAddresses[i] = addr;
                }

                var style = i == 0 ? "subtle" : "normal";

                var text = addressDetails?.Labels?[0].Name ?? addr.ToHex();

                if( addressDetails?.Source != null )
                {
                    // got source 

                    _stackFrames.Add(
                        new StackFrame(
                            stackFrameId,
                            addressDetails.GetRelativeText() + " " + symbolIcon,
                            new Source(
                                null,
                                Path.GetFullPath( Path.Combine( Session.Settings.ProjectFolder, addressDetails.Source.File.Filename ) )
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
                Session.Machine.WriteDisassemblyFile( DisassemblyFile );

            foreach( var frame in _stackFrames )
                if( frame.source == DisassemblySource && frame.line == 0 )
                    frame.line = Session.Machine.GetLineOfAddressInDisassembly( _stackAddresses[frame.id - 1] ) + 1;

            Session.VSCode.Send(
                request,
                new StackTraceResponseBody(
                    _stackFrames
                )
            );
        }

        void GetScopes( Request request, int frameId )
        {
            var addr = _stackAddresses[frameId - 1];
            Session.Machine.UpdateDisassembly( addr, DisassemblyFile );

            var scopes = new List<Scope>();

            foreach( var value in _rootValues.Children )
            {
                scopes.Add(
                    new Scope(
                        value.Name,
                        value.ID
                    )
                );
            }

            Session.VSCode.Send( request, new ScopesResponseBody( scopes ) );

            if( _stackFrames[frameId - 1].source != DisassemblySource )
            {
                var disasmLine = Session.Machine.GetLineOfAddressInDisassembly( addr );
                if( disasmLine > 0 )
                {
                    Session.VSCode.Send(
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
            //Log.Write( Log.Severity.Error, pRequest.arguments.ToString() );
        }

        void Evaluate( Request request, int frameId, string context, string expression, bool wantHex, ref string result )
        {
            switch( context )
            {
                case "repl":
                    result = VSCode_OnEvaluate_REPL( request, expression );
                    break;

                default:
                    result = VSCode_OnEvaluate_Variable( request, expression );
                    break;
            }
        }

        string VSCode_OnEvaluate_REPL( Request request, string expression )
        {
            return string.Join( "\n", Session.Device.CustomCommand( expression ) );
        }

        char[] _varSplitChar = new[] { ' ', ',' };
        byte[] _tempVar = new byte[1024];
        string VSCode_OnEvaluate_Variable( Request request, string expression )
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

                    if( Session.Machine.Registers.IsValidRegister( text ) )
                    {
                        address = Session.Machine.Registers[text];
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

                            length = Session.Machine.Registers.Size( text );
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
                Session.Machine.Memory.Get( address, _tempVar, 0, length );
            }

            result = Convert.ToHex( _tempVar, length );

            if( isPointer && isRegister )
                result = $"({address.ToHex()}) {result}";

            return result;
        }

        void GetVariables( Request request, int reference, List<Variable> results )
        {
            var value = _rootValues.All( reference );

            if( value == null )
                return;

            value.Refresh();

            foreach( var child in value.Children )
                results.Add( CreateVariableForValue( child ) );
        }


        void SetVariable( Request request, Variable variable )
        {
            var value = _rootValues.AllByName( variable.name );
            value.Setter?.Invoke( value, variable.value );
        }


        void Disconnect( Request request )
        {
            Session.Machine.Stop();
            Session.VSCode.Stop();
            _running = false;
        }


        HashSet<Spectrum.Breakpoint> _tempBreakpoints = new HashSet<Spectrum.Breakpoint>();
        List<VSCode.Breakpoint> _tempBreakpointsResponse = new List<VSCode.Breakpoint>();
        void SetBreakpoints( Request request )
        {
            string sourceName = request.arguments.source.name;

            if( sourceName != DisassemblySource.name )
                return;

            var max = Session.Device.Meta.MaxBreakpoints;

            _tempBreakpointsResponse.Clear();

            // record old ones
            _tempBreakpoints.Clear();
            foreach( var b in Session.Machine.Breakpoints )
                _tempBreakpoints.Add( b );

            // set new ones
            foreach( var breakpoint in request.arguments.breakpoints )
            {
                string error = null;
                int lineNumber = breakpoint.line;
                Spectrum.Breakpoint bp = null;

                var line = Session.Machine.GetLineFromDisassemblyFile( LineFromVSCode( lineNumber ) );

                if( line != null )
                {
                    bp = Session.Machine.Breakpoints.Add( line );
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
                            $"{bp.Line.Bank.ID}+{bp.Line.Offset.ToHex()} ({( (ushort)( bp.Bank.LastAddress + bp.Line.Offset ) ).ToHex()})",
                            DisassemblySource,
                            LineToVSCode( bp.Line.FileLine ),
                            0,
                            LineToVSCode( bp.Line.FileLine ),
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
                            LineToVSCode( lineNumber ),
                            0,
                            LineToVSCode( lineNumber ),
                            0
                        )
                    );
            }

            // remove those no longer set
            foreach( var b in _tempBreakpoints )
                Session.Machine.Breakpoints.Remove( b );


            // respond to vscode 

            Session.Machine.Breakpoints.Commit();

            Session.VSCode.Send(
                request,
                new SetBreakpointsResponseBody( _tempBreakpointsResponse )
            );
        }


        void CustomGetDefinition( Request request, string file, int line, string text )
        {
            // note: line numbers are always 0-based and ignore _linesStartAt1

            Log.Write( Log.Severity.Message, "GetDef: " + file + ", " + line + ", [" + text + "]" );

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

        void CustomSetNextStatement( Request request, string file, int line )
        {
            // note: line numbers are always 0-based and ignore _linesStartAt1

            var disasmLine = Session.Machine.GetLineFromDisassemblyFile( line );

            if( disasmLine == null )
            {
                Session.VSCode.Send( request, errorMsg: "Invalid line" );
                return;
            }

            var memBank = Session.Machine.Memory.Bank( disasmLine.Bank.ID );
            if( !memBank.IsPagedIn )
                throw new Exception( "Cannot set PC to that address as it isn't currently paged in." );

            Session.Machine.Registers.Set( "PC", (ushort)( memBank.LastAddress + disasmLine.Offset ) );

            Session.VSCode.Send( request );

            Session.VSCode.Refresh();
        }

        public void SendLog( Log.Severity severity, string msg )
        {
            var type = severity == Log.Severity.Error ? OutputEvent.OutputEventType.stderr : OutputEvent.OutputEventType.stdout;
            Session.VSCode?.Send( new OutputEvent( type, msg + "\n" ) );
        }

        int LineFromVSCode( int line )
        {
            return _linesStartAt1 ? line - 1 : line;
        }
        int LineToVSCode( int line )
        {
            return _linesStartAt1 ? line : line + 1;
        }
    }
}
