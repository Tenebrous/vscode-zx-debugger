/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using ZDebug;


namespace VSCodeDebug
{
    // ---- Types -------------------------------------------------------------------------

    public class Message
    {
        public int id { get; }
        public string format { get; }
        public dynamic variables { get; }
        public dynamic showUser { get; }
        public dynamic sendTelemetry { get; }

        public Message(int id, string format, dynamic variables = null, bool user = true, bool telemetry = false)
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
        public int id { get; }
        public string name { get; }
        public Source source { get; }
        public int line { get; }
        public int column { get; }
        public string presentationHint { get;  }

        public StackFrame(int id, string name, Source source, int line, int column, string presentationHint = null )
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

        public Scope(string name, int variablesReference, bool expensive = false)
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

        public Variable(string name, string value, string type = null, int variablesReference = 0, VariablePresentationHint presentationHint = null)
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

        public Thread(int id, string name)
        {
            this.id = id;
            if (name == null || name.Length == 0)
            {
                this.name = string.Format("Thread #{0}", id);
            }
            else
            {
                this.name = name;
            }
        }
    }

    public class Source
    {
        public string name { get; }
        public string path { get; }
        public int sourceReference { get; }
        public string presentationHint { get; }

        public Source(string name = null , string path = null, int sourceReference = 0, string presentationHint = null )
        {
            this.name = name;
            this.path = path;
            this.sourceReference = sourceReference;
            this.presentationHint = presentationHint;
        }
    }

    public class Breakpoint
    {
        public bool verified { get; }
        public int line { get; }

        public Breakpoint(bool verified, int line)
        {
            this.verified = verified;
            this.line = line;
        }
    }

    // ---- Events -------------------------------------------------------------------------

    public class InitializedEvent : Event
    {
        public InitializedEvent() : base("initialized") { }
    }

    public class StoppedEvent : Event
    {
        public StoppedEvent(int tid, string reasn, string txt = null)
            : base("stopped", new
            {
                threadId = tid,
                reason = reasn,
                text = txt
            })
        { }
    }

    public class ContinuedEvent : Event
    {
        public ContinuedEvent( bool all ) : base( "continued", new { allThreadsContinued = all } ) { }
    }

    public class ExitedEvent : Event
    {
        public ExitedEvent(int exCode) : base("exited", new { exitCode = exCode }) { }
    }

    public class TerminatedEvent : Event
    {
        public TerminatedEvent() : base("terminated") { }
    }

    public class BreakpointEvent : Event
    {
        public BreakpointEvent() : base("breakpoint") { }
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
        public OutputEvent(string cat, string outpt)
            : base("output", new
            {
                category = cat,
                output = outpt
            })
        { }
    }

    public class LoadedSourceEvent : Event
    {
        public LoadedSourceEvent(string pReason, Source pSource) : base("loadedSource", new { reason = pReason, source = pSource }) { }
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

        public ErrorResponseBody(Message error)
        {
            this.error = error;
        }
    }

    public class StackTraceResponseBody : ResponseBody
    {
        public StackFrame[] stackFrames { get; }

        public StackTraceResponseBody(List<StackFrame> frames = null)
        {
            if (frames == null)
                stackFrames = new StackFrame[0];
            else
                stackFrames = frames.ToArray<StackFrame>();
        }
    }

    public class ScopesResponseBody : ResponseBody
    {
        public Scope[] scopes { get; }

        public ScopesResponseBody(List<Scope> scps = null)
        {
            if (scps == null)
                scopes = new Scope[0];
            else
                scopes = scps.ToArray<Scope>();
        }
    }

    public class LoadedSourcesResponseBody : ResponseBody
    {
        public Source[] sources { get; }

        public LoadedSourcesResponseBody(List<Source> sources = null)
        {
            if (sources == null)
                this.sources = new Source[0];
            else
                this.sources = sources.ToArray<Source>();
        }
    }

    public class VariablesResponseBody : ResponseBody
    {
        public Variable[] variables { get; }

        public VariablesResponseBody(List<Variable> vars = null)
        {
            if (vars == null)
                variables = new Variable[0];
            else
                variables = vars.ToArray<Variable>();
        }
    }

    public class ThreadsResponseBody : ResponseBody
    {
        public Thread[] threads { get; }

        public ThreadsResponseBody(List<Thread> vars = null)
        {
            if (vars == null)
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

        public EvaluateResponseBody(string value, int reff = 0)
        {
            result = value;
            variablesReference = reff;
        }
    }

    public class SetBreakpointsResponseBody : ResponseBody
    {
        public Breakpoint[] breakpoints { get; }

        public SetBreakpointsResponseBody(List<Breakpoint> bpts = null)
        {
            if (bpts == null)
                breakpoints = new Breakpoint[0];
            else
                breakpoints = bpts.ToArray<Breakpoint>();
        }
    }

    // ---- The Session --------------------------------------------------------

    public abstract class DebugSession : ProtocolServer
    {
        private bool _debuggerLinesStartAt1;
        private bool _debuggerPathsAreURI;
        private bool _clientLinesStartAt1 = true;
        private bool _clientPathsAreURI = true;


        public DebugSession(bool debuggerLinesStartAt1, bool debuggerPathsAreURI = false)
        {
            _debuggerLinesStartAt1 = debuggerLinesStartAt1;
            _debuggerPathsAreURI = debuggerPathsAreURI;
        }

