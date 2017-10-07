using VSCode;

namespace ZXDebug
{
    /// <summary>
    /// Handles custom requests from VSCode
    /// </summary>
    class CustomRequests
    {
        public delegate void GetDefinitionHandler( Request pRequest, string pFile, int pLine, string pText );
        public event GetDefinitionHandler GetDefinitionEvent;

        public delegate void SetNextStatementHandler( Request pRequest, string pFile, int pLine );
        public event SetNextStatementHandler SetNextStatementEvent;

        public CustomRequests( Connection pVSCode )
        {
            pVSCode.CustomRequestEvent += VSCode_CustomRequest;
        }

        void VSCode_CustomRequest( Request pRequest )
        {
            switch( pRequest.command )
            {
                case "getDefinition":
                    GetDefinitionEvent?.Invoke( 
                        pRequest, 
                        (string)pRequest.arguments.file, 
                        (int)pRequest.arguments.line,
                        (string)pRequest.arguments.text
                    );
                    break;

                case "setNextStatement":
                    SetNextStatementEvent?.Invoke( 
                        pRequest, 
                        (string)pRequest.arguments.file, 
                        (int)pRequest.arguments.line 
                    );
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
