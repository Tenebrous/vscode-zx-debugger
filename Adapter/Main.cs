using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VSCodeDebugAdapter;
using Z80Machine;
using Thread = System.Threading.Thread;

namespace ZEsarUXDebugger
{
    public class ZMain
	{
	    static ZEsarUXConnection _zesarux;
	    static VSCodeConnection _vscode;
	    static bool _active;

	    static string _folder;

	    static Value _values = new Value();
	    static Value _registers;
	    static Value _settings;
	    static Machine _machine;

    	static void Main(string[] argv)
	    {
	        // set up 

            Log.OnLog += Log_SendToVSCode;
	        Log.MaxSeverity = Log.Severity.Message;
            Log.Write( Log.Severity.Message, "main: starting... " );

            
	        _registers = _values.Create("Registers");
	        _registers.Refresher = Registers_Refresh;

	        _settings = _values.Create( "Settings" );

            //todo:link vars directly to _registers items using delegates here and remove the need to refresh _values

            // vscode events

            _vscode = new VSCodeConnection();
	        _vscode.OnPause += VSCode_OnPause;
	        _vscode.OnContinue += VSCode_OnContinue;
	        _vscode.OnNext += VSCode_OnNext;
	        _vscode.OnStepIn += VSCode_OnStepIn;
            _vscode.OnInitialize += VSCode_OnInitialize;
	        _vscode.OnLaunch += VSCode_OnLaunch;
	        _vscode.OnAttach += VSCode_OnAttach;
	        _vscode.OnConfigurationDone += VSCode_OnConfigurationDone;
	        _vscode.OnGetThreads += VSCode_OnGetThreads;
	        _vscode.OnGetStackTrace += VSCode_OnGetStackTrace;
	        _vscode.OnGetScopes += VSCode_OnGetScopes;
	        _vscode.OnGetVariables += VSCode_OnGetVariables;
	        _vscode.OnSetVariable += VSCode_OnSetVariable;
	        _vscode.OnGetSource += VSCode_OnGetSource;
	        _vscode.OnGetLoadedSources += VSCode_OnGetLoadedSources;
	        _vscode.OnDisconnect += VSCode_OnDisconnect;
	        _vscode.OnEvaluate += VSCode_OnEvaluate;


            // zesarux events

            _zesarux = new ZEsarUXConnection();
	        // _zesarux.OnPaused += Z_OnPaused;
	        // _zesarux.OnContinued += Z_OnContinued;
	        // _zesarux.OnStepped += Z_OnStepped;
            // _zesarux.OnRegisterChange += Z_OnRegisterChange;

            _machine = new Machine( _zesarux );

	        _active = true;

	        var wasActive = false;


	        // event loop
	        while( _active )
	        {
	            var active = _vscode.Read() || _zesarux.Read();

	            switch( _zesarux.StateChange )
	            {
	                case ZEsarUXConnection.RunningStateChangeEnum.Started:
                        _vscode.Continued( true );
	                    break;

                    case ZEsarUXConnection.RunningStateChangeEnum.Stopped:
                        _vscode.Stopped( 1, "step", "step" );
                        break;
	            }

	            if( !active )
	            {
	                if( wasActive )
	                    Log.Write( Log.Severity.Debug, "" );

	                Thread.Sleep( 10 );
	            }

	            wasActive = active;
	        }

	        Log.Write( Log.Severity.Message, "main: stopped." );
	    }


	    // events coming in from VSCode

	    static void VSCode_OnInitialize( Request pRequest )
	    {
	        _vscode.Send(
	            pRequest,
	            new Capabilities()
	            {
	                supportsConfigurationDoneRequest = true
                }
	        );

	        _vscode.Initialized();
	    }

         
	    static void VSCode_OnContinue( Request pRequest )
	    {
	        _zesarux.Continue();
	    }

	    static void VSCode_OnPause( Request pRequest )
	    {
	        _zesarux.Pause();
	    }

        static void VSCode_OnNext( Request pRequest )
	    {
	        // must ack before sending anything else
	        _vscode.Send(pRequest);

            _zesarux.StepOver();
	    }

	    static void VSCode_OnStepIn( Request pRequest )
	    {
	        // must ack before sending anything else
	        _vscode.Send(pRequest);

            _zesarux.Step();
        }

        static void VSCode_OnLaunch( Request pRequest )
        {
            if( !_zesarux.Start())
	            _vscode.Send(pRequest, pErrorMessage: "Could not connect to ZEsarUX");
	    }

