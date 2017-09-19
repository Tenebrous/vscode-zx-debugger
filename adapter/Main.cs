using System;
using System.Collections.Generic;
using System.IO;
using Spectrum;
using VSCode;

namespace ZXDebug
{
    public static class Adapter
	{
	    static Connection _vscode;
	    static Debugger _debugger;
	    static bool _running;

	    static Value _rootValues = new Value();
	    static Value _registersValues;
	    static Value _settingsValues;

	    static Machine _machine;

	    static Settings _settings = new Settings();


    	static void Main(string[] argv)
	    {
	        // set up 
             

            // wire the logging stuff up to VSCode's console output
            Log.OnLog += Log_SendToVSCode;
	        Log.MaxSeverityConsole = Log.Severity.Message;
            Log.MaxSeverityLog = Log.Severity.Debug;


            // vscode events

            _vscode = new Connection();
            _vscode.InitializeEvent += VSCode_OnInitialize;
	        _vscode.DisconnectEvent += VSCode_OnDisconnect;
	        _vscode.LaunchEvent += VSCode_OnLaunch;
	        _vscode.AttachEvent += VSCode_OnAttach;
	        _vscode.ConfigurationDoneEvent += VSCode_OnConfigurationDone;

	        _vscode.PauseEvent += VSCode_OnPause;
	        _vscode.ContinueEvent += VSCode_OnContinue;
	        _vscode.NextEvent += VSCode_OnNext;
	        _vscode.StepInEvent += VSCode_OnStepIn;
	        _vscode.StepOutEvent += VSCode_OnStepOut;

	        _vscode.GetThreadsEvent += VSCode_OnGetThreads;
	        _vscode.GetStackTraceEvent += VSCode_OnGetStackTrace;
	        _vscode.GetScopesEvent += VSCode_OnGetScopes;

	        _vscode.GetVariablesEvent += VSCode_OnGetVariables;
	        _vscode.SetVariableEvent += VSCode_OnSetVariable;
	        // _vscode.OnGetSource += VSCode_OnGetSource;
	        // _vscode.OnGetLoadedSources += VSCode_OnGetLoadedSources;
			_vscode.GetCompletionsEvent += VSCode_OnGetCompletions;
	        _vscode.EvaluateEvent += VSCode_OnEvaluate;


            // zesarux events

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


	        // event loop
	        while( _running )
	        {
	            var vsactive = _vscode.Process();
				var dbgactive = _debugger.Process();

                if( !vsactive )
                    System.Threading.Thread.Sleep( 10 );
	        }
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

        static void VSCode_OnNext( Request pRequest )
	    {
            _vscode.Send( pRequest );
            _machine.StepOver();
	    }

        static void VSCode_OnStepIn( Request pRequest )
	    {
	        _vscode.Send( pRequest );
	        _machine.Step();
	    }

        static void VSCode_OnStepOut( Request pRequest )
	    {
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

            _tempFolder = Path.Combine( _settings.cwd, ".zxdbg" );
	        Directory.CreateDirectory( _tempFolder );

            if( !_debugger.Connect() )
	            _vscode.Send(pRequest, pErrorMessage: "Could not connect to ZEsarUX");

	        if( _settings.stopOnEntry )
	            _machine.Pause();
	    }

	    static void Initialise( string pJSONSettings )
	    {
	        _settings.FromJSON( pJSONSettings );
	        _settings.Validate();

	        _machine.HexPrefix = _settings.hexPrefix;
	        _machine.HexSuffix = _settings.hexSuffix;

            _machine.SourceMaps.Clear();
	        foreach( var map in _settings.sourceMaps )
	        {
	            var file = FindFile( map, "maps" );
                _machine.SourceMaps.Add( new SourceMap( file ) );
	            Log.Write( Log.Severity.Message, "Loaded " + file );
	        }

            _machine.Disassembler.ClearLayers();
	        foreach( var table in _settings.opcodeTables )
	        {
	            var file = FindFile( table, "opcodes" );
	            _machine.Disassembler.AddLayer( file );
	            Log.Write( Log.Severity.Message, "Loaded " + file );
	        }
	    }

	    static string FindFile( string pFilename, string pSubFolder )
	    {
	        if( File.Exists( pFilename ) )
	            return pFilename;

	        var path = Path.Combine( _settings.cwd, pFilename );
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

	    static Source DisassemblySource()
	    {
	        return new Source( "-", DisassemblyFile, 0, Source.SourcePresentationHintEnum.deemphasize );
	    }

        static List<StackFrame> _stackFrames = new List<StackFrame>();
	    static void VSCode_OnGetStackTrace( Request pRequest )
	    {
	        _machine.Registers.Get();
	        _machine.Memory.GetMapping();
	        _machine.Stack.Get();

            // disassemble from current PC
	        var updated = _machine.UpdateDisassembly( _machine.Registers.PC, DisassemblyFile );

            // if current PC instruction is a jp/call etc, pre-disassemble the destination
	        updated |= _machine.PreloadDisassembly( _machine.Stack[0], DisassemblyFile );

            // if( updated )
            //   _vscode.Send( new Event( "refreshDisasm", new { file = DisassemblyFile } ) );

            _stackFrames.Clear();

	        var stack = _machine.Stack;
	        for( var i = 0; i < stack.Count; i++ )
	        {   
	            _stackFrames.Add(
	                new StackFrame(
	                    i + 1,
	                    $"${stack[i]:X4} / {stack[i]}",
	                    DisassemblySource(),
	                    _machine.FindLine( stack[i] ),
	                    0,
	                    "normal"
                    )
	            );
	        }

	        _vscode.Send(
                pRequest,
                new StackTraceResponseBody(
                    _stackFrames
                )
            );
        }

        static void VSCode_OnGetScopes( Request pRequest, int pFrameID )
        {
            _machine.UpdateDisassembly( _machine.Stack[pFrameID-1], DisassemblyFile );

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
			return _debugger.CustomCommand( pExpression );
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
	            result = "(" + ( _machine.FormattedHex16( address ) ) + ") " + result;
            
	        return result;
	    }

        static void VSCode_OnGetVariables( Request pRequest, int pReference, List<Variable> pResult )
        {
            var value = _rootValues.All( pReference );

            if( value != null )
            {
                value.Refresh();

                foreach( var child in value.Children )
                    pResult.Add( CreateVariableForValue( child ) );
            }
        }

	    static Variable CreateVariableForValue( Value pValue )
	    {
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
            // Log.Write( Log.Severity.Message, name + " -> " + val );

	        var value = _rootValues.AllByName( pVariable.name );
			value.Setter?.Invoke( value, pVariable.value );
	    }


        static void VSCode_OnDisconnect( Request pRequest )
	    {
	        _machine.Stop();
	        _running = false;
	    }



        // events from values/variables


        static void SetupValues( Value pValues, Machine pMachine )
        {
            _registersValues = pValues.Create("Registers");
			SetupValues_Registers( _registersValues );

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


        static string DynString( dynamic pArgs, string pName, string pDefault = null )
	    {
	        var result = (string)pArgs[pName];

	        if( result == null )
	            return pDefault;

	        result = result.Trim();

	        if( result.Length == 0 )
	            return pDefault;

	        return result;
	    }

	    static string _tempFolder;
        static string DisassemblyFile => Path.Combine( _tempFolder, "disasm.zdis" );
	}
}

