﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

        public delegate void EvaluateHandler( Request pRequest, int pFrameID, string pContext, string pExpression, bool bHex, ref string pResult );
        public event EvaluateHandler EvaluateEvent;

        static readonly Regex _contentLength = new Regex(@"Content-Length: (\d+)\r\n\r\n");
        static readonly Encoding _encoding = System.Text.Encoding.UTF8;

        Stream _input;
        Reader _inputReader;
        Stream _output;
        StringBuilder _rawData = new StringBuilder();

        Request _currentRequest;

        public Connection()
        {
            _input = Console.OpenStandardInput();
            _inputReader = new Reader( _input );

            _output = Console.OpenStandardOutput();
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

	                    string val = pArgs.value.ToString();
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
                             pRequest, (int)pArgs.frameId, (string)pArgs.context, (string)pArgs.expression, (bool)pArgs.format.hex,
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
                Log.Write( Log.Severity.Error, e.ToString() );

                Log.Write(  
                    Log.Severity.Error,
                    $"vscode: error during request '{Format.Encode( pRequest.arguments.ToString() )}' [{pCommand}] (exception: {e.Message})\n{e}"
                );

                Send( new Response( pRequest, pErrorMessage: e.Message ) );
            }
        }


        //

        public bool Process()
        {
            if( !_inputReader.HasData )
                return false;

            var data = Encoding.ASCII.GetString( _inputReader.GetData() );
            _rawData.Append(data);
            ProcessData();

            return true;
        }

        void ProcessData()
        {
            while( true )
            {
                var data = _rawData.ToString();

                // find size text
                var match = _contentLength.Match( data );

                if( !match.Success || match.Groups.Count != 2 )
                    break;

                var size = Convert.ToInt32( match.Groups[1].ToString() );
                var end = match.Index + match.Length;

                if( data.Length < end + size )
                    break;

                var message = data.Substring( end, size );

                _rawData.Remove( 0, end + size );

                ProcessMessage( message );
            }
        }

        void ProcessMessage(string pMessage)
        {
            Log.Write( Log.Severity.Verbose, "vscode: (in)  " + pMessage );

            Request request = null;

            try
            {
                request = JsonConvert.DeserializeObject<Request>(pMessage);
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
                _currentRequest = request;

                HandleMessage( request.command, request.arguments, request );

                if( !request.responded )
                    Send( request );

                _currentRequest = null;
            }
        }

        // send response to request
        public void Send( Request pRequest, ResponseBody pResponse = null, string pErrorMessage = null )
        {
            var message = new Response( pRequest, pResponse, pErrorMessage );

            Log.Write( Log.Severity.Debug, "vscode: (out) response " +
                        pRequest.command
                        + (pResponse == null ? "" : " response:" + pResponse.ToString())
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
            var jsonBytes = _encoding.GetBytes(asJson);

            var header = $"Content-Length: {jsonBytes.Length}\r\n\r\n";
            var headerBytes = _encoding.GetBytes(header);

            Log.Write( Log.Severity.Verbose, "vscode: (out) [" + asJson + "]" );

            var data = new byte[headerBytes.Length + jsonBytes.Length];
            System.Buffer.BlockCopy(headerBytes, 0, data, 0, headerBytes.Length);
            System.Buffer.BlockCopy(jsonBytes, 0, data, headerBytes.Length, jsonBytes.Length);

            return data;
        }
    }

    class Reader
    {
        byte[] _buffer = new byte[4096];
        MemoryStream _stream = new MemoryStream();

        public Reader( Stream pStream )
        {
            pStream.BeginRead( _buffer, 0, _buffer.Length, Callback, pStream );
        }

        void Callback( IAsyncResult pResult )
        {
            var stream = (Stream) pResult.AsyncState;
            var bytes = stream.EndRead( pResult );
            _stream.Write( _buffer, 0, bytes );

            stream.BeginRead( _buffer, 0, _buffer.Length, Callback, stream );
        }

        public bool HasData
        {
            get { return _stream.Length > 0; }
        }

        public byte[] GetData()
        {
            var result = _stream.ToArray();

            _stream = new MemoryStream();

            return result;
        }
    }
}