	    static void VSCode_OnAttach( Request pRequest )
	    {
	        _folder = DynString( pRequest.arguments, "folder" );

	        if( string.IsNullOrWhiteSpace( _folder ) )
	        {
	            Log.Write( Log.Severity.Error, "Property 'folder' is missing or empty." );
	            return;
	        }

	        if( !Directory.Exists( _folder ) )
	        {
	            Log.Write( Log.Severity.Error, "Property 'folder' refers to a folder that could not be found." );
	            return;
	        }

	        _zesarux.TempFolder = Path.Combine( _folder, ".debug" );
	        Directory.CreateDirectory( _zesarux.TempFolder );

            if( !_zesarux.Start() )
	            _vscode.Send(pRequest, pErrorMessage: "Could not connect to ZEsarUX");
	    }

	    static void VSCode_OnConfigurationDone( Request pRequest )
	    {
	    }

        static void VSCode_OnGetThreads( Request pRequest )
        {
            _vscode.Send( 
                pRequest,
                new ThreadsResponseBody( 
                    new List<VSCodeDebugAdapter.Thread>()
                    {
                        new VSCodeDebugAdapter.Thread( 1, "Main" )
                    }
                )
            );
        }

	    static Source DisassemblySource()
	    {
	        return new Source( "-", _zesarux.DisassemblyFile, 0, Source.SourcePresentationHintEnum.deemphasize );
	    }

        static List<StackFrame> _stackFrames = new List<StackFrame>();
	    static void VSCode_OnGetStackTrace( Request pRequest )
	    {
	        _machine.RefreshRegisters();

	        _zesarux.GetMemoryPages();
            _zesarux.GetRegisters();
	        _zesarux.GetStackTrace();
            _zesarux.UpdateDisassembly( _machine.Registers.PC );

            _stackFrames.Clear();

	        var stack = _zesarux.Stack;
	        for( int i = 0; i < stack.Count; i++ )
	        {   
	            _stackFrames.Add(
	                new StackFrame(
	                    i + 1,
	                    string.Format( "${0:X4} / {0}", stack[i] ),
	                    DisassemblySource(),
	                    _zesarux.FindLine( stack[i] ),
	                    0,
	                    "normal"
                    )
	            );
	        }

	        _vscode.Send(
                pRequest,
                new StackTraceResponseBody(
                    _stackFrames
                )
            );
        }

        static void VSCode_OnGetScopes( Request pRequest )
        {
            int frameId = pRequest.arguments.frameId;

            _zesarux.UpdateDisassembly( _zesarux.Stack[frameId-1] );

            var scopes = new List<Scope>();

            foreach( var value in _values.Children )
            {
                scopes.Add( 
                    new Scope( 
                        value.Name,
                        value.ID,
                        false
                    ) 
                );
            }

            _vscode.Send( pRequest, new ScopesResponseBody( scopes ) );
        }

	    static void VSCode_OnGetLoadedSources( Request pRequest )
	    {
	    //    _vscode.Send(
        //        pRequest,
        //        new LoadedSourcesResponseBody(
        //            new List<Source>()
        //            {
        //                DisassemblySource()
        //            }
        //        )
        //    );
	    }

	    static void VSCode_OnGetSource( Request pRequest )
	    {
        //    _zesarux.GetRegisters();
        //
        //    DisassemblePC();
        //
        //    _vscode.Send( 
        //        pRequest,
        //        new SourceResponseBody(
        //            _zesarux.Disassembly,
        //            ""
        //        )
        //    );
	    }


