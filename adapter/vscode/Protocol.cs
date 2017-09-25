using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace VSCode
{
    public class ProtocolMessage
    {
        public int seq;
        public string type { get; }

        public ProtocolMessage( string typ )
        {
            type = typ;
        }

        public ProtocolMessage( string typ, int sq )
        {
            type = typ;
            seq = sq;
        }
    }

    public class Request : ProtocolMessage
    {
        [JsonIgnore]
        public bool responded;

        public string command;
        public dynamic arguments;

        public Request( int id, string cmd, dynamic arg ) : base( "request", id )
        {
            command = cmd;
            arguments = arg;
        }
    }

    /*
	 * subclasses of ResponseBody are serialized as the body of a response.
	 * Don't change their instance variables since that will break the debug protocol.
	 */
    public class ResponseBody
    {
        // empty
    }

    public class Response : ProtocolMessage
    {
        public bool success { get; private set; }
        public string message { get; private set; }
        public int request_seq { get; }
        public string command { get; }
        public ResponseBody body { get; private set; }

        public Response( Request pRequest, ResponseBody pResponse = null, string pErrorMessage = null ) : base( "response" )
        {
            success = true;
            request_seq = pRequest.seq;
            command = pRequest.command;

            if( pResponse != null )
                body = pResponse;

            if( pErrorMessage == null )
                return;

            success = false;
            message = pErrorMessage;
        }
    }

    public class Event : ProtocolMessage
    {
        [JsonProperty( PropertyName = "event" )]
        public string eventType { get; }
        public dynamic body { get; }

        public Event( string type, dynamic bdy = null ) : base( "event" )
        {
            eventType = type;
            body = bdy;
        }
    }


    public class Message
    {
        public int id { get; }
        public string format { get; }
        public dynamic variables { get; }
        public dynamic showUser { get; }
        public dynamic sendTelemetry { get; }

        public Message( int id, string format, dynamic variables = null, bool user = true, bool telemetry = false )
        {
            this.id = id;
            this.format = format;
            this.variables = variables;
            this.showUser = user;
            this.sendTelemetry = telemetry;
        }
    }

    public class StackFrame
    {
        public int id;
        public string name;
        public Source source;
        public int line;
        public int column;
        public string presentationHint;

        public StackFrame( int id, string name, Source source, int line, int column, string presentationHint = null )
        {
            this.id = id;
            this.name = name;
            this.source = source;
            this.line = line;
            this.column = column;
            this.presentationHint = presentationHint;
        }
    }

    public class Scope
    {
        public string name { get; }
        public int variablesReference { get; }
        public bool expensive { get; }

        public Scope( string name, int variablesReference, bool expensive = false )
        {
            this.name = name;
            this.variablesReference = variablesReference;
            this.expensive = expensive;
        }
    }

    public class Variable
    {
        public string name { get; }
        public string value { get; }
        public string type { get; }
        public int variablesReference { get; }
        public VariablePresentationHint presentationHint;

        public Variable( string name, string value, string type = null, int variablesReference = 0, VariablePresentationHint presentationHint = null )
        {
            this.name = name;
            this.type = type;
            this.value = value;
            this.variablesReference = variablesReference;
            this.presentationHint = presentationHint;
        }
    }

    public class VariablePresentationHint
    {
        public string kind { get; }
        public string[] attributes { get; }
        public string visibility { get; }

        public VariablePresentationHint( string kind, string[] attributes = null, string visibility = null )
        {
            this.kind = kind;
            this.attributes = attributes;
            this.visibility = visibility;
        }
    }

    public class Thread
    {
        public int id { get; }
        public string name { get; }

        public Thread( int id, string name )
        {
            this.id = id;
            if( string.IsNullOrEmpty( name ) )
            {
                this.name = $"Thread #{id}";
            }
            else
            {
                this.name = name;
            }
        }
    }

    public class Source
    {
        public enum SourcePresentationHintEnum
        {
            normal,
            emphasize,
            deemphasize
        }

        public string name { get; }
        public string path { get; }
        public int sourceReference { get; }

        [JsonConverter( typeof( StringEnumConverter ) )]
        public SourcePresentationHintEnum presentationHint { get; }

        public Source( string name = null, string path = null, int sourceReference = 0, SourcePresentationHintEnum presentationHint = SourcePresentationHintEnum.normal )
        {
            this.name = name;
            this.path = path;
            this.sourceReference = sourceReference;
            this.presentationHint = presentationHint;
        }
    }

    public class Breakpoint
    {
        public int id { get; }
        public bool verified { get; }
        public string message { get; }
        public Source source { get; }
        public int line { get; }
        public int column { get; }
        public int endLine { get; }
        public int endColumn { get; }

        public Breakpoint( int id, bool verified, string message, Source source, int line, int column, int endLine, int endColumn )
        {
            this.id = id;
            this.verified = verified;
            this.message = message;
            this.source = source;
            this.line = line;
            this.column = column;
            this.endLine = endLine;
            this.endColumn = endColumn;
        }
    }

    // ---- Events -------------------------------------------------------------------------

    public class InitializedEvent : Event
    {
        public InitializedEvent() : base( "initialized" ) { }
    }

    public class StoppedEvent : Event
    {
        public StoppedEvent( int tid, string reasn, string txt = null )
            : base( "stopped", new
            {
                threadId = tid,
                reason = reasn,
                text = txt
            } )
        { }
    }

    public class ContinuedEvent : Event
    {
        public ContinuedEvent( bool all ) : base( "continued", new { allThreadsContinued = all } ) { }
    }

    public class ExitedEvent : Event
    {
        public ExitedEvent( int exCode ) : base( "exited", new { exitCode = exCode } ) { }
    }

    public class TerminatedEvent : Event
    {
        public TerminatedEvent() : base( "terminated" ) { }
    }

    public class BreakpointEvent : Event
    {
        public BreakpointEvent() : base( "breakpoint" ) { }
    }

    public class ThreadEvent : Event
    {
        public ThreadEvent( string reasn, int tid ) :
            base( "thread",
            new
            {
                reason = reasn,
                threadId = tid
            } )
        {

        }
    }

    public class OutputEvent : Event
    {
        public enum OutputEventType
        {
            console,
            stdout,
            stderr,
            telemetry
        }

        public OutputEvent( OutputEventType cat, string outpt )
            : base( "output", new
            {
                category = cat.ToString(),
                output = outpt
            } )
        { }
    }

    public class LoadedSourceEvent : Event
    {
        public LoadedSourceEvent( string pReason, Source pSource ) : base( "loadedSource", new { reason = pReason, source = pSource } ) { }
    }

    // ---- Response -------------------------------------------------------------------------

    public class Capabilities : ResponseBody
    {
        /** The debug adapter supports the configurationDoneRequest. */
        public bool supportsConfigurationDoneRequest;

        /** The debug adapter supports function breakpoints. */
        public bool supportsFunctionBreakpoints;

        /** The debug adapter supports conditional breakpoints. */
        public bool supportsConditionalBreakpoints;

        /** The debug adapter supports breakpoints that break execution after a specified number of hits. */
        public bool supportsHitConditionalBreakpoints;

        /** The debug adapter supports a (side effect free) evaluate request for data hovers. */
        public bool supportsEvaluateForHovers;

        /** Available filters or options for the setExceptionBreakpoints request. */
        public dynamic[] exceptionBreakpointFilters;

        /** The debug adapter supports stepping back via the stepBack and reverseContinue requests. */
        public bool supportsStepBack;

        /** The debug adapter supports setting a variable to a value. */
        public bool supportsSetVariable;

        /** The debug adapter supports restarting a frame. */
        public bool supportsRestartFrame;

        /** The debug adapter supports the gotoTargetsRequest. */
        public bool supportsGotoTargetsRequest;

        /** The debug adapter supports the stepInTargetsRequest. */
        public bool supportsStepInTargetsRequest;

        /** The debug adapter supports the completionsRequest. */
        public bool supportsCompletionsRequest;

        /** The debug adapter supports the modules request. */
        public bool supportsModulesRequest;

        /** The set of additional module information exposed by the debug adapter. */
        public dynamic[] additionalModuleColumns;

        /** Checksum algorithms supported by the debug adapter. */
        public dynamic[] supportedChecksumAlgorithms;

        /** The debug adapter supports the RestartRequest. In this case a client should not implement 'restart' by terminating and relaunching the adapter but by calling the RestartRequest. */
        public bool supportsRestartRequest;

        /** The debug adapter supports 'exceptionOptions' on the setExceptionBreakpoints request. */
        public bool supportsExceptionOptions;

        /** The debug adapter supports a 'format' attribute on the stackTraceRequest, variablesRequest, and evaluateRequest. */
        public bool supportsValueFormattingOptions;

        /** The debug adapter supports the exceptionInfo request. */
        public bool supportsExceptionInfoRequest;

        /** The debug adapter supports the 'terminateDebuggee' attribute on the 'disconnect' request. */
        public bool supportTerminateDebuggee;

        /** The debug adapter supports the delayed loading of parts of the stack, which requires that both the 'startFrame' and 'levels' arguments and the 'totalFrames' result of the 'StackTrace' request are supported. */
        public bool supportsDelayedStackTraceLoading;

        /** The debug adapter supports the 'loadedSources' request. */
        public bool supportsLoadedSourcesRequest;
    }

    public class ErrorResponseBody : ResponseBody
    {

        public Message error { get; }

        public ErrorResponseBody( Message error )
        {
            this.error = error;
        }
    }

    public class StackTraceResponseBody : ResponseBody
    {
        public StackFrame[] stackFrames { get; }

        public StackTraceResponseBody( List<StackFrame> frames = null )
        {
            stackFrames = frames?.ToArray<StackFrame>() ?? new StackFrame[0];
        }
    }

    public class ScopesResponseBody : ResponseBody
    {
        public Scope[] scopes { get; }

        public ScopesResponseBody( List<Scope> scps = null )
        {
            if( scps == null )
                scopes = new Scope[0];
            else
                scopes = scps.ToArray<Scope>();
        }
    }

    public class LoadedSourcesResponseBody : ResponseBody
    {
        public Source[] sources { get; }

        public LoadedSourcesResponseBody( List<Source> sources = null )
        {
            if( sources == null )
                this.sources = new Source[0];
            else
                this.sources = sources.ToArray<Source>();
        }
    }

    public class VariablesResponseBody : ResponseBody
    {
        public Variable[] variables { get; }

        public VariablesResponseBody( List<Variable> vars = null )
        {
            if( vars == null )
                variables = new Variable[0];
            else
                variables = vars.ToArray<Variable>();
        }
    }

    public class SetVariableResponseBody : ResponseBody
    {
        public string value { get; }
        public int variablesReference { get; }
        public SetVariableResponseBody( string value, int reference )
        {
            this.value = value;;
            this.variablesReference = reference;
        }
    }

    public class ThreadsResponseBody : ResponseBody
    {
        public Thread[] threads { get; }

        public ThreadsResponseBody( List<Thread> vars = null )
        {
            if( vars == null )
                threads = new Thread[0];
            else
                threads = vars.ToArray<Thread>();
        }
    }

    public class SourceResponseBody : ResponseBody
    {
        public string content { get; }
        public string mimeType { get; }

        public SourceResponseBody( string content, string mimeType = null )
        {
            this.content = content;
            this.mimeType = mimeType;
        }
    }

    public class EvaluateResponseBody : ResponseBody
    {
        public string result { get; }
        public int variablesReference { get; }

        public EvaluateResponseBody( string value, int reff = 0 )
        {
            result = value;
            variablesReference = reff;
        }
    }

    public class SetBreakpointsResponseBody : ResponseBody
    {
        public Breakpoint[] breakpoints { get; }

        public SetBreakpointsResponseBody( List<Breakpoint> bpts = null )
        {
            if( bpts == null )
                breakpoints = new Breakpoint[0];
            else
                breakpoints = bpts.ToArray<Breakpoint>();
        }
    }

}
