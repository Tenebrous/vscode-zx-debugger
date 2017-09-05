using System;
using System.Collections.Generic;
using System.IO;
using VSCodeDebug;
using Thread = System.Threading.Thread;

namespace ZDebug
{
    public class ZMain
	{
	    static ConnectionZEsarUX _zesarux;
	    static ConnectionVSCode _vscode;
	    static bool _running;

    	static void Main(string[] argv)
	    {
	        ZMain.Log("main: starting...");

	        // set up 
	        _vscode = new ConnectionVSCode();
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

            _zesarux = new ConnectionZEsarUX();
	        _zesarux.OnPaused += Z_OnPaused;
	        _zesarux.OnContinued += Z_OnContinued;
	        _zesarux.OnStepped += Z_OnStepped;
            // _zesarux.OnRegisterChange += Z_OnRegisterChange;

	        _running = true;

	        var continuous = false;

	        // event loop
	        while( _running )
	        {
	            if( _vscode.Read() || _zesarux.Read() )
	                continuous = true;
	            else
	            {
	                if( continuous )
	                {
	                    ZMain.Log( "" );
	                    continuous = false;
	                }

	                Thread.Sleep( 10 );
	            }
	        }

	        ZMain.Log("main: stopped.");
	    }


	    // events coming in from VSCode

	    static void VSCode_OnInitialize( Request pRequest )
	    {
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
                    new List<VSCodeDebug.Thread>()
                    {
                        new VSCodeDebug.Thread( 1, "Main" )
                    }
                )
            );
        }

	    static Source DisassemblySource()
	    {
	        return new Source( "Disassembly", _zesarux.DisassemblyFilePath, 0, "deemphasize" );

	    }

        static List<StackFrame> _stackFrames = new List<StackFrame>();
	    static void VSCode_OnGetStackTrace( Request pRequest )
	    {
	        _zesarux.GetMachine();

            _zesarux.GetStackTrace();

            _zesarux.GetRegisters();

	        DisassemblePC();

            _stackFrames.Clear();
            _stackFrames.Add( new StackFrame (0, "Current", DisassemblySource(), 0, 0, "normal" ) );

	        var stack = _zesarux.Stack;
            for( int i = 0; i < stack.Count; i++ )
                _stackFrames.Add( new StackFrame( i+1, stack[i], DisassemblySource(), 0, 0, "normal" ) );

            _vscode.Send(
                pRequest,
                new StackTraceResponseBody(
                    _stackFrames
                )
            );
        }

        static void VSCode_OnGetScopes( Request pRequest )
        {
            _vscode.Send(
                pRequest,
                new ScopesResponseBody(
                    new List<Scope>()
                    {
                        new Scope( "Registers", 1, false )
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


	    static void DisassemblePC()
	    {
            int pc;

	        _zesarux.Registers.TryGetValue( "PC", out pc );
	        _zesarux.Disassemble(pc, 30);
        }

	    static List<Variable> _variables = new List<Variable>();
        static void VSCode_OnGetVariables( Request pRequest )
        {
            _zesarux.GetRegisters();

            _variables.Clear();

            int i = 0;
            foreach( var kp in _zesarux.Registers )
                _variables.Add( 
                    new Variable(
                        kp.Key, kp.Value.ToString(),
                        type: "register",
                        variablesReference: -1,
                        presentationHint: new VariablePresentationHint("data")
                    ) 
                );

            _variables.Sort( ( pLeft, pRight ) => String.Compare( pLeft.name, pRight.name, StringComparison.Ordinal ) );

            _vscode.Send(
                pRequest,
                new VariablesResponseBody(_variables)
            );
        }


        static void VSCode_OnDisconnect( Request pRequest )
	    {
	        _zesarux.Stop();
	        _running = false;
	    }




	    // events coming in from ZEsarUX

	    static void Z_OnContinued()
	    {
	        _vscode.Continued( true );
	    }

	    static void Z_OnPaused()
	    {
	        _vscode.Stopped( 1, "pause" );
	        _vscode.SourceChanged( DisassemblySource() );
        }

	    static void Z_OnStepped()
	    {
	        _vscode.Stopped( 1, "step" );
            _vscode.SourceChanged( DisassemblySource() );
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


        static bool _inLog = false;
	    public static void Log(string pMessage)
	    {
            // don't log the fact that we're logging a log message
	        if( _inLog ) return;

            // send the log message to VSCode
	        _inLog = true;
	        _vscode?.Send( new OutputEvent( "console", pMessage + "\n" ) );
	        _inLog = false;
	    }
    }
}

