using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Formatters;
using System.Text;
using Spectrum;
using VSCode;
using VSCodeBreakpoint = VSCode.Breakpoint;
using File = System.IO.File;
using ZXDebug.SourceMap;

namespace ZXDebug
{
    public static class Adapter
    {
        static Connection _vscode;
	    static Debugger _debugger;
	    static bool _running;

	    static Value _rootValues = new Value();
        static Value _registersValues;
        static Value _pagingValues;
	    static Value _settingsValues;

	    static Machine _machine;

	    static Settings _settings;


    	static void Main(string[] argv)
	    {
            // set up 


            // wire the logging stuff up to VSCode's console output
            Log.OnLog += Log_SendToVSCode;
	        Log.MaxSeverityConsole = Log.Severity.Message;
            Log.MaxSeverityLog = Log.Severity.Debug;


            // settings
	        _settings = new Settings();
            _settings.DeserializingEvent += Settings_OnDeserializing;
            _settings.DeserializedEvent  += Settings_OnDeserialized;


            // vscode events

            _vscode = new Connection();
            _vscode.InitializeEvent += VSCode_OnInitialize;
	        _vscode.DisconnectEvent += VSCode_OnDisconnect;
	        _vscode.LaunchEvent += VSCode_OnLaunch;
	        _vscode.AttachEvent += VSCode_OnAttach;
	        _vscode.ConfigurationDoneEvent += VSCode_OnConfigurationDone;

	        _vscode.PauseEvent += VSCode_OnPause;
	        _vscode.ContinueEvent += VSCode_OnContinue;
	        _vscode.StepOverEvent += VSCode_OnStepOver;
	        _vscode.StepInEvent += VSCode_OnStepIn;
	        _vscode.StepOutEvent += VSCode_OnStepOut;

	        _vscode.GetThreadsEvent += VSCode_OnGetThreads;
	        _vscode.GetStackTraceEvent += VSCode_OnGetStackTrace;
	        _vscode.GetScopesEvent += VSCode_OnGetScopes;

	        _vscode.GetVariablesEvent += VSCode_OnGetVariables;
	        _vscode.SetVariableEvent += VSCode_OnSetVariable;
			_vscode.GetCompletionsEvent += VSCode_OnGetCompletions;
	        _vscode.EvaluateEvent += VSCode_OnEvaluate;
            _vscode.SetBreakpointsEvent += VSCode_OnSetBreakpoints;


            // debugger events

            _debugger = new ZEsarUX.Connection();	  
			// _debugger.OnData += Z_OnData; 


			// machine events

            _machine = new Machine( _debugger );
			_machine.OnPause += Machine_OnPause;
			_machine.OnContinue += Machine_OnContinue;


            // tie all the values together
            SetupValues( _rootValues, _machine );


			// event loop

	        _running = true;


            // testing thing
            var f = new FileSystemWatcher(@"D:\Dev\ZX\test1", "*.png");
            f.EnableRaisingEvents = true;
            f.Changed += Files_Changed;


	        // event loop
	        while( _running )
	        {
	            var vsactive = _vscode.Process();
				var dbgactive = _debugger.Process();

                if( !vsactive )
                    System.Threading.Thread.Sleep( 10 );

                if( _files.Count > 0 )
                    DoFiles();
	        }
        }


	    static ConcurrentBag<string> _files = new ConcurrentBag<string>();

        static void Files_Changed( object pSender, FileSystemEventArgs pFileSystemEventArgs )
        {
            _files.Add( pFileSystemEventArgs.FullPath );
        }

