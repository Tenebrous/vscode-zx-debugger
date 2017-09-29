using VSCode;

namespace ZXDebug
{
    /// <summary>
    /// Handles custom requests from VSCode
    /// </summary>
    class CustomRequests
    {
        Connection _vscode;

        public delegate void GetDisassemblyForSourceHandler( Request pRequest, string pFile, int pLine );
        public event GetDisassemblyForSourceHandler GetDisassemblyForSourceEvent;

        public delegate void GetSourceForDisassemblyHandler( Request pRequest, string pFile, int pLine );
        public event GetSourceForDisassemblyHandler GetSourceForDisassemblyEvent;

        public CustomRequests( Connection pVSCode )
        {
            _vscode = pVSCode;

            _vscode.CustomRequestEvent += VSCode_CustomRequest;
        }

        void VSCode_CustomRequest( Request pRequest )
        {
            switch( pRequest.command )
            {
                case "getDisassemblyForSource":
                    GetDisassemblyForSourceEvent?.Invoke( pRequest, (string)pRequest.arguments.file, (int)pRequest.arguments.line );
                    break;

                case "getSourceForDisassembly":
                    GetSourceForDisassemblyEvent?.Invoke( pRequest, (string)pRequest.arguments.file, (int)pRequest.arguments.line );
                    break;
            }
        }
    }


    public class GetDisassemblyForSourceResponseBody : ResponseBody
    {
        public string file;
        public int startLine;
        public int endLine;

        public GetDisassemblyForSourceResponseBody( string pFile, int pStartLine, int pEndLine )
        {
            file = pFile;
            startLine = pStartLine;
            endLine = pEndLine;
        }
    }
}
