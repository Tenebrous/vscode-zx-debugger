using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using ZXDebug;

namespace VSCode
{
    public class Connection
    {
        public delegate void EventHandler( Request pRequest );

        public delegate void InitialiseHandler( Request pRequest, Capabilities pResult );
        public event InitialiseHandler  InitializeEvent;

        public delegate void LaunchHandler( Request pRequest, string pJSON );
        public event LaunchHandler LaunchEvent;

        public delegate void AttachHandler( Request pRequest, string pJSON );
        public event AttachHandler AttachEvent;

        public event EventHandler DisconnectEvent;
        public event EventHandler PauseEvent;
        public event EventHandler ContinueEvent;
        public event EventHandler NextEvent;
        public event EventHandler StepInEvent;
        public event EventHandler StepOutEvent;
        public event EventHandler GetStackTraceEvent;

        public delegate void GetVariablesHandler( Request pRequest, int pReference, List<Variable> pResult );
        public event GetVariablesHandler GetVariablesEvent;

        public delegate void SetVariablesHandler( Request pRequest, Variable pVariable );
        public event SetVariablesHandler SetVariableEvent;

        public event EventHandler GetThreadsEvent;

        public delegate void GetCompletionsHandler( Request pRequest, int pFrameID, string pText, int pColumn, int pLine );
        public event GetCompletionsHandler GetCompletionsEvent;

        public delegate void GetScopesHandler( Request pRequest, int pFrameID );
        public event GetScopesHandler GetScopesEvent;

        public event EventHandler GetSourceEvent;
        public event EventHandler GetLoadedSourcesEvent;
        public event EventHandler ConfigurationDoneEvent;

        public delegate void EvaluateHandler( Request pRequest, int pFrameID, string pContext, string pExpression, bool pHex, ref string pResult );
        public event EvaluateHandler EvaluateEvent;

        
        Stream _output;
        StringBuilder _inputBuffer = new StringBuilder();

        System.Threading.Thread _inputThread;
        Encoding _inputEncoding;

        public Connection()
        {
            _inputEncoding = Console.InputEncoding;
            _output = Console.OpenStandardOutput();

            _inputThread = new System.Threading.Thread( ReadThread );
            _inputThread.Start( Console.OpenStandardInput() );
        }

        byte[] _tempReadBuffer = new byte[4096];
        void ReadThread( object pStream )
        {
            var stream = (Stream)pStream;

            try
            {
                while( true )
                {
                    var read = stream.Read( _tempReadBuffer, 0, _tempReadBuffer.Length );

                    if( read > -1 )
                        lock( _inputBuffer )
                        {
                            var str = _inputEncoding.GetString( _tempReadBuffer, 0, read );
                            _inputBuffer.Append( str );
                        }
                }
            }
            catch( ThreadAbortException )
            {
                // this is fine, means we can exit the loop
            }
        }
        
        public void Stop()
        {
            _inputThread.Abort();
        }


        public bool Process()
        {
            lock( _inputBuffer )
            {
                if( _inputBuffer.Length <= 0 )
                    return false;

                ProcessData();
            }

            return true;
        }

        static Regex _contentLength = new Regex(@"Content-Length: (\d+)\r\n\r\n");
        void ProcessData()
        {
            while( true )
            {
                var data = _inputBuffer.ToString();

                // find size text
                var match = _contentLength.Match( data );

                if( !match.Success || match.Groups.Count != 2 )
                    break;

                var size = Convert.ToInt32( match.Groups[1].ToString() );
                var end = match.Index + match.Length;

                if( data.Length < end + size )
                    break;

                var message = data.Substring( end, size );

                _inputBuffer.Remove( 0, end + size );

                ProcessMessage( message );
            }
        }

        void ProcessMessage( string pMessage )
        {
            Log.Write( Log.Severity.Verbose, "vscode: (in)  " + pMessage );

            Request request;

            try
            {
                request = JsonConvert.DeserializeObject<Request>( pMessage );
            }
            catch( Exception e )
            {
                Log.Write( Log.Severity.Error, "The following message caused an error: [" + pMessage.Replace( "\r", "\\r" ).Replace( "\n", "\\n" ) + "]" );
                Log.Write( Log.Severity.Error, e.ToString() );
                return;
            }

            if( request == null )
                return;

            Log.Write( Log.Severity.Debug, "vscode: (in)  " + request.type + " " + request.command );

            if( request.type == "request" )
            {
                HandleMessage( request.command, request.arguments, request );

                if( !request.responded )
                    Send( request );
            }
        }


        // commands/events sent to vscode

        public void Initialized()
        {
            Send( new InitializedEvent() );
        }

        public void Stopped( int pThread, string pReason, string pDescription )
        {
            Send( new StoppedEvent( pThread, pReason, pDescription ) );
        }

        public void Continued( bool pAllThreads )
        {
            Send( new ContinuedEvent( pAllThreads ) );
        }


        static List<Variable> _tempVariables = new List<Variable>();

