using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using ZDebug;

namespace VSCodeDebug
{
    public class ConnectionVSCode
    {
        public Action<Request> OnInitialize;
        public Action<Request> OnPause;
        public Action<Request> OnContinue;

        protected const int BufferSize = 4096;
        protected const string TwoCRLF = "\r\n\r\n";
        protected static readonly Regex ContentLengthMatcher = new Regex(@"Content-Length: (\d+)");
        protected static readonly Encoding Encoding = System.Text.Encoding.UTF8;

        Stream _input;
        Reader _inputReader;
        Stream _output;

        StringBuilder _rawData = new StringBuilder();

        public ConnectionVSCode()
        {
            _input = Console.OpenStandardInput();
            _inputReader = new Reader( _input );

            _output = Console.OpenStandardOutput();
        }

        public void Paused()
        {
            
        }

        public void Continued()
        {
            
        }

        public bool Read()
        {
            if( _inputReader.HasData )
            {
                var data = Encoding.ASCII.GetString( _inputReader.GetData() );
                _rawData.Append( data );
                //ZMain.Log( "vscode: -> [" + data + "]" );    
                ProcessData();
            }

            return true;
        }

        void ProcessData()
        {
            var data = _rawData.ToString();

            while( true )
            {
                // find end of message
                var twocrlf = data.IndexOf( TwoCRLF, StringComparison.Ordinal );

                if( twocrlf == -1 )
                    break;

                // find size text
                var match = ContentLengthMatcher.Match( data );

                if( !match.Success || match.Groups.Count != 2 )
                    break;

                var size = Convert.ToInt32( match.Groups[1].ToString() );

                if ( data.Length < twocrlf + size + TwoCRLF.Length )
                    break;

                var message = data.Substring( twocrlf + TwoCRLF.Length, size );
                ProcessMessage(message);

                data = data.Substring(twocrlf + TwoCRLF.Length + size);
            }

            _rawData.Append( data );
        }

        void ProcessMessage( string pMessage )
        {
            var request = JsonConvert.DeserializeObject<Request>( pMessage );

            if (request != null && request.type == "request")
            {
                HandleMessage( request.command, request.arguments, request );
            }
        }

        void HandleMessage( string pCommand, dynamic pArgs, Request pRequest )
        {
            ZMain.Log( "vscode: <- [" + pCommand + "]" );

            pArgs = pArgs ?? new { };

            try
            {

                switch( pCommand )
                {
                    case "initialize":

                        if( OnInitialize != null )
                            OnInitialize(pRequest);

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
                        break;

                    case "launch":
//                        Launch( pResponse, pArgs );
                        break;

                    case "attach":
//                        Attach( pResponse, pArgs );
                        break;

                    case "disconnect":
//                        Disconnect( pResponse, pArgs );
                        break;

                    case "next":
//                        Next( pResponse, pArgs );
                        break;

                    case "continue":

                        if( OnContinue != null )
                            OnContinue(pRequest);

                        break;

                    case "stepIn":
//                        StepIn( pResponse, pArgs );
                        break;

                    case "stepOut":
//                        StepOut( pResponse, pArgs );
                        break;

                    case "pause":

                        if ( OnPause != null )
                            OnPause(pRequest);

                        break;

                    case "stackTrace":
//                        StackTrace( pResponse, pArgs );
                        break;

                    case "scopes":
//                        Scopes( pResponse, pArgs );
                        break;

                    case "variables":
//                        Variables( pResponse, pArgs );
                        break;

                    case "source":
//                        Source( pResponse, pArgs );
                        break;

                    case "threads":
//                        Threads( pResponse, pArgs );
                        break;

                    case "setBreakpoints":
//                        SetBreakpoints( pResponse, pArgs );
                        break;

                    case "setFunctionBreakpoints":
//                        SetFunctionBreakpoints( pResponse, pArgs );
                        break;

                    case "setExceptionBreakpoints":
//                        SetExceptionBreakpoints( pResponse, pArgs );
                        break;

                    case "evaluate":
//                        Evaluate( pResponse, pArgs );
                        break;

                    case "configurationDone":
//                        ConfigurationDone( pResponse, pArgs );
                        break;

                    default:
//                        SendErrorResponse( pResponse, 1014, "unrecognized request: {_request}", new { _request = pCommand } );
                        break;
                }
            }
            catch( Exception e )
            {
//                SendErrorResponse( pResponse, 1104, "error while processing request '{_request}' (exception: {_exception})", new { _request = pCommand, _exception = e.Message } );
            }

            if( pCommand == "disconnect" )
            {
//                Stop();
            }
        }

        private int _sequenceNumber;
        public void Send( Request pRequest, ResponseBody pResponse )
        {
            var message = new Response( pRequest, pResponse );
            ZMain.Log("vscode: -> (response) " + pRequest.command + " " + pResponse.ToString());

            Send( message );
        }

        void Send( ProtocolMessage pMessage )
        {
            var data = ConvertToBytes( pMessage );
            try
            {
                _output.Write( data, 0, data.Length );
                _output.Flush();
            }
            catch( Exception e )
            {
                // ignore
                ZMain.Log( "Send error " + e );
            }
        }

        public void Send( Event pEvent )
        {
            pEvent.seq = _sequenceNumber++;
            ZMain.Log("vscode: -> (event) " + pEvent.eventType );

            Send( (ProtocolMessage) pEvent );
        }

        static byte[] ConvertToBytes( ProtocolMessage pMessage )
        {
            var asJson = JsonConvert.SerializeObject( pMessage );
            var jsonBytes = Encoding.GetBytes(asJson);

            var header = string.Format( "Content-Length: {0}{1}", jsonBytes.Length, TwoCRLF );
            var headerBytes = Encoding.GetBytes(header);

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