        static void VSCode_OnEvaluate( Request pRequest )
	    {
	        var value = "";
            string formatted = "";

            string expression = pRequest.arguments.expression;
	        string prefix = "";

	        var split = expression.Split( new []{' ', ','}, StringSplitOptions.RemoveEmptyEntries );
	        int parseIndex = 0;

	        if( split[parseIndex].StartsWith( "(" ) && split[parseIndex].EndsWith( ")" ) )
	        {
	            var target = split[parseIndex].Substring( 1, split[parseIndex].Length - 2 );
	            ushort address;

	            if( _registers.HasAllByName( target ) )
	            {
	                address = Convert.ToUInt16( _registers.AllByName( target ).Content );
	                prefix = string.Format( "${0:X4}: ", address );
	            }
	            else
	                address = ParseAddress( target );

	            int length = 2;
	            parseIndex++;

                if( split.Length > parseIndex )
                    if( int.TryParse( split[parseIndex], out length ) )
                        parseIndex++;
                    else
                        length = 2;

	            value = _zesarux.GetMemory( address, length );
	            formatted = value;
	        }
	        else
	        {
	            if( _registers.HasAllByName( split[parseIndex] ) )
	            {
	                var reg = _registers.AllByName( split[parseIndex] );

	                value = reg.Content;
                    formatted = reg.Formatted;
	                parseIndex++;
	            }
            }

	        if( split.Length > parseIndex )
	        {
	            int count = 0;

	            if( split[parseIndex] == "b" )
	            {
	                formatted = HexToBin( value, 8 );
	                parseIndex++;
	            }
	            else if( split[parseIndex].StartsWith( "b" ) && int.TryParse( split[parseIndex].Substring( 1 ), out count ) )
	            {
	                formatted = HexToBin( value, count );
	                parseIndex++;
	            }
	            else if( split[parseIndex] == "n" )
	            {
	                formatted = HexToBin( value, 4 );
	                parseIndex++;
	            }
	            else if( split[parseIndex] == "w" )
	            {
	                formatted = HexToBin( value, 2 );
	                parseIndex++;
	            }
	            else if( split[parseIndex] == "dw" )
	            {
	                formatted = HexToBin( value, 4 );
	                parseIndex++;
	            }
	        }

            _vscode.Send(
                pRequest, 
                new EvaluateResponseBody(
                    prefix + formatted
                )
            );
	    }

	    static StringBuilder _tempHexToBin = new StringBuilder();
	    static string HexToBin( string pValue, int pSplit )
	    {
	        _tempHexToBin.Clear();

	        int count = 0;
	        for( int i = 0; i < pValue.Length; i+=2 )
	        {
	            var part = Convert.ToByte( pValue.Substring( i, 2 ), 16 );
	            var binary = Convert.ToString( part, 2 ).PadLeft( 8, '0' );


	            foreach( var ch in binary )
	            {
	                _tempHexToBin.Append( ch );

	                if( ++count % pSplit == 0 )
	                    _tempHexToBin.Append( ' ' );
	            }
            }

            return _tempHexToBin.ToString().Trim();
	    }

	    static ushort ParseAddress( string pValue )
	    {
	        ushort result = 0;

	        try
	        {
	            if( pValue.StartsWith( "$" ) )
	                result = Convert.ToUInt16( pValue.Substring( 1 ), 16 );
	            else if( pValue.StartsWith( "0x" ) )
	                result = Convert.ToUInt16( pValue.Substring( 2 ), 16 );
	            else
	                result = ushort.Parse( pValue );
            }
            catch( Exception e )
	        {
	            Log.Write( Log.Severity.Error, "Can't parse address '" + pValue + "'" );
	        }

            return result;
	    }

        static List<Variable> _tempVariables = new List<Variable>();
        static void VSCode_OnGetVariables( Request pRequest )
        {
            _tempVariables.Clear();

            int id = pRequest.arguments.variablesReference;
            var value = _values.All(id);

            if( value != null )
            {
                value.Refresh();

                var data = new VariablePresentationHint( "data" );

                foreach( var child in value.Children )
                {
                    _tempVariables.Add(
                        new Variable(
                            child.Name,
                            child.Formatted,
                            "value",
                            child.Children.Count == 0 ? -1 : child.ID,
                            data )
                        );
                }
            }

            _vscode.Send(
                pRequest,
                new VariablesResponseBody(_tempVariables)
            );
        }


	    static void VSCode_OnSetVariable( Request pRequest )
	    {
	        string reg = pRequest.arguments.name.ToString();
	        string val = pRequest.arguments.value.ToString();

            var regs = _zesarux.SetRegister( reg, val );
            UpdateValues( _values, regs );

            _vscode.Send(
                pRequest,
	            new SetVariableResponseBody( _values.AllByName( reg ).Formatted )
            );
	    }


        static void VSCode_OnDisconnect( Request pRequest )
	    {
	        _zesarux.Stop();
	        _active = false;
	    }



	    // events from values/variables

	    static void Registers_Refresh( Value pValue )
	    {
	        UpdateValues( pValue, _machine.RefreshRegisters() );
	    }