        static void DoFiles()
        {
            var list = _debugger.CustomCommand( "tbblue-get-palette 0 256" );
            if( list == null || list.Count == 0 )
                return;

            var pal = list[0];

            var palStr = pal.Split( new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );
            var cols = new Color[palStr.Length];

            for( int i = 0; i < cols.Length; i++ )
            {
                var rgb = (byte)Format.Parse( palStr[i], pKnownHex:true );
                cols[i] = Color.FromArgb( 
                    (rgb & 0xE0 ),
                    (rgb & 0x1C) << 3, 
                    (rgb & 0x03) << 6
                );
            }
            
            var s = new StringBuilder();

            while( _files.TryTake( out var file ) )
            {
                s.Clear();

                using( var img = new Bitmap( file ) )
                {
                    for( int y = 0; y < 16; y++ )
                    {
                        for( int x = 0; x < 16; x++ )
                        {
                            s.Append( ' ' );

                            var pix = img.GetPixel( x, y );

                            if( pix.A == 0 )
                            {
                                s.Append( "E3h" );
                                continue;
                            }

                            var closest = 0;
                            var closestDist = double.MaxValue;
                            
                            for( int i = 0; i < cols.Length; i++ )
                            {
                                //var dhue = cols[i].GetHue() - pix.GetHue();
                                //var dsat = cols[i].GetSaturation() - pix.GetSaturation();
                                //var dbri = cols[i].GetBrightness() - pix.GetBrightness();
                                //var dist = Math.Sqrt( dhue * dhue + dsat * dsat + dbri * dbri );

                                var dr = cols[i].R - pix.R;
                                var dg = cols[i].G - pix.G;
                                var db = cols[i].B - pix.B;
                                var dist = Math.Sqrt( dr * dr + dg * dg + db * db );

                                if( dist < closestDist )
                                {
                                    closest = i;
                                    closestDist = dist;
	                            }
	                        }

                            s.Append( string.Format( "{0:X2}h", closest ) );
                        }
                    }
                }

                for( int i = 0; i < 64; i++ )
                {
                    var result = _debugger.CustomCommand( "tbblue-set-pattern " + i + s.ToString() );
                }
            }
        }

	    static void Settings_OnDeserializing( VSCode.Settings pSettings )
	    {
            // make sure all the things that use settings are wired up
	        _settings.Disassembler = _machine.Disassembler.Settings;
	    }

	    static void Settings_OnDeserialized( VSCode.Settings pSettings )
	    {
            Format.HexPrefix = _settings.HexPrefix;
	        Format.HexSuffix = _settings.HexSuffix;
        }


        /////////////////
        // machine events

        static void Machine_OnPause()
		{
			_vscode.Stopped( 1, "step", "step" );
		}

		static void Machine_OnContinue()
		{
			_vscode.Continued( true );
		}



        /////////////////
        // vscode events

        static void VSCode_OnInitialize( Request pRequest, VSCode.Capabilities pCapabilities )
	    {
			pCapabilities.supportsConfigurationDoneRequest = true;
			pCapabilities.supportsCompletionsRequest = true;
	    }

         
	    static void VSCode_OnContinue( Request pRequest )
	    {
	        _machine.Continue();
	    }

	    static void VSCode_OnPause( Request pRequest )
	    {
	        _machine.Pause();
	    }

        static byte[] _tempMemStepOver = new byte[1];
        static void VSCode_OnStepOver( Request pRequest )
	    {
            _vscode.Send( pRequest );

	        if( _debugger.Meta.CanStepOverSensibly )
	        {
                // debugger is well-behaved when it comes to stepping over jr,jp and ret
	            _machine.StepOver();
	            return;
	        }

	        // deal with debuggers that don't deal with jr,jp and ret propertly when stepping over

	        var b = _machine.Memory.Get( _machine.Registers.PC, 1, _tempMemStepOver );

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
                    _machine.Step();
                    return;

                default:
                    break;
            }

            _machine.StepOver();
	    }

        static void VSCode_OnStepIn( Request pRequest )
	    {
	        _vscode.Send( pRequest );
	        _machine.Step();
	    }

        static void VSCode_OnStepOut( Request pRequest )
	    {
            if( _debugger.Meta.CanStepOut )
            {
                _vscode.Send( pRequest );
                _machine.StepOut();
            }
            else
	        _vscode.Send( pRequest, pErrorMessage: "Step Out is not supported" );
	    }

        static void VSCode_OnLaunch( Request pRequest, string pJSONSettings )
        {
            Initialise( pJSONSettings );

            if( !_machine.Start())
	            _vscode.Send(pRequest, pErrorMessage: "Could not connect to ZEsarUX");
	    }

