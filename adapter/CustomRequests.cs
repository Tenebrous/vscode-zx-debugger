using VSCode;

namespace ZXDebug
{
    /// <summary>
    /// Handles custom requests from VSCode
    /// </summary>
    class CustomRequests
    {
        public delegate void GetDefinitionHandler( Request request, string file, int line, string text );
        public event GetDefinitionHandler GetDefinitionEvent;

        public delegate void SetNextStatementHandler( Request request, string file, int line );
        public event SetNextStatementHandler SetNextStatementEvent;

        public CustomRequests( Connection vscode )
        {
            vscode.CustomRequestEvent += VSCode_CustomRequest;
        }

        void VSCode_CustomRequest( Request request )
        {
            switch( request.command )
            {
                case "getDefinition":
                    GetDefinitionEvent?.Invoke( 
                        request, 
                        (string)request.arguments.file, 
                        (int)request.arguments.line,
                        (string)request.arguments.text
                    );
                    break;

                case "setNextStatement":
                    SetNextStatementEvent?.Invoke( 
                        request, 
                        (string)request.arguments.file, 
                        (int)request.arguments.line 
                    );
                    break;
            }
        }
    }
}
