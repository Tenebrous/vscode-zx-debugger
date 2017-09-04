using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSCodeDebug;
using Thread = System.Threading.Thread;

namespace ZDebug
{
    class Session : DebugSession
    {
        ConnectionZEsarUX z = new ConnectionZEsarUX();
        Task zt;

        public Session() : base( debuggerLinesStartAt1 : true )
        {
        }

        public Task Start()
        {
            ZMain.Log("vscode: starting...");

            var task = base.Start( Console.OpenStandardInput(), Console.OpenStandardOutput() );

            while( !task.IsCompleted )
            {
                z.Read();
                task.Wait(100);
            }

            ZMain.Log("vscode: stopped");

            return null;
        }

        public override void Initialize( Response response, dynamic args )
        {
            ZMain.Log("vscode: initialize");

            SendResponse(response, new Capabilities()
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
            });

            // ready to accept breakpoints immediately
            SendEvent(new InitializedEvent());
        }

        public override void Launch( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: launch");

            if( !z.Start() )
            {
                response.SetErrorBody( "Could not connect to ZEsarUX" );
            }
        }

        public override void Attach( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: attach");

            if (!z.Start())
            {
                response.SetErrorBody("Could not connect to ZEsarUX");
            }
        }

        public override void ConfigurationDone( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: configuration done");
        }

        public override void Disconnect( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: disconnect");

            z.Send( "exit" );
            z.Stop();
        }

        public override void SetBreakpoints( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: setbreakpoints");
        }

        public override void Continue( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: continue");
            z.Send("exit-cpu-step");
        }

        public override void Next( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: next");
        }

        public override void StepIn( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: stepin");
        }

        public override void StepOut( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: stepout");
        }

        public override void Pause( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: pause");
            z.Send( "enter-cpu-step" );

            SendEvent( new StoppedEvent( 0, "user request" ) );
        }

        public override void StackTrace( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: stacktrace");
        }

        public override void Scopes( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: scopes");
        }

        public override void Variables( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: variables");
        }

        public override void Threads( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: threads");

            response.SetBody( 
                new ThreadsResponseBody( 
                    new List<VSCodeDebug.Thread>()
                    {
                        new VSCodeDebug.Thread(0, "Main")
                    }
                ) 
            );
        }

        public override void Evaluate( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: evaluate");
        }

        public override void SetExceptionBreakpoints( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: set exception breakpoints");
        }

        public override void SetFunctionBreakpoints( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: set function breakpoints");
        }

        public override void Source( Response response, dynamic arguments )
        {
            ZMain.Log("vscode: source");
        }
    }
}
