using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using ZXDebug;
using Convert = ZXDebug.Convert;

namespace VSCode
{
    public class Connection : Loggable
    {
        public delegate void EventHandler( Request request );

        public delegate void InitialiseHandler( Request request, Capabilities result );
        public event InitialiseHandler  InitializeEvent;

        public delegate void LaunchHandler( Request request, string json );
        public event LaunchHandler LaunchEvent;

        public delegate void AttachHandler( Request request, string json );
        public event AttachHandler AttachEvent;

        public event EventHandler DisconnectEvent;
        public event EventHandler PauseEvent;
        public event EventHandler ContinueEvent;
        public event EventHandler StepOverEvent;
        public event EventHandler StepInEvent;
        public event EventHandler StepOutEvent;
        public event EventHandler GetStackTraceEvent;

        public delegate void GetVariablesHandler( Request request, int reference, List<Variable> results );
        public event GetVariablesHandler GetVariablesEvent;

        public delegate void SetVariablesHandler( Request request, Variable variable );
        public event SetVariablesHandler SetVariableEvent;

        public event EventHandler GetThreadsEvent;

        public delegate void GetCompletionsHandler( Request request, int frameId, int line, int column, string text );
        public event GetCompletionsHandler GetCompletionsEvent;

        public delegate void GetScopesHandler( Request request, int frameId );
        public event GetScopesHandler GetScopesEvent;

        public event EventHandler GetSourceEvent;
        public event EventHandler GetLoadedSourcesEvent;
        public event EventHandler ConfigurationDoneEvent;
        public event EventHandler SetBreakpointsEvent;
        
        public delegate void EvaluateHandler( Request request, int frameId, string context, string expression, bool wantHex, ref string result );
        public event EvaluateHandler EvaluateEvent;

        public delegate void CustomRequestHandler( Request request );
        public event CustomRequestHandler CustomRequestEvent;

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


        bool _needRefresh;
        public bool NeedRefresh
        {
            get { return _needRefresh; }
            set { _needRefresh = value; }
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

                var size = System.Convert.ToInt32( match.Groups[1].ToString() );
                var end = match.Index + match.Length;

                if( data.Length < end + size )
                    break;

                var message = data.Substring( end, size );

                _inputBuffer.Remove( 0, end + size );

                ProcessMessage( message );
            }
        }