	    static void VSCode_OnAttach( Request pRequest, string pJSONSettings )
	    {
	        Initialise( pJSONSettings );

            _tempFolder = Path.Combine( _settings.ProjectFolder, ".zxdbg" );
	        Directory.CreateDirectory( _tempFolder );

            if( !_debugger.Connect() )
	            _vscode.Send(pRequest, pErrorMessage: "Could not connect to ZEsarUX");

	        if( _settings.StopOnEntry )
	            _machine.Pause();
	    }

	    static void Initialise( string pJSONSettings )
	    {
	        _settings.FromJSON( pJSONSettings );
	        _settings.Validate();

            _machine.SourceMaps.Clear();
	        foreach( var map in _settings.SourceMaps )
	        {
	            var file = FindFile( map, "maps" );
                _machine.SourceMaps.Add( new Map( file ) );
	            Log.Write( Log.Severity.Message, "Loaded map: " + file );
	        }

            _machine.Disassembler.ClearLayers();
	        foreach( var table in _settings.OpcodeTables )
	        {
	            var file = FindFile( table, "opcodes" );
	            _machine.Disassembler.AddLayer( file );
	            Log.Write( Log.Severity.Message, "Loaded opcode layer: " + file );
	        }
	    }

	    static string FindFile( string pFilename, string pSubFolder )
	    {
	        if( File.Exists( pFilename ) )
	            return pFilename;

	        var path = Path.Combine( _settings.ProjectFolder, pFilename );
	        if( File.Exists( path ) )
	            return path;

	        path = Path.Combine( _settings.ExtensionPath, pFilename );
	        if( File.Exists( path ) )
	            return path;

	        path = Path.Combine( _settings.ExtensionPath, pSubFolder, pFilename );
	        if( File.Exists( path ) )
	            return path;

            throw new FileNotFoundException( "Can't find file", pFilename );
	    }

        static void VSCode_OnConfigurationDone( Request pRequest )
	    {
	    }

        static void VSCode_OnGetThreads( Request pRequest )
        {
            _vscode.Send( 
                pRequest,
                new ThreadsResponseBody( 
                    new List<Thread>()
                    {
                        new Thread( 1, "Main" )
                    }
                )
            );
        }

        static HashSet<byte> _callerOpcode3 = new HashSet<byte>()
        { 0xC4, 0xCC, 0xCD, 0xD4, 0xDC, 0xE4, 0xEC, 0xF4, 0xFC };

        static HashSet<byte> _callerOpcode2 = new HashSet<byte>()
        {  };

        static HashSet<byte> _callerOpcode1 = new HashSet<byte>()
        { 0xC7, 0xCF, 0xD7, 0xDF, 0xE7, 0xEF, 0xF7, 0xFF };

        static List<ushort> _stackAddresses = new List<ushort>();
        static List<StackFrame> _stackFrames = new List<StackFrame>();
	    static void VSCode_OnGetStackTrace( Request pRequest )
	    {
            _machine.Registers.Get();
	        _machine.Memory.GetMapping();

	        PrepopulateDisassemblyFile();

            // disassemble from current PC
	        var disassemblyUpdated = _machine.UpdateDisassembly( _machine.Registers.PC );

            // if current PC instruction is a jp/call etc, pre-disassemble the destination
	        disassemblyUpdated |= _machine.PreloadDisassembly( _machine.Registers.PC );

	        var stackBytes = new byte[20];
	        var caller = new byte[4];

	        _stackAddresses.Clear();
	        _stackFrames.Clear();

            // add current PC as an entry to the stack frame
            _stackAddresses.Add( _machine.Registers.PC );

            // get stack pos and limit how many bytes we read if we would go higher than 0xFFFF
	        ushort stackPos = _machine.Registers.SP;
            int maxBytes = Math.Min( 20, 0xFFFF - stackPos );

            // read bytes from SP onwards for analysis of the addresses
	        var bytes = _machine.Memory.Get( _machine.Registers.SP, maxBytes, stackBytes );

            // turn the bytes into ushorts
	        for( var i = 0; i < bytes; i += 2 )
	            _stackAddresses.Add( (ushort)( (stackBytes[i+1] << 8) | stackBytes[i] ) );

            // now check out each address
	        for( var i = 0; i < _stackAddresses.Count; i++ )
	        {
                // note: entry at i=0 is PC, so we don't need to get mem and we always show it

                var addr = _stackAddresses[i];

	            string symbol = null;

	            if( i == 0 )
	            {
                    // always try to get symbol for PC
	                symbol = GetPreviousSymbol( addr, ref disassemblyUpdated );
	            }
	            else
	            {
	                _machine.Memory.Get( (ushort)( addr - 3 ), 3, caller );

                    if( _callerOpcode3.Contains( caller[0] ) )
	                {
	                    addr -= 3;
	                    symbol = GetPreviousSymbol( addr, ref disassemblyUpdated );
	                    if( symbol != null ) symbol += " ↑";

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
	                    symbol = GetPreviousSymbol( addr , ref disassemblyUpdated );
	                }
	                else if( _callerOpcode1.Contains( caller[2] ) )
	                {
	                    addr -= 1;
	                    symbol = GetPreviousSymbol( addr, ref disassemblyUpdated );
	                    if( symbol != null ) symbol += " ↖";
	                }

	                _stackAddresses[i] = addr;
	            }

                _stackFrames.Add(
	                new StackFrame(
	                    i,
	                    symbol,
	                    symbol == null ? StackSource : DisassemblySource,
	                    0,
	                    0,
	                    i == 0 ? "subtle" : "normal"
                    )
	            );
	        }

            if( disassemblyUpdated )
                _machine.WriteDisassemblyFile( DisassemblyFile );

	        foreach( var frame in _stackFrames )
	            if( frame.name == null )
	                frame.name = _stackAddresses[frame.id].ToHex();
	            else
	                frame.line = _machine.FindLine( _stackAddresses[frame.id] );

            _vscode.Send(
                pRequest,
                new StackTraceResponseBody(
                    _stackFrames
                )
            );
        }

