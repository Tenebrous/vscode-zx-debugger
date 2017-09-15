using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace VSCode
{
    public class Connection
    {
        public Action<Request> OnInitialize;
        public Action<Request> OnLaunch;
        public Action<Request> OnAttach;
        public Action<Request> OnDisconnect;
        public Action<Request> OnPause;
        public Action<Request> OnContinue;
        public Action<Request> OnNext;
        public Action<Request> OnStepIn;
        public Action<Request> OnStepOut;
        public Action<Request> OnGetStackTrace;
        public Action<Request> OnGetVariables;
        public Action<Request> OnSetVariable;
        public Action<Request> OnGetThreads;
        public Action<Request> OnGetCompletions;
        public Action<Request> OnGetScopes;
        public Action<Request> OnGetSource;
        public Action<Request> OnGetLoadedSources;
        public Action<Request> OnConfigurationDone;
        public Action<Request> OnEvaluate;

        const string TwoCRLF = "\r\n\r\n";
        static readonly Regex ContentLengthMatcher = new Regex(@"Content-Length: (\d+)");
        static readonly Encoding Encoding = System.Text.Encoding.UTF8;

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
        
        public void SourceAdded( Source pSource )
        {
            Send( new LoadedSourceEvent( "new", pSource ) );
        }

        public void SourceRemoved( Source pSource )
        {
            Send( new LoadedSourceEvent( "removed", pSource ) );
        }

        public void SourceChanged( Source pSource )
        {
            Send( new LoadedSourceEvent( "changed", pSource )  );
        }

        // commands/events from vscode

        void HandleMessage( string pCommand, dynamic pArgs, Request pRequest )
        {
            Log.Write( Log.Severity.Verbose, "vscode: (in) [" + pCommand + "]" );

            pArgs = pArgs ?? new { };

            try
            {

                switch( pCommand )
                {
                    case "initialize":
                        OnInitialize?.Invoke(pRequest);
                        break;

                    //if( pArgs.linesStartAt1 != null )
                    //{
                    //    _clientLinesStartAt1 = (bool) pArgs.linesStartAt1;
                    //}
                    //var pathFormat = (string) pArgs.pathFormat;
                    //if( pathFormat != null )
                    //{
                    //    switch( pathFormat )
                    //    {
                    //        case "uri":
                    //            _clientPathsAreURI = true;
                    //            break;
                    //        case "path":
                    //            _clientPathsAreURI = false;
                    //            break;
                    //        default:
                    //            SendErrorResponse( pResponse, 1015, "initialize: bad value '{_format}' for pathFormat", new { _format = pathFormat } );
                    //            return;
                    //    }
                    //}
                    // 
                    //Initialize( pResponse, pArgs );

                    case "configurationDone":
                        OnConfigurationDone?.Invoke(pRequest);
                        break;

                    case "launch":
                        OnLaunch?.Invoke( pRequest );
                        break;

                    case "attach":
                        OnAttach?.Invoke( pRequest );
                        break;

                    case "disconnect":
                        OnDisconnect?.Invoke( pRequest );
                        break;

                    case "next":
                        OnNext?.Invoke( pRequest );
                        break;

                    case "continue":
                        OnContinue?.Invoke( pRequest );
                        break;

                    case "stepIn":
                        OnStepIn?.Invoke( pRequest );
                        break;

                    case "stepOut":
                        OnStepOut?.Invoke( pRequest );
                        break;

                    case "pause":
                        OnPause?.Invoke( pRequest );
                        break;

                    case "threads":
                        OnGetThreads?.Invoke(pRequest);
                        break;

                    case "scopes":
                        OnGetScopes?.Invoke(pRequest);
                        break;

                    case "stackTrace":
                        OnGetStackTrace?.Invoke(pRequest);
                        break;

                    case "variables":
                        OnGetVariables?.Invoke( pRequest );
                        break;

                    case "setVariable":
                        OnSetVariable?.Invoke( pRequest );
                        break;

                    case "loadedSources":
                        OnGetLoadedSources?.Invoke( pRequest );
                        break;

                    case "source":
                        OnGetSource?.Invoke( pRequest );
                        break;

                    case "evaluate":
                        OnEvaluate?.Invoke( pRequest );
                        break;

                    case "completions":
                        OnGetCompletions?.Invoke( pRequest );
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
                            string.Format( "VSCode request not handled: {0}", pCommand )
                        );

                        break;
                }
            }
            catch( Exception e )
            {
                Log.Write(
                    Log.Severity.Error,
                    string.Format( "Error during request '{0}' (exception: {1})\n{2}", pCommand, e.Message, e )
                );

                Send( new Response( pRequest, pErrorMessage: e.Message ) );
            }
        }

        //

        public bool Read()
        {
            if (_inputReader.HasData)
            {
                var data = Encoding.ASCII.GetString(_inputReader.GetData());
                _rawData.Append(data);
                ProcessData();

                return true;
            }

            return false;
        }

        void ProcessData()
        {
            var data = _rawData.ToString();
            _rawData.Clear();

            while (true)
            {
                // find end of message
                var twocrlf = data.IndexOf(TwoCRLF, StringComparison.Ordinal);

                if (twocrlf == -1)
                    break;

                // find size text
                var match = ContentLengthMatcher.Match(data);

                if (!match.Success || match.Groups.Count != 2)
                    break;

                var size = Convert.ToInt32(match.Groups[1].ToString());

                if (data.Length < twocrlf + size + TwoCRLF.Length)
                    break;

                var message = data.Substring(twocrlf + TwoCRLF.Length, size);
                ProcessMessage(message);

                data = data.Substring(twocrlf + TwoCRLF.Length + size);
            }

            _rawData.Append( data );
        }

        void ProcessMessage(string pMessage)
        {
            Log.Write( Log.Severity.Verbose, "vscode: (in) " + pMessage );

            var request = JsonConvert.DeserializeObject<Request>(pMessage);

            if (request != null && request.type == "request")
            {
                _currentRequest = request;

                HandleMessage(request.command, request.arguments, request);

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
            var jsonBytes = Encoding.GetBytes(asJson);

            var header = string.Format( "Content-Length: {0}{1}", jsonBytes.Length, TwoCRLF );
            var headerBytes = Encoding.GetBytes(header);

            Log.Write( Log.Severity.Verbose, "vscode: (out) [" + asJson + "]" );

            var data = new byte[headerBytes.Length + jsonBytes.Length];
            System.Buffer.BlockCopy(headerBytes, 0, data, 0, headerBytes.Length);
            System.Buffer.BlockCopy(jsonBytes, 0, data, headerBytes.Length, jsonBytes.Length);

            return data;
        }


        public Request CurrentRequest
        {
            get { return _currentRequest; }
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