        protected override void DispatchRequest(string command, dynamic args, Response response)
        {
            ZMain.Log( "vscode: <- [" + command + "]" );

            if (args == null)
            {
                args = new { };
            }

            try
            {

                switch (command)
                {

                    case "initialize":
                        if (args.linesStartAt1 != null)
                        {
                            _clientLinesStartAt1 = (bool)args.linesStartAt1;
                        }
                        var pathFormat = (string)args.pathFormat;
                        if (pathFormat != null)
                        {
                            switch (pathFormat)
                            {
                                case "uri":
                                    _clientPathsAreURI = true;
                                    break;
                                case "path":
                                    _clientPathsAreURI = false;
                                    break;
                                default:
                                    return;
                            }
                        }
                        Initialize(response, args);
                        break;

                    case "launch":
                        Launch(response, args);
                        break;

                    case "attach":
                        Attach(response, args);
                        break;

                    case "disconnect":
                        Disconnect(response, args);
                        break;

                    case "next":
                        Next(response, args);
                        break;

                    case "continue":
                        Continue(response, args);
                        break;

                    case "stepIn":
                        StepIn(response, args);
                        break;

                    case "stepOut":
                        StepOut(response, args);
                        break;

                    case "pause":
                        Pause(response, args);
                        break;

                    case "stackTrace":
                        StackTrace(response, args);
                        break;

                    case "scopes":
                        Scopes(response, args);
                        break;

                    case "variables":
                        Variables(response, args);
                        break;

                    case "source":
                        Source(response, args);
                        break;

                    case "threads":
                        Threads(response, args);
                        break;

                    case "setBreakpoints":
                        SetBreakpoints(response, args);
                        break;

                    case "setFunctionBreakpoints":
                        SetFunctionBreakpoints(response, args);
                        break;

                    case "setExceptionBreakpoints":
                        SetExceptionBreakpoints(response, args);
                        break;

                    case "evaluate":
                        Evaluate(response, args);
                        break;

                    case "configurationDone":
                        ConfigurationDone(response, args);
                        break;

                    default:
                        break;
                }
            }
            catch (Exception e)
            {
            }

            if (command == "disconnect")
            {
                Stop();
            }
        }

        public abstract void Initialize(Response response, dynamic args);

        public abstract void Launch(Response response, dynamic arguments);

        public abstract void Attach(Response response, dynamic arguments);

        public virtual void ConfigurationDone( Response response, dynamic arguments )
        {
        }

        public abstract void Disconnect(Response response, dynamic arguments);

        public virtual void SetFunctionBreakpoints(Response response, dynamic arguments)
        {
        }

        public virtual void SetExceptionBreakpoints(Response response, dynamic arguments)
        {
        }

        public abstract void SetBreakpoints(Response response, dynamic arguments);

        public abstract void Continue(Response response, dynamic arguments);

        public abstract void Next(Response response, dynamic arguments);

        public abstract void StepIn(Response response, dynamic arguments);

        public abstract void StepOut(Response response, dynamic arguments);

        public abstract void Pause(Response response, dynamic arguments);

        public abstract void StackTrace(Response response, dynamic arguments);

        public abstract void Scopes(Response response, dynamic arguments);

        public abstract void Variables(Response response, dynamic arguments);

        public virtual void Source(Response response, dynamic arguments)
        {
        }

        public abstract void Threads(Response response, dynamic arguments);

        public abstract void Evaluate(Response response, dynamic arguments);

        // protected

        protected int ConvertDebuggerLineToClient(int line)
        {
            if (_debuggerLinesStartAt1)
            {
                return _clientLinesStartAt1 ? line : line - 1;
            }
            else
            {
                return _clientLinesStartAt1 ? line + 1 : line;
            }
        }

        protected int ConvertClientLineToDebugger(int line)
        {
            if (_debuggerLinesStartAt1)
            {
                return _clientLinesStartAt1 ? line : line + 1;
            }
            else
            {
                return _clientLinesStartAt1 ? line - 1 : line;
            }
        }

        protected string ConvertDebuggerPathToClient(string path)
        {
            if (_debuggerPathsAreURI)
            {
                if (_clientPathsAreURI)
                {
                    return path;
                }
                else
                {
                    Uri uri = new Uri(path);
                    return uri.LocalPath;
                }
            }
            else
            {
                if (_clientPathsAreURI)
                {
                    try
                    {
                        var uri = new System.Uri(path);
                        return uri.AbsoluteUri;
                    }
                    catch
                    {
                        return null;
                    }
                }
                else
                {
                    return path;
                }
            }
        }

        protected string ConvertClientPathToDebugger(string clientPath)
        {
            if (clientPath == null)
            {
                return null;
            }

            if (_debuggerPathsAreURI)
            {
                if (_clientPathsAreURI)
                {
                    return clientPath;
                }
                else
                {
                    var uri = new System.Uri(clientPath);
                    return uri.AbsoluteUri;
                }
            }
            else
            {
                if (_clientPathsAreURI)
                {
                    if (Uri.IsWellFormedUriString(clientPath, UriKind.Absolute))
                    {
                        Uri uri = new Uri(clientPath);
                        return uri.LocalPath;
                    }
                    Console.Error.WriteLine("path not well formed: '{0}'", clientPath);
                    return null;
                }
                else
                {
                    return clientPath;
                }
            }
        }
    }
}