        void ProcessMessage( string msg )
        {
            Log( Logging.Severity.Verbose, "(in)  " + msg );

            Request request;

            try
            {
                request = JsonConvert.DeserializeObject<Request>( msg );
            }
            catch( Exception e )
            {
                Log( Logging.Severity.Error, "The following message caused an error: [" + msg.Replace( "\r", "\\r" ).Replace( "\n", "\\n" ) + "]" );
                Log( Logging.Severity.Error, e.ToString() );
                return;
            }

            if( request == null )
                return;

            Log( Logging.Severity.Debug, "(in)  " + request.type + " " + request.command );

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

        public void Stopped( int threadId, string reason, string description )
        {
            Send( new StoppedEvent( threadId, reason, description ) );
        }

        public void Continued( bool allThreads )
        {
            Send( new ContinuedEvent( allThreads ) );
        }


        static List<Variable> _tempVariables = new List<Variable>();

        void HandleMessage( string msg, dynamic args, Request request )
        {
            Log( Logging.Severity.Verbose, "(in) [" + msg + "]" );

            args = args ?? new { };

            try
            {

                switch( msg )
                {
                    case "initialize":
                        var cap = new Capabilities();
                        InitializeEvent?.Invoke( request, cap );
                        Send( request, cap );
                        Initialized();
                        break;

                    case "configurationDone":
                        ConfigurationDoneEvent?.Invoke(request);
                        break;

                    case "launch":
                        LaunchEvent?.Invoke( request, JsonConvert.SerializeObject( args ) );
                        break;

                    case "attach":
                        AttachEvent?.Invoke( request, JsonConvert.SerializeObject( args ) );
                        break;

                    case "disconnect":
                        DisconnectEvent?.Invoke( request );
                        break;

                    case "next":
                        StepOverEvent?.Invoke( request );
                        break;

                    case "continue":
                        ContinueEvent?.Invoke( request );
                        break;

                    case "stepIn":
                        StepInEvent?.Invoke( request );
                        break;

                    case "stepOut":
                        StepOutEvent?.Invoke( request );
                        break;

                    case "pause":
                        PauseEvent?.Invoke( request );
                        break;

                    case "threads":
                        GetThreadsEvent?.Invoke( request );
                        break;

                    case "scopes":
                        GetScopesEvent?.Invoke( request, (int)args.frameId );
                        break;

                    case "stackTrace":
                        GetStackTraceEvent?.Invoke( request );
                        break;

                    case "variables":
                        _tempVariables.Clear();
                        GetVariablesEvent?.Invoke( request, (int)args.variablesReference, _tempVariables );
                        Send( request, new VariablesResponseBody( _tempVariables ) );
                        break;

                    case "setVariable":

                        var variable = new Variable( (string)args.name, (string)args.value, "", (int)args.variablesReference );
                    
                        SetVariableEvent?.Invoke( 
                            request, variable
                        );

                        Send(
                            request,
                            new SetVariableResponseBody( variable.value, variable.variablesReference )
                        );

                        break;

                    case "loadedSources":
                        GetLoadedSourcesEvent?.Invoke( request );
                        break;

                    case "source":
                        GetSourceEvent?.Invoke( request );
                        break;

                    case "evaluate":

                        string resultEval = "";
                        EvaluateEvent?.Invoke(
                             request, (int)args.frameId, (string)args.context, (string)args.expression, (bool)(args.format?.hex ?? false),
                             ref resultEval
                        );

                        Send(
                            request, 
                            new EvaluateResponseBody(
                                resultEval
                            )
                        );

                        break;

                    case "completions":
                        GetCompletionsEvent?.Invoke( 
                            request, (int)args.frameId, (int)args.line, (int)args.column, (string )args.text
                        );
                        break;


                    case "setBreakpoints":
                        SetBreakpointsEvent?.Invoke( request );
                        break;


//                    case "runInTerminal":
//                        OnRunInTerminal?.Invoke( pRequest );
//                        break;


//                    case "setFunctionBreakpoints":
//                        SetFunctionBreakpoints( pResponse, pArgs );
//                        break;

//                    case "setExceptionBreakpoints":
//                        SetExceptionBreakpoints( pResponse, pArgs );
//                        break;

                    default:

                        CustomRequestEvent?.Invoke( request );

                        if( !request.responded )
                            Log( 
                                Logging.Severity.Error,
                                string.Format( "Request not handled: '{0}' [{1}]", msg, Convert.Encode(request.arguments.ToString()) )
                            );

                        break;
                }
            }
            catch( Exception e )
            {
                Log(  
                    Logging.Severity.Error,
                    $"Error during request '{Convert.Encode( request.arguments.ToString() )}' [{msg}] (exception: {e.Message})\n{e}"
                );

                Send( new Response( request, errorMsg: e.Message ) );
            }
        }


        // send response to request
        public void Send( Request request, ResponseBody response = null, string errorMsg = null )
        {
            var message = new Response( request, response, errorMsg );

            Log( Logging.Severity.Debug, "(out) response " +
                        request.command
                        + (response == null ? "" : " response:" + response)
                        + (errorMsg == null ? "" : " error:'" + errorMsg + "'")
                     );

            request.responded = true;
            Send( message );
        }

        // send event
        public void Send(Event evt)
        {
            Log( Logging.Severity.Debug, "(out) event " + evt.eventType );

            Send((ProtocolMessage)evt);
        }

        int _sequenceNumber = 1;
        void Send( ProtocolMessage msg)
        {
            msg.seq = _sequenceNumber++;

            var data = ConvertToBytes( msg );
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

        byte[] ConvertToBytes( ProtocolMessage msg )
        {
            var asJson = JsonConvert.SerializeObject( msg );
            var jsonBytes = Encoding.UTF8.GetBytes(asJson);

            var header = $"Content-Length: {jsonBytes.Length}\r\n\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);

            Log( Logging.Severity.Verbose, "(out) [" + asJson + "]" );

            var data = new byte[headerBytes.Length + jsonBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, data, 0, headerBytes.Length);
            Buffer.BlockCopy(jsonBytes, 0, data, headerBytes.Length, jsonBytes.Length);

            return data;
        }

        public void Refresh()
        {
            Continued( true );
            Stopped( 1, "step", "step" );
        }

        public override string LogPrefix
        {
            get { return "VSCodeConnection"; }
        }
    }
}
