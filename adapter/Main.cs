﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Spectrum;

namespace VSCodeDebugger
{
    public class Adapter
	{
	    static ZEsarUXConnection _zesarux;
	    static VSCodeConnection _vscode;
	    static bool _running;

	    static string _folder;

	    static Value _rootValues = new Value();
	    static Value _registersValues;
	    static Value _settingsValues;

	    static Machine _machine;

	    static bool _analysing;

    	static void Main(string[] argv)
	    {
	        // set up 
             
            Log.OnLog += Log_SendToVSCode;
	        Log.MaxSeverity = Log.Severity.Message;


            // vscode events

            _vscode = new VSCodeConnection();
	        _vscode.OnPause += VSCode_OnPause;
	        _vscode.OnContinue += VSCode_OnContinue;
	        _vscode.OnNext += VSCode_OnNext;
	        _vscode.OnStepIn += VSCode_OnStepIn;
	        _vscode.OnStepOut += VSCode_OnStepOut;
            _vscode.OnInitialize += VSCode_OnInitialize;
	        _vscode.OnLaunch += VSCode_OnLaunch;
	        _vscode.OnAttach += VSCode_OnAttach;
	        _vscode.OnConfigurationDone += VSCode_OnConfigurationDone;
	        _vscode.OnGetThreads += VSCode_OnGetThreads;
	        _vscode.OnGetStackTrace += VSCode_OnGetStackTrace;
	        _vscode.OnGetScopes += VSCode_OnGetScopes;
	        _vscode.OnGetVariables += VSCode_OnGetVariables;
	        _vscode.OnSetVariable += VSCode_OnSetVariable;
	        _vscode.OnGetSource += VSCode_OnGetSource;
			_vscode.OnGetCompletions += VSCode_OnGetCompletions;
	        _vscode.OnGetLoadedSources += VSCode_OnGetLoadedSources;
	        _vscode.OnDisconnect += VSCode_OnDisconnect;
	        _vscode.OnEvaluate += VSCode_OnEvaluate;


            // zesarux events

            _zesarux = new ZEsarUXConnection();	       
			_zesarux.OnData += Z_OnData; 


			// machine events

            _machine = new Machine( _zesarux );


			// tie all the values together

			SetupValues( _rootValues, _machine );


			// event loop

	        _running = true;

	        var wasActive = false;

	        // event loop
	        while( _running )
	        {
	            var vsactive = _vscode.Read();
				var zactive = _zesarux.Read();

	            switch( _zesarux.LastTransition )
	            {
					case ZEsarUXConnection.Transition.None:
						break;

					case ZEsarUXConnection.Transition.Started:
						_vscode.Continued( true );
						_zesarux.LastTransition = ZEsarUXConnection.Transition.None;
						break;

					case ZEsarUXConnection.Transition.Stopped:
						_vscode.Stopped( 1, "step", "step" );
						_zesarux.LastTransition = ZEsarUXConnection.Transition.None;
						break;
	            }

	            if( !vsactive )
	            {
	                if( wasActive )
	                    Log.Write( Log.Severity.Debug, "" );

					if( !zactive )
	                	System.Threading.Thread.Sleep( 10 );
	            }

	            wasActive = vsactive;
	        }
	    }

        static void Z_OnData( List<string> pList )
        {
            if( _analysing )
            {
                // _machine.UpdateDisassembly( pList, DisassemblyFile );

                _vscode.Send( new OutputEvent( OutputEvent.OutputEventType.console, "." ) );
                _zesarux.Send( "run verbose 10000" );

                return;
            }

            _vscode.Send( 
                new OutputEvent( 
                    OutputEvent.OutputEventType.console, 
                    string.Join( "\n", pList )
                )
            );
        }


        // events coming in from VSCode

        static void VSCode_OnInitialize( Request pRequest )
	    {
	        _vscode.Send(
	            pRequest,
	            new Capabilities()
	            {
	                supportsConfigurationDoneRequest = true,
					supportsCompletionsRequest = true
                }
	        );

	        _vscode.Initialized();
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
	        // must ack before sending anything else
	        _vscode.Send(pRequest);

            _machine.StepOver();
	    }

