using System;
using System.Threading.Tasks;
using Thread = System.Threading.Thread;
using VSCodeDebug;

namespace ZDebug
{
    class Session
    {
        ConnectionZEsarUX _zesarux;
        ConnectionVSCode  _vscode;

        bool _running;
        
        public void Run()
        {
            ZMain.Log("vscode: starting...");

            _vscode = new ConnectionVSCode();
            _vscode.OnPause += VSCode_OnPause;
            _vscode.OnContinue += VSCode_OnContinue;
            _vscode.OnInitialize += VSCode_OnInitialize;

            ZMain.Log("zesarux: starting...");

            _zesarux = new ConnectionZEsarUX();
            _zesarux.OnPaused += Z_OnPaused;
            _zesarux.OnContinued += Z_OnContinued;

            _running = true;

            while( _running )
            {
                _vscode.Read();
                _zesarux.Read();
                Thread.Sleep(100);
            }

            ZMain.Log("vscode: stopped");
        }



        // ZEsarUX events

        void Z_OnContinued()
        {
            _vscode.Continued();
        }

        void Z_OnPaused()
        {
            _vscode.Paused();
        }



        // VSCode events

        void VSCode_OnInitialize(Request pRequest)
        {
            _vscode.Send(
                pRequest,
                new Capabilities()
                {
                    // This debug adapter does not need the configurationDoneRequest
                    supportsConfigurationDoneRequest = true,

                    // This debug adapter does not support function breakpoints
                    supportsFunctionBreakpoints = true,

                    // This debug adapter doesn't support conditional breakpoints
                    supportsConditionalBreakpoints = true,

                    // This debug adapter does not support a side effect free evaluate request for data hovers
                    supportsEvaluateForHovers = true

                    // This debug adapter does not support exception breakpoint filters
                    //exceptionBreakpointFilters = new dynamic[0]
                }
            );

            _vscode.Send( new InitializedEvent() );
        }


        void VSCode_OnContinue(Request pRequest)
        {
            _zesarux.Continue();
        }

        void VSCode_OnPause(Request pRequest)
        {
            _zesarux.Pause();
        }

        //public override void Initialize( Response response, dynamic args )
        //{
        //    ZMain.Log("vscode: initialize");

        //    SendResponse(response, new Capabilities()
        //    {
        //        // This debug adapter does not need the configurationDoneRequest
        //        supportsConfigurationDoneRequest = true,

        //        // This debug adapter does not support function breakpoints
        //        supportsFunctionBreakpoints = true,

        //        // This debug adapter doesn't support conditional breakpoints
        //        supportsConditionalBreakpoints = true,

        //        // This debug adapter does not support a side effect free evaluate request for data hovers
        //        supportsEvaluateForHovers = true

        //        // This debug adapter does not support exception breakpoint filters
        //        //exceptionBreakpointFilters = new dynamic[0]
        //    });

        //    // ready to accept breakpoints immediately
        //    SendEvent(new InitializedEvent());
        //}

        //public override void Launch( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: launch");

        //    if( !zesarux.Start() )
        //    {
        //        response.SetErrorBody( "Could not connect to ZEsarUX" );
        //    }
        //}

        //public override void Attach( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: attach");

        //    if (!zesarux.Start())
        //    {
        //        response.SetErrorBody("Could not connect to ZEsarUX");
        //    }
        //}

        //public override void ConfigurationDone( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: configuration done");
        //}

        //public override void Disconnect( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: disconnect");

        //    zesarux.Send( "exit" );
        //    zesarux.Stop();
        //}

        //public override void SetBreakpoints( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: setbreakpoints");
        //}

        //public override void Continue( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: continue");
        //    zesarux.Send("exit-cpu-step");
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

        //public override void Pause( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: pause");
        //    zesarux.Send( "enter-cpu-step" );
        //}

        //public override void StackTrace( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: stacktrace");
        //}

        //public override void Scopes( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: scopes");
        //}

        //public override void Variables( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: variables");
        //}

        //public override void Threads( Response response, dynamic arguments )
        //{
        //    ZMain.Log("vscode: threads");

        //    response.SetBody( 
        //        new ThreadsResponseBody( 
        //            new List<VSCodeDebug.Thread>()
        //            {
        //                new VSCodeDebug.Thread(0, "Main")
        //            }
        //        ) 
        //    );
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
    }
}