        void HandleMessage( string pCommand, dynamic pArgs, Request pRequest )
        {
            Log.Write( Log.Severity.Verbose, "vscode: (in) [" + pCommand + "]" );

            pArgs = pArgs ?? new { };

            try
            {

                switch( pCommand )
                {
                    case "initialize":
                        var cap = new Capabilities();
                        InitializeEvent?.Invoke( pRequest, cap );
                        Send( pRequest, cap );
                        Initialized();
                        break;

                    case "configurationDone":
                        ConfigurationDoneEvent?.Invoke(pRequest);
                        break;

                    case "launch":
                        LaunchEvent?.Invoke( pRequest, JsonConvert.SerializeObject( pArgs ) );
                        break;

                    case "attach":
                        AttachEvent?.Invoke( pRequest, JsonConvert.SerializeObject( pArgs ) );
                        break;

                    case "disconnect":
                        DisconnectEvent?.Invoke( pRequest );
                        break;

                    case "next":
                        NextEvent?.Invoke( pRequest );
                        break;

                    case "continue":
                        ContinueEvent?.Invoke( pRequest );
                        break;

                    case "stepIn":
                        StepInEvent?.Invoke( pRequest );
                        break;

                    case "stepOut":
                        StepOutEvent?.Invoke( pRequest );
                        break;

                    case "pause":
                        PauseEvent?.Invoke( pRequest );
                        break;

                    case "threads":
                        GetThreadsEvent?.Invoke( pRequest );
                        break;

                    case "scopes":
                        GetScopesEvent?.Invoke( pRequest, (int)pArgs.frameId );
                        break;

                    case "stackTrace":
                        GetStackTraceEvent?.Invoke( pRequest );
                        break;

                    case "variables":
                        _tempVariables.Clear();
                        GetVariablesEvent?.Invoke( pRequest, (int)pArgs.variablesReference, _tempVariables );
                        Send( pRequest, new VariablesResponseBody( _tempVariables ) );
                        break;

                    case "setVariable":

                        var variable = new Variable( (string)pArgs.name, (string)pArgs.value, "", (int)pArgs.variablesReference );
                    
                        SetVariableEvent?.Invoke( 
                            pRequest, variable
                        );

                        Send(
                            pRequest,
                            new SetVariableResponseBody( variable.value, variable.variablesReference )
                        );

                        break;

                    case "loadedSources":
                        GetLoadedSourcesEvent?.Invoke( pRequest );
                        break;

                    case "source":
                        GetSourceEvent?.Invoke( pRequest );
                        break;

                    case "evaluate":

                        string resultEval = "";
                        EvaluateEvent?.Invoke(
                             pRequest, (int)pArgs.frameId, (string)pArgs.context, (string)pArgs.expression, (bool)(pArgs.format?.hex ?? false),
                             ref resultEval
                        );

                        Send(
                            pRequest, 
                            new EvaluateResponseBody(
                                resultEval
                            )
	            		);

                        break;

                    case "completions":
                        GetCompletionsEvent?.Invoke( 
                            pRequest, (int)pArgs.frameId, (string)pArgs.text, (int)pArgs.column, (int)pArgs.line 
                        );
                        break;

//                    case "runInTerminal":
//                        OnRunInTerminal?.Invoke( pRequest );
//                        break;

//                    case "setBreakpoints":
//                        SetBreakpoints( pResponse, pArgs );
//                        break;

//                    case "setFunctionBreakpoints":
//                        SetFunctionBreakpoints( pResponse, pArgs );
//                        break;

//                    case "setExceptionBreakpoints":
//                        SetExceptionBreakpoints( pResponse, pArgs );
//                        break;

                    default:
                        Log.Write( 
                            Log.Severity.Error,
                            pMessage: string.Format( "vscode: request not handled: '{0}' [{1}]", pCommand, Format.Encode(pRequest.arguments.ToString()) )
                        );

                        break;
                }
            }
            catch( Exception e )
            {
                Log.Write(  
                    Log.Severity.Error,
                    $"vscode: error during request '{Format.Encode( pRequest.arguments.ToString() )}' [{pCommand}] (exception: {e.Message})\n{e}"
                );

                Send( new Response( pRequest, pErrorMessage: e.Message ) );
            }
        }


        // send response to request
        public void Send( Request pRequest, ResponseBody pResponse = null, string pErrorMessage = null )
        {
            var message = new Response( pRequest, pResponse, pErrorMessage );

            Log.Write( Log.Severity.Debug, "vscode: (out) response " +
                        pRequest.command
                        + (pResponse == null ? "" : " response:" + pResponse)
                        + (pErrorMessage == null ? "" : " error:'" + pErrorMessage + "'")
                     );

            pRequest.responded = true;
            Send( message );
        }

        // send event
        public void Send(Event pEvent)
        {
            Log.Write( Log.Severity.Debug, "vscode: (out) event " + pEvent.eventType );

            Send((ProtocolMessage)pEvent);
        }

        int _sequenceNumber = 1;
        void Send( ProtocolMessage pMessage)
        {
            pMessage.seq = _sequenceNumber++;

            var data = ConvertToBytes( pMessage );
            try
            {
                _output.Write( data, 0, data.Length );
                _output.Flush();
            }
            catch
            {
                // ignore
                // Log.Write( "Send error " + e );
            }
        }

        static byte[] ConvertToBytes( ProtocolMessage pMessage )
        {
            var asJson = JsonConvert.SerializeObject( pMessage );
            var jsonBytes = Encoding.UTF8.GetBytes(asJson);

            var header = $"Content-Length: {jsonBytes.Length}\r\n\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);

            Log.Write( Log.Severity.Verbose, "vscode: (out) [" + asJson + "]" );

            var data = new byte[headerBytes.Length + jsonBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, data, 0, headerBytes.Length);
            Buffer.BlockCopy(jsonBytes, 0, data, headerBytes.Length, jsonBytes.Length);

            return data;
        }
    }
}
