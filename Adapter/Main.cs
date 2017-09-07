using System;
using System.Collections.Generic;
using VSCodeDebugAdapter;
using Thread = System.Threading.Thread;

namespace ZEsarUXDebugger
{
    public class ZMain
	{
	    static ZEsarUXConnection _zesarux;
	    static VSCodeConnection _vscode;
	    static bool _active;

    	static void Main(string[] argv)
	    {
	        ZMain.Log( LogLevel.Message, "main: starting...");

	        // set up 
	        _vscode = new VSCodeConnection();
	        _vscode.OnPause += VSCode_OnPause;
	        _vscode.OnContinue += VSCode_OnContinue;
	        _vscode.OnNext += VSCode_OnNext;
	        _vscode.OnStepIn += VSCode_OnStepIn;
            _vscode.OnInitialize += VSCode_OnInitialize;
	        _vscode.OnLaunch += VSCode_OnLaunch;
	        _vscode.OnAttach += VSCode_OnAttach;
	        _vscode.OnConfigurationDone += VSCode_OnConfigurationDone;
	        _vscode.OnGetThreads += VSCode_OnGetThreads;
	        _vscode.OnGetStackTrace += VSCode_OnGetStackTrace;
	        _vscode.OnGetScopes += VSCode_OnGetScopes;
	        _vscode.OnGetVariables += VSCode_OnGetVariables;
	        _vscode.OnGetSource += VSCode_OnGetSource;
	        _vscode.OnGetLoadedSources += VSCode_OnGetLoadedSources;
	        _vscode.OnDisconnect += VSCode_OnDisconnect;

            _zesarux = new ZEsarUXConnection();
	        // _zesarux.OnPaused += Z_OnPaused;
	        // _zesarux.OnContinued += Z_OnContinued;
	        // _zesarux.OnStepped += Z_OnStepped;
            // _zesarux.OnRegisterChange += Z_OnRegisterChange;

	        _active = true;

	        var wasActive = false;

	        // event loop
	        while( _active )
	        {
	            var active = _vscode.Read() || _zesarux.Read();

	            switch( _zesarux.StateChange )
	            {
	                case ZEsarUXConnection.RunningStateChangeEnum.Started:
                        _vscode.Continued( true );
	                    break;

                    case ZEsarUXConnection.RunningStateChangeEnum.Stopped:
                        _vscode.Stopped( 1, "step", "step" );
                        break;
	            }

	            if( !active )
	            {
	                if( wasActive )
	                    ZMain.Log( LogLevel.Debug, "" );

	                Thread.Sleep( 10 );
	            }

	            wasActive = active;
	        }

	        ZMain.Log( LogLevel.Message, "main: stopped." );
	    }


	    // events coming in from VSCode

	    static void VSCode_OnInitialize( Request pRequest )
	    {
	        //Log( LogLevel.Message, pRequest.arguments.ToString() );

	        _vscode.Send(
	            pRequest,
	            new Capabilities()
	            {
	                supportsConfigurationDoneRequest = true
                }
	        );

	        _vscode.Initialized();
	    }

         
	    static void VSCode_OnContinue( Request pRequest )
	    {
	        _zesarux.Continue();
	    }

	    static void VSCode_OnPause( Request pRequest )
	    {
	        _zesarux.Pause();
	    }

        static void VSCode_OnNext( Request pRequest )
	    {
	        // must ack before sending anything else
	        _vscode.Send(pRequest);

            _zesarux.StepOver();
	    }

	    static void VSCode_OnStepIn( Request pRequest )
	    {
	        // must ack before sending anything else
	        _vscode.Send(pRequest);

            _zesarux.Step();
        }

        static void VSCode_OnLaunch( Request pRequest )
	    {
	        if (!_zesarux.Start())
	            _vscode.Send(pRequest, pErrorMessage: "Could not connect to ZEsarUX");
	    }