	    static void VSCode_OnStepIn( Request pRequest )
	    {
	        // must ack before sending anything else
	        _vscode.Send( pRequest );
	        _machine.Step();
	    }

	    static void VSCode_OnStepOut( Request pRequest )
	    {
	        _vscode.Send( pRequest, pErrorMessage: "Step Out is not supported" );
	    }

        static void VSCode_OnLaunch( Request pRequest )
        {
            if( !_machine.Start())
	            _vscode.Send(pRequest, pErrorMessage: "Could not connect to ZEsarUX");
	    }

	    static void VSCode_OnAttach( Request pRequest )
	    {
	        _folder = DynString( pRequest.arguments, "folder" );

	        if( string.IsNullOrWhiteSpace( _folder ) )
	        {
	            Log.Write( Log.Severity.Error, "Property 'folder' is missing or empty." );
	            _vscode.Send( pRequest, pErrorMessage: "Property 'folder' is missing or empty." );
                return;
	        }

	        if( !Directory.Exists( _folder ) )
	        {
	            Log.Write( Log.Severity.Error, "Property 'folder' refers to a folder that could not be found." );
	            _vscode.Send( pRequest, pErrorMessage: "Property 'folder' refers to a folder that could not be found." );
                return;
	        }

	        _tempFolder = Path.Combine( _folder, ".debug" );
	        Directory.CreateDirectory( _tempFolder );

            if( !_zesarux.Start() )
	            _vscode.Send(pRequest, pErrorMessage: "Could not connect to ZEsarUX");
	    }

	    static void VSCode_OnConfigurationDone( Request pRequest )
	    {
	    }