        static string GetPreviousSymbol( ushort pAddress, ref bool pDisassemblyUpdated )
        {
            var slot = _machine.Memory.GetSlot( pAddress );
            var label = _machine.SourceMaps.FindPreviousLabel( slot.Bank.ID, pAddress, 0x2000 );

            if( label == null )
                return null;

            //if( pAddress - label.Location <  )
            pDisassemblyUpdated |= _machine.UpdateDisassembly( label.Location );

            string offset = label.Location == pAddress ? "" : $"+{ pAddress - label.Location}";

            if( _settings.Stack.LabelPosition == Settings.StackSettings.LabelPositionEnum.Left )
                return $"{label.Labels[0]}{offset} {pAddress.ToHex()}";

            return $"{pAddress.ToHex()} {label.Labels[0]}{offset}";
        }

        static void VSCode_OnGetScopes( Request pRequest, int pFrameID )
        {
            _machine.UpdateDisassembly( _stackAddresses[pFrameID], DisassemblyFile );

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

            _vscode.Send( pRequest, new ScopesResponseBody( scopes ) );
        }

        static void VSCode_OnGetCompletions( Request pRequest, int pFrameID, string pText, int pColumn, int pLine )
        {
			//Log.Write( Log.Severity.Error, pRequest.arguments.ToString() );
        }		

        static void VSCode_OnEvaluate( Request pRequest, int pFrameID, string pContext, string pExpression, bool bHex, ref string pResult )
	    {
			if( pContext == "repl" )
				pResult = VSCode_OnEvaluate_REPL( pRequest, pExpression );
			else
				pResult = VSCode_OnEvaluate_Variable( pRequest, pExpression );
		}

		static string VSCode_OnEvaluate_REPL( Request pRequest, string pExpression )
		{
			return string.Join( "\n", _debugger.CustomCommand( pExpression ) );
		}

	    static char[] _varSplitChar = new[] { ' ', ',' };
	    static byte[] _tempVar = new byte[1024];
	    static string VSCode_OnEvaluate_Variable( Request pRequest, string pExpression )
	    {
	        var result = "n/a";

	        var parts = pExpression.Split( _varSplitChar, StringSplitOptions.RemoveEmptyEntries );

	        bool gotAddress = false;
	        bool gotLength = false;
	        bool gotData = false;
	        var isPointer = false;
	        var isRegister = false;
	        ushort address = 0;
	        int parsedLength = 0;
	        int length = 0;

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

	                if( _machine.Registers.IsValidRegister( text ) )
	                {
	                    address = _machine.Registers[text];
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

	                        length = _machine.Registers.Size( text );
	                        gotLength = true;
	                        gotData = true;
                        }
                    }
	                else
	                {
	                    address = Format.Parse( text );
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
	            _machine.Memory.Get( address, length, _tempVar );
	        }

