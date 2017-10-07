using Spectrum;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using VSCode;
using ZXDebug.SourceMapper;
using File = System.IO.File;
using VSCodeBreakpoint = VSCode.Breakpoint;

namespace ZXDebug
{
    public static class Adapter
    {
        static bool _running;

        static Value _rootValues = new Value();
        static Value _registersValues;
        static Value _pagingValues;
        static Value _settingsValues;

        static bool _needVSCodeRefresh;

        static Session _session;

        static void Main(string[] argv)
        {
            Log.MaxSeverityConsole = Log.Severity.Message;
            Log.MaxSeverityLog     = Log.Severity.Debug;


            // set up 
            _session = new Session();
            _session.Settings = new Settings();
            _session.VSCode = new VSCode.Connection();
            _session.Machine = new Machine(new ZEsarUX.Connection());
            _session.Device = new ZEsarUX.Connection();

            _session.HandleMachine = new HandleMachine( _session );
            _session.HandleMachine.Configure();

            _session.HandleVSCode = new HandleVSCode( _session );
            _session.HandleVSCode.Configure();

            _session.Settings.Disassembler = _session.Machine.Disassembler.Settings;
            _session.Settings.Format = Convert.Settings;
            

            // tie all the values together
            SetupValues( _rootValues, _session.Machine );


            // event loop
            _running = true;


            // testing things

            //_machine.SourceMaps.SourceRoot = @"D:\Dev\ZX\test1";
            //_machine.SourceMaps.Add( @"D:\Dev\ZX\test1\tmp\game.map" );

            // event loop
            while( _running )
            {
                var vsactive = _session.VSCode.Process();
                var dbgactive = _session.Device.Process();

                if( !vsactive )
                {
                    if( _needVSCodeRefresh )
                    {
                        System.Threading.Thread.Sleep( 150 );
                        _session.VSCode.Refresh();
                        _needVSCodeRefresh = false;
                    }
                    else
                    {
                        System.Threading.Thread.Sleep( 1 );
                    }
                }
            }
        }


        static string FindFile( string filename, string extFolder )
        {
            if( File.Exists( filename ) )
                return filename;

            var path = Path.Combine( _session.Settings.ProjectFolder, filename );
            if( File.Exists( path ) )
                return path;

            path = Path.Combine( _session.Settings.ExtensionPath, filename );
            if( File.Exists( path ) )
                return path;

            path = Path.Combine( _session.Settings.ExtensionPath, extFolder, filename );
            if( File.Exists( path ) )
                return path;

            throw new FileNotFoundException( "Can't find file", filename );
        }


        static Variable CreateVariableForValue( Value value )
        {
            value.Refresh();

            return new Variable(
                value.Name,
                value.Formatted,
                "value",
                value.Children.Count == 0 ? -1 : value.ID,
                new VariablePresentationHint( "data" )
            );
        }

        static void SetupValues( Value values, Machine machine )
        {
            _registersValues = values.Create( "Registers" );
            SetupValues_Registers( _registersValues );

            _pagingValues = values.Create( "Paging", refresher: SetupValues_Paging );
            SetupValues_Paging( _pagingValues );

            _settingsValues = values.Create("Settings");
            SetupValues_Settings( _settingsValues );
        }