	    static void VSCode_OnAttach( Request pRequest )
	    {
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
                    new List<VSCodeDebugAdapter.Thread>()
                    {
                        new VSCodeDebugAdapter.Thread( 1, "Main" )
                    }
                )
            );
        }

	    static Source DisassemblySource()
	    {
	        return new Source( "Disassembly", _zesarux.DisassemblyFilePath, 0, "normal" );

	    }

        static List<StackFrame> _stackFrames = new List<StackFrame>();
	    static void VSCode_OnGetStackTrace( Request pRequest )
	    {
	        _zesarux.GetPorts();
            _zesarux.GetRegisters();
	        _zesarux.GetStackTrace();
            _zesarux.UpdateDisassembly( _zesarux.PC );

            _stackFrames.Clear();

	        var stack = _zesarux.Stack;
	        for( int i = 0; i < stack.Count; i++ )
	        {   
	            _stackFrames.Add(
	                new StackFrame(
	                    i + 1,
	                    string.Format( "${0:X4} / {0}", stack[i] ),
	                    DisassemblySource(),
	                    _zesarux.FindLine( stack[i] ),
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

            _zesarux.UpdateDisassembly( _zesarux.Stack[frameId-1] );

            _vscode.Send(
                pRequest,
                new ScopesResponseBody(
                    new List<Scope>()
                    {
                        new Scope( "Registers", 10000, false ),
                        new Scope( "Ports", 20000, false )
                    }
                )
            );
        }

	    static void VSCode_OnGetLoadedSources( Request pRequest )
	    {
	     //   _vscode.Send(
         //       pRequest,
         //       new LoadedSourcesResponseBody(
         //           new List<Source>()
         //           {
         //               DisassemblySource()
         //           }
         //       )
         //   );
	    }

	    static void VSCode_OnGetSource( Request pRequest )
	    {
         //   _zesarux.GetRegisters();
         //
         //   DisassemblePC();

	     //   _vscode.Send( 
         //       pRequest,
         //       new SourceResponseBody(
         //           _zesarux.Disassembly,
         //           ""
         //       )
         //   );
	    }


	    static List<Variable> _variables = new List<Variable>();
        static void VSCode_OnGetVariables( Request pRequest )
        {
            _variables.Clear();

            var data = new VariablePresentationHint( "data" );
            var index = 0;

            switch( (int)pRequest.arguments.variablesReference )
            {
                case 10000:     // registers

                    _zesarux.GetRegisters();
                    foreach ( var kp in _zesarux.Registers )
                        _variables.Add( 
                            new Variable( 
                                kp.Key,
                                string.Format( "${0:X4} / {0}", kp.Value ), 
                                "register", 
                                -1, 
                                data ) 
                        );

                    break;

                case 20000:     // ports

                    _zesarux.GetPorts();
                    break;

                default:
                    break;
            }

            _variables.Sort( ( pLeft, pRight ) => String.Compare( pLeft.name, pRight.name, StringComparison.Ordinal ) );

            _vscode.Send(
                pRequest,
                new VariablesResponseBody(_variables)
            );
        }

        static void VSCode_OnDisconnect( Request pRequest )
	    {
	        _zesarux.Stop();
	        _active = false;
	    }


        //static void Z_OnRegisterChange( string pRegister, string pValue )
        //{
        //    _vscode.Send(
        //        new var
        //    );
        //}


        //public override void SetBreakpoints( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: setbreakpoints");
        //}

        //public override void Next( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: next");
        //}

        //public override void StepIn( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: stepin");
        //}

        //public override void StepOut( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: stepout");
        //}

        //public override void StackTrace( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: stacktrace");
        //}

        //public override void Variables( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: variables");
        //}

        //public override void Evaluate( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: evaluate");
        //}

        //public override void SetExceptionBreakpoints( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: set exception breakpoints");
        //}

        //public override void SetFunctionBreakpoints( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: set function breakpoints");
        //}

        //public override void Source( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: source");
        //}

	    static LogLevel _log = LogLevel.Message;
        static bool _inLog = false;
	    public static void Log( LogLevel pLevel, string pMessage )
	    {
	        if( pLevel > _log ) return;

            // don't log the fact that we're logging a log message
	        if( _inLog ) return;

            // send the log message to VSCode
	        _inLog = true;

            if( pLevel == LogLevel.Error )
	            _vscode?.Send( new OutputEvent( OutputEvent.OutputEventType.stderr, pMessage + "\n" ) );
            else
                _vscode?.Send( new OutputEvent( OutputEvent.OutputEventType.stdout, pMessage + "\n" ) );

	        _inLog = false;
	    }
    }

    public enum LogLevel
    {
        Error,
        Message,
        Debug
    }
}