        static void VSCode_OnGetThreads( Request pRequest )
        {
            _vscode.Send( 
                pRequest,
                new ThreadsResponseBody( 
                    new List<VSCodeDebugger.Thread>()
                    {
                        new VSCodeDebugger.Thread( 1, "Main" )
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

            _machine.UpdateDisassembly( _machine.Registers.PC, DisassemblyFile );

            _stackFrames.Clear();

	        var stack = _machine.Stack;
	        for( int i = 0; i < stack.Count; i++ )
	        {   
	            _stackFrames.Add(
	                new StackFrame(
	                    i + 1,
	                    string.Format( "${0:X4} / {0}", stack[i] ),
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

        static void VSCode_OnGetScopes( Request pRequest )
        {
            int frameId = pRequest.arguments.frameId;

            _machine.UpdateDisassembly( _machine.Stack[frameId-1], DisassemblyFile );

            var scopes = new List<Scope>();

            foreach( var value in _rootValues.Children )
            {
                scopes.Add( 
                    new Scope( 
                        value.Name,
                        value.ID,
                        false
                    ) 
                );
            }

            _vscode.Send( pRequest, new ScopesResponseBody( scopes ) );
        }

        static void VSCode_OnGetCompletions( Request pRequest )
        {
			//Log.Write( Log.Severity.Error, pRequest.arguments.ToString() );
        }		

	    static void VSCode_OnGetLoadedSources( Request pRequest )
	    {
	    //    _vscode.Send(
        //        pRequest,
        //        new LoadedSourcesResponseBody(
        //            new List<Source>()
        //            {
        //                DisassemblySource()
        //            }
        //        )
        //    );
	    }

	    static void VSCode_OnGetSource( Request pRequest )
	    {
        //    _zesarux.GetRegisters();
        //
        //    DisassemblePC();
        //
        //    _vscode.Send( 
        //        pRequest,
        //        new SourceResponseBody(
        //            _zesarux.Disassembly,
        //            ""
        //        )
        //    );
	    }


        static void VSCode_OnEvaluate( Request pRequest )
	    {
			if( DynString( pRequest.arguments, "context", "" ) == "repl" )
				VSCode_OnEvaluate_REPL( pRequest );
			else
				VSCode_OnEvaluate_Variable( pRequest );
		}

		static void VSCode_OnEvaluate_REPL( Request pRequest )
		{
			string command = DynString( pRequest.arguments, "expression", "" );

		    if( !_analysing && command == "heat" )
		    {
		        _vscode.Send( pRequest, new EvaluateResponseBody("Analysing - type 'stop' to end ..." ) );

                _analysing = true;
		        _zesarux.Send( "run verbose 10000" );
		        return;
		    }

            if( _analysing && command == "stop" )
		    {
		        _vscode.Send( pRequest, new EvaluateResponseBody( "Stopped." ) );

                _analysing = false;
		        return;
		    }

			var result = _zesarux.SendAndReceive( command );
			
			_vscode.Send(
				 pRequest, 
				 new EvaluateResponseBody(
					 string.Join( "\n", result )
				 )
			);
		}

		static void VSCode_OnEvaluate_Variable( Request pRequest )
		{
	        var value = "";
            string formatted = "";

            string expression = pRequest.arguments.expression;
	        string prefix = "";

	        var split = expression.Split( new []{' ', ','}, StringSplitOptions.RemoveEmptyEntries );
	        int parseIndex = 0;

	        if( split[parseIndex].StartsWith( "(" ) && split[parseIndex].EndsWith( ")" ) )
	        {
	            var target = split[parseIndex].Substring( 1, split[parseIndex].Length - 2 );
	            ushort address;

	            if( _registersValues.HasAllByName( target ) )
	            {
	                address = Convert.ToUInt16( _registersValues.AllByName( target ).Content );
	                prefix = string.Format( "${0:X4}: ", address );
	            }
	            else
	                address = Format.Parse( target );

	            int length = 2;
	            parseIndex++;

                if( split.Length > parseIndex )
                    if( int.TryParse( split[parseIndex], out length ) )
                        parseIndex++;
                    else
                        length = 2;

	            value = _machine.Memory.Get( address, length );
	            formatted = value;
	        }
	        else if( _registersValues.HasAllByName( split[parseIndex] ) )
			{
				var reg = _registersValues.AllByName( split[parseIndex] );

				value = reg.Content;
				formatted = reg.Formatted;
				parseIndex++;
			}

	        if( split.Length > parseIndex )
				if( Format.ApplyRule( split[parseIndex], value, ref formatted ) )
					parseIndex++;

            _vscode.Send(
                pRequest, 
                new EvaluateResponseBody(
                    prefix + formatted
                )
            );
	    }



        static List<Variable> _tempVariables = new List<Variable>();
        static void VSCode_OnGetVariables( Request pRequest )
        {
            _tempVariables.Clear();

            int id = pRequest.arguments.variablesReference;
            var value = _rootValues.All(id);

            if( value != null )
            {
                value.Refresh();

                foreach( var child in value.Children )
                    _tempVariables.Add( CreateVariableForValue( child ) );
            }

            _vscode.Send(
                pRequest,
                new VariablesResponseBody(_tempVariables)
            );
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


	    static void VSCode_OnSetVariable( Request pRequest )
	    {
	        string name = pRequest.arguments.name.ToString();
	        string val = pRequest.arguments.value.ToString();

            Log.Write( Log.Severity.Message, name + " -> " + val );

	        var value = _rootValues.AllByName( name );

			if( value.Setter != null )
			{
				try
				{
					value.Setter.Invoke( value, val );
				}
				catch( Exception e )
				{
					_vscode.Send(
						pRequest,
						pErrorMessage: e.Message
					);
					return;
				}
			}

	        var var   = CreateVariableForValue( value );

            _vscode.Send(
                pRequest,
	            new SetVariableResponseBody( var.value, var.variablesReference )
            );
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
        static string DisassemblyFile
	    {
	        get { return Path.Combine( _tempFolder, "disasm.zdis" ); }
	    }

    }
}