            result = Format.ToHex( _tempVar, length );

	        if( isPointer && isRegister )
	            result = $"({address.ToHex()}) {result}";
            
	        return result;
	    }

        static void VSCode_OnGetVariables( Request pRequest, int pReference, List<Variable> pResult )
        {
            var value = _rootValues.All( pReference );

            if( value == null )
                return;

            value.Refresh();

            foreach( var child in value.Children )
                pResult.Add( CreateVariableForValue( child ) );
        }

	    static Variable CreateVariableForValue( Value pValue )
	    {
	        pValue.Refresh();

	        return new Variable(
	            pValue.Name,
	            pValue.Formatted,
	            "value",
	            pValue.Children.Count == 0 ? -1 : pValue.ID,
	            new VariablePresentationHint( "data" )
            );
	    }


	    static void VSCode_OnSetVariable( Request pRequest, Variable pVariable )
	    {
	        var value = _rootValues.AllByName( pVariable.name );
			value.Setter?.Invoke( value, pVariable.value );
	    }


        static void VSCode_OnDisconnect( Request pRequest )
	    {
	        _machine.Stop();
            _vscode.Stop();
	        _running = false;
	    }


        static HashSet<Spectrum.Breakpoint> _tempBreakpoints = new HashSet<Spectrum.Breakpoint>();
	    static List<VSCodeBreakpoint> _tempBreakpointsResponse = new List<VSCodeBreakpoint>();
	    static void VSCode_OnSetBreakpoints( Request pRequest )
	    {
	        string sourceName = pRequest.arguments.source.name;

	        if( sourceName != DisassemblySource.name )
	            return;

	        var max = _debugger.Meta.MaxBreakpoints;

            _tempBreakpointsResponse.Clear();

            // record old ones
	        _tempBreakpoints.Clear();
            foreach( var b in _machine.Breakpoints )
                _tempBreakpoints.Add( b );

            // set new ones
	        foreach( var breakpoint in pRequest.arguments.breakpoints )
	        {
	            string error = null;
	            int lineNumber = breakpoint.line;
	            Spectrum.Breakpoint bp = null;

	            var line = _machine.GetLineFromDisassemblyFile( lineNumber );

	            if( line != null )
	            {
	                bp = _machine.Breakpoints.Add( line );
	                _tempBreakpoints.Remove( bp );
	            }

	            if( bp == null )
	                error = "Invalid location";
                else if( bp.Index < 0 || bp.Index >= max )
	                error = "A maximum of " + max + " breakpoints are supported";

                if( error == null )
                    _tempBreakpointsResponse.Add(
                        new VSCodeBreakpoint(
                            bp.Index,
                            true,
                            bp.Line.Bank.ToString() + " " + bp.Line.Address.ToHex(),
                            DisassemblySource,
                            bp.Line.FileLine,
                            0,
                            bp.Line.FileLine,
                            0
                        )
                    );
                else
                    _tempBreakpointsResponse.Add(
                        new VSCodeBreakpoint(
                            -1,
                            false,
                            error,
                            DisassemblySource,
                            lineNumber,
                            0,
                            lineNumber,
                            0
                        )
                    );
            }

            // remove those no longer set
            foreach( var b in _tempBreakpoints )
                _machine.Breakpoints.Remove( b );


            // respond to vscode 

            _machine.Breakpoints.Commit();

            _vscode.Send( 
                pRequest,
                new SetBreakpointsResponseBody( _tempBreakpointsResponse )
            );
	    }



        // events from values/variables

        static void SetupValues( Value pValues, Machine pMachine )
        {
            _registersValues = pValues.Create( "Registers" );
            SetupValues_Registers( _registersValues );

            _pagingValues = pValues.Create( "Paging", pRefresher: SetupValues_Paging );
            SetupValues_Paging( _pagingValues );

            _settingsValues = pValues.Create("Settings");
			SetupValues_Settings( _settingsValues );
		}

        static void SetupValues_Registers( Value pVal )
		{
            Value reg16;

            pVal.Create(         "A",   pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );

            reg16 = pVal.Create( "HL",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex16 );
                reg16.Create(    "H",   pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );
                reg16.Create(    "L",   pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );

            reg16 = pVal.Create( "BC",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex16 );
                reg16.Create(    "B",   pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );
                reg16.Create(    "C",   pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );

            reg16 = pVal.Create( "DE",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex16 );
                reg16.Create(    "D",   pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );
                reg16.Create(    "E",   pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );


            pVal.Create(         "A'",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );

            reg16 = pVal.Create( "HL'", pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex16 );
                reg16.Create(    "H'",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );
                reg16.Create(    "L'",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );

            reg16 = pVal.Create( "BC'", pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex16 );
                reg16.Create(    "B'",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );
                reg16.Create(    "C'",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );

            reg16 = pVal.Create( "DE'", pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex16 );
                reg16.Create(    "D'",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );
                reg16.Create(    "E'",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );


            reg16 = pVal.Create( "IX",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex16 );
                reg16.Create(    "IXH", pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );
                reg16.Create(    "IXL", pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );

            reg16 = pVal.Create( "IY",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex16 );
                reg16.Create(    "IYH", pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );
                reg16.Create(    "IYL", pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );

            pVal.Create(         "PC",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex16 );

            pVal.Create(         "SP",  pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex16 );

            pVal.Create(         "I",   pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );

            pVal.Create(         "R",   pGet: GetReg, pSet: SetReg, pFormat: Format.ToHex8  );
        }

        static void SetupValues_Paging( Value pVal )
        {
            pVal.ClearChildren();
            foreach( var p in _machine.Memory.Slots )
            {
                var slot = pVal.Create( p.Min.ToHex(), delegate( Value pValue ) { pValue.Content = p.Bank.ID.ToString(); } );
            }
        }

        static void SetupValues_Settings( Value pVal )
        {
        }

        static void SetReg( Value pReg, string pValue )
		{
			_machine.Registers.Set( pReg.Name, pValue );
		}

		static string GetReg( Value pReg )
		{
		    return _machine.Registers[pReg.Name].ToString();
		}



        // events from Log

        static void Log_SendToVSCode( Log.Severity pLevel, string pMessage )
        {
            var type = pLevel == Log.Severity.Error ? OutputEvent.OutputEventType.stderr : OutputEvent.OutputEventType.stdout;
	        _vscode?.Send( new OutputEvent( type, pMessage + "\n" ) );
	    }


	    static bool _prepopulatedDisassemblyFile = false;
	    static void PrepopulateDisassemblyFile()
	    {
	        if( _prepopulatedDisassemblyFile )
	            return;

	        foreach( var f in _machine.SourceMaps )
	        {
	            foreach( var bankkvp in f.Banks )
	            {
	                var bank = bankkvp.Value;

                    // todo: check bank is paged in

	                foreach( var symbolkvp in bank.Symbols )
	                {
	                    var symbol = symbolkvp.Value;

	                    if( symbol.File != null && symbol.Labels != null && symbol.Labels.Count > 0 )
	                    {
	                        //Log.Write( Log.Severity.Message, bankkvp.Key + " " + symbol.Address.ToHex() + " " + string.Join( " ", symbol.Labels ) + " " + symbol.File.Filename + ":" + symbol.Line );
	                        //_machine.UpdateDisassembly( s.Value.Address );
	                    }
	                }
	            }
	        }

	        //_machine.WriteDisassemblyFile( DisassemblyFile );

	        _prepopulatedDisassemblyFile = true;
	    }

	    static string _tempFolder;

        static string _disassemblyFile;
        static string DisassemblyFile
        {
            get { return _disassemblyFile = _disassemblyFile ?? Path.Combine( _tempFolder, "disasm.zdis" ); }
        }

        static Source _stackSource;
        static Source StackSource
        {
            get { return _stackSource = _stackSource ?? new Source( "", "", 0, Source.SourcePresentationHintEnum.deemphasize ); }
        }

        static Source _disassemblySource;
        static Source DisassemblySource
        {
            get { return _disassemblySource = _disassemblySource ?? new Source( "asm", DisassemblyFile, 0, Source.SourcePresentationHintEnum.normal ); }
        }
    }
}