        static void UpdateValues( Value pValue, Registers pRegs )
        {
            SetReg8(  pValue, "A" ,                pRegs.A,     Hex8Formatter  );
	        SetReg16( pValue, "HL",  "H",   "L",   pRegs.HL,    Hex16Formatter );
	        SetReg16( pValue, "BC",  "B",   "C",   pRegs.BC,    Hex16Formatter );
	        SetReg16( pValue, "DE",  "D",   "E",   pRegs.DE,    Hex16Formatter );
	        SetReg8(  pValue, "A'",                pRegs.AltA,  Hex8Formatter  );
	        SetReg16( pValue, "HL'", "H'",  "L'",  pRegs.AltHL, Hex16Formatter );
            SetReg16( pValue, "BC'", "B'",  "C'",  pRegs.AltBC, Hex16Formatter );
	        SetReg16( pValue, "DE'", "C'",  "E'",  pRegs.AltDE, Hex16Formatter );
	        SetReg16( pValue, "IX",  "IXH", "IXL", pRegs.IX,    Hex16Formatter );
            SetReg16( pValue, "IY",  "IYH", "IYL", pRegs.IY,    Hex16Formatter );
	        SetReg16( pValue, "PC",                pRegs.PC,    Hex16Formatter );
	        SetReg16( pValue, "SP",                pRegs.SP,    Hex16Formatter );
            SetReg8(  pValue, "I",                 pRegs.I,     Hex8Formatter  );
	        SetReg8(  pValue, "R",                 pRegs.R,     Hex8Formatter  );
        }

	    static void SetReg16( Value pParent, string pName, string pHigh, string pLow, int pValue, Value.ValueFormatter pFormatter )
	    {
	        var value = pParent.ChildByName( pName );
	        value.Content = pValue.ToString();
	        value.Formatter = pFormatter;

	        SetReg8( value, pHigh, ( pValue & 0xFF00 ) >> 8, Hex8Formatter );
	        SetReg8( value, pLow,  ( pValue & 0x00FF ),      Hex8Formatter );
	    }

        static void SetReg16( Value pParent, string pName, int pValue, Value.ValueFormatter pFormatter )
	    {
	        var value = pParent.ChildByName( pName );
	        value.Formatter = pFormatter;
	        value.Content = pValue.ToString();
	    }

        static void SetReg8( Value pParent, string pName, int pValue, Value.ValueFormatter pFormatter )
	    {
	        var value = pParent.ChildByName( pName );
	        value.Formatter = pFormatter;
	        value.Content = pValue.ToString();
	    }

        static string Hex16Formatter( Value pValue )
        {
            uint value = Convert.ToUInt16( pValue.Content );
            return string.Format( "${0:X4} / {0}", value );
        }

        static string Hex8Formatter( Value pValue )
        {
            byte value = Convert.ToByte( pValue.Content );
            return string.Format( "${0:X2} / {0}", value );
        }


        // events from Log

        static void Log_SendToVSCode( Log.Severity pLevel, string pMessage )
        {
            var type = pLevel == Log.Severity.Error ? OutputEvent.OutputEventType.stderr : OutputEvent.OutputEventType.stdout;
	        _vscode?.Send( new OutputEvent( type, pMessage + "\n" ) );
	    }


        //static void Z_OnRegisterChange( string pRegister, string pValue )
        //{
        //    _vscode.Send(
        //        new var
        //    );
        //}


        //public override void SetBreakpoints( Response response, dynamic arguments )
        //{
        //    Log.Write("vscode: setbreakpoints");
        //}

        //public override void Next( Response response, dynamic arguments )
        //{
        //    Log.Write("vscode: next");
        //}

        //public override void StepIn( Response response, dynamic arguments )
        //{
        //    Log.Write("vscode: stepin");
        //}

        //public override void StepOut( Response response, dynamic arguments )
        //{
        //    Log.Write("vscode: stepout");
        //}

        //public override void StackTrace( Response response, dynamic arguments )
        //{
        //    Log.Write("vscode: stacktrace");
        //}

        //public override void Variables( Response response, dynamic arguments )
        //{
        //    Log.Write("vscode: variables");
        //}

        //public override void SetExceptionBreakpoints( Response response, dynamic arguments )
        //{
        //    Log.Write("vscode: set exception breakpoints");
        //}

        //public override void SetFunctionBreakpoints( Response response, dynamic arguments )
        //{
        //    Log.Write("vscode: set function breakpoints");
        //}

        //public override void Source( Response response, dynamic arguments )
        //{
        //    Log.Write("vscode: source");
        //}

        static string DynString( dynamic pArgs, string pName, string pDefault = null )
	    {
	        var result = (string)pArgs[pName];

	        if( result == null )
	            return pDefault;

	        result = result.Trim();

	        if( result.Length == 0 )
	            return pDefault;

	        return result;
	    }
    }
}