        static void SetupValues_Registers( Value val )
        {
            Value reg16;

            val.Create(         "A",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "HL",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "H",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "L",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "BC",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "B",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "C",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "DE",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "D",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "E",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );


            val.Create(         "A'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "HL'", getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "H'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "L'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "BC'", getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "B'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "C'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "DE'", getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "D'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "E'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );


            reg16 = val.Create( "IX",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "IXH", getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "IXL", getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "IY",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "IYH", getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "IYL", getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            val.Create(         "PC",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );

            val.Create(         "SP",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );

            val.Create(         "I",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            val.Create(         "R",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
        }

        static void SetupValues_Paging( Value val )
        {
            val.ClearChildren();
            foreach( var p in _session.Machine.Memory.Slots )
            {
                var slot = val.Create( p.Min.ToHex(), delegate( Value pValue ) { pValue.Content = p.Bank.ID.ToString(); } );
            }
        }

        static void SetupValues_Settings( Value val )
        {
        }

        static void SetReg( Value val, string content )
        {
            _session.Machine.Registers.Set( val.Name, content );
        }

        static string GetReg( Value val )
        {
            return _session.Machine.Registers[val.Name].ToString();
        }



        // other things

        static void Initialise( string json )
        {
            // read settings
            _session.Settings.FromJSON( json );
            _session.Settings.Validate();

            
            // set up a temp folder
            _tempFolder = Path.Combine( _session.Settings.ProjectFolder, ".zxdbg" );
            Directory.CreateDirectory( _tempFolder );

            
            // load source maps
            _session.Machine.SourceMaps.Clear();
            _session.Machine.SourceMaps.SourceRoot = _session.Settings.ProjectFolder;

            var jsonSettings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore, 
                DefaultValueHandling = DefaultValueHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };

            long beforeTotal = GC.GetTotalMemory(true);
            long beforeSingle = 0;

            foreach( var filename in _session.Settings.SourceMaps )
            {
                var file = FindFile( filename, "maps" );

                beforeSingle = GC.GetTotalMemory( true );
                var map = _session.Machine.SourceMaps.Add( file );
                Log.Write( Log.Severity.Message, "Loaded map: " + file + " (~" + ( GC.GetTotalMemory( true ) - beforeSingle ) + ")" );

                var fileOnly = Path.GetFileName( filename );
                System.IO.File.WriteAllText(
                    Path.Combine( _tempFolder, fileOnly + ".address.json" ),
                    JsonConvert.SerializeObject( map.Source, jsonSettings )
                );

                System.IO.File.WriteAllText(
                    Path.Combine( _tempFolder, fileOnly + ".labels.json" ),
                    JsonConvert.SerializeObject( map.Labels, jsonSettings )
                );
            }

            Log.Write( Log.Severity.Message, "Loaded maps (~" + ( GC.GetTotalMemory( true ) - beforeTotal ) + ")" );


            // load opcode layers for the disassembler
            _session.Machine.Disassembler.ClearLayers();

            beforeTotal = GC.GetTotalMemory(true);

            foreach( var table in _session.Settings.OpcodeTables )
            {
                var file = FindFile( table, "opcodes" );

                beforeSingle = GC.GetTotalMemory( true );
                _session.Machine.Disassembler.AddLayer( file );
                Log.Write( Log.Severity.Message, "Loaded opcode layer: " + file + " (~" + ( GC.GetTotalMemory( true ) - beforeSingle ) + ")" );
            }

            Log.Write( Log.Severity.Message, "Loaded opcode layers (~" + ( GC.GetTotalMemory( true ) - beforeTotal ) + ")" );


            // all done
        }

        static void SaveDebug()
        {
            File.WriteAllText(
                Path.Combine( _tempFolder, "map_data.json" ),
                JsonConvert.SerializeObject(
                    _session.Machine.SourceMaps,
                    new JsonSerializerSettings()
                    {
                        Formatting = Formatting.Indented,
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore
                    }
                )
            );

            File.WriteAllText(
                Path.Combine( _tempFolder, "map_files.json" ),
                JsonConvert.SerializeObject(
                    _session.Machine.SourceMaps.Files,
                    new JsonSerializerSettings()
                    {
                        Formatting = Formatting.Indented,
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore
                    }
                )
            );
        }


        static string _tempFolder;

        static string _disassemblyFile;
        static string DisassemblyFile
        {
            get { return _disassemblyFile = _disassemblyFile ?? Path.Combine( _tempFolder, "disasm.zdis" ); }
        }

        static Source _stackSource;
        static Source StackSource
        {
            get { return _stackSource = _stackSource ?? new Source( "#", "", 0, Source.SourcePresentationHintEnum.deemphasize ); }
        }

        static Source _disassemblySource;
        static Source DisassemblySource
        {
            get { return _disassemblySource = _disassemblySource ?? new Source( " ", DisassemblyFile, 0, Source.SourcePresentationHintEnum.normal ); }
        }


        // standard debug commands from VSCode use line numbers based at 0 or 1 depending on the value of _linesStartAt1
        // custom debug commands always use 0
        static bool _linesStartAt1;
        static int LineFromVSCode( int line )
        {
            return _linesStartAt1 ? line - 1 : line;
        }
        static int LineToVSCode( int line )
        {
            return _linesStartAt1 ? line : line + 1;
        }
    }
}

