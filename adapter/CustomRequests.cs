using VSCode;

namespace ZXDebug
{
    /// <summary>
    /// Handles custom requests from VSCode
    /// </summary>
    class CustomRequests
    {
        public delegate void GetDefinitionHandler( Request request, string file, int line, int column, string text, string symbol );
        public event GetDefinitionHandler GetDefinitionEvent;

        public delegate void GetHoverHandler( Request request, string file, int line, int column, string text, string symbol );
        public event GetHoverHandler GetHoverEvent;

        public delegate void SetNextStatementHandler( Request request, string file, int line );
        public event SetNextStatementHandler SetNextStatementEvent;

        public delegate void WatchMemoryHandler( Request request, string id, string address, string length );
        public event WatchMemoryHandler WatchMemoryEvent;

        public CustomRequests( VSCode.Connection vscode )
        {
            vscode.CustomRequestEvent += VSCode_CustomRequest;
        }

        void VSCode_CustomRequest( Request request )
        {
            Logging.Write( Logging.Severity.Message, request.arguments.ToString() );

            switch( request.command )
            {
                case "getDefinition":
                    GetDefinitionEvent?.Invoke(
                        request,
                        (string) request.arguments.file,
                        (int)    request.arguments.line,
                        (int)    request.arguments.column,
                        (string) request.arguments.text,
                        (string) request.arguments.symbol
                    );
                    break;

                case "getHover":
                    GetHoverEvent?.Invoke(
                        request,
                        (string) request.arguments.file,
                        (int)    request.arguments.line,
                        (int)    request.arguments.column,
                        (string) request.arguments.text,
                        (string) request.arguments.symbol
                    );
                    break;

                case "setNextStatement":
                    SetNextStatementEvent?.Invoke( 
                        request, 
                        (string) request.arguments.file, 
                        (int)    request.arguments.line 
                    );
                    break;

                case "watchMemory":
                    WatchMemoryEvent?.Invoke(
                        request,
                        (string) request.arguments.id,
                        (string) request.arguments.address,
                        (string) request.arguments.length
                    );
                    break;
            }
        }
    }

    public class HoverResponseBody : ResponseBody
    {
        public string result;

        public HoverResponseBody( string result )
        {
            this.result = result;
        }
    }    
}
