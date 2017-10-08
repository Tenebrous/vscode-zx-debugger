using System;
using System.IO;
using Newtonsoft.Json;

namespace ZXDebug
{
    public class HandleMachine
    {
        Session _session;

        public HandleMachine( Session session )
        {
            _session = session;
        }

        public void Configure()
        {
            _session.Machine.PausedEvent             += Paused;
            _session.Machine.ContinuedEvent          += Continued;
            _session.Machine.DisassemblyUpdatedEvent += DisassemblyUpdated;

            _session.Settings.Disassembler = _session.Machine.Disassembler.Settings;
            _session.Settings.DeserializedEvent += SettingsUpdated;
        }

        void Paused()
        {
            _session.VSCode.Stopped( 1, "step", "step" );
        }

        void Continued()
        {
            _session.VSCode.Continued( true );
        }

        void DisassemblyUpdated()
        {
            _session.VSCode.NeedRefresh = true;
        }

        void SettingsUpdated( VSCode.Settings settings )
        {
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
                var file = _session.Settings.Locate( filename, "maps" );

                beforeSingle = GC.GetTotalMemory( true );
                var map = _session.Machine.SourceMaps.Add( file );
                Log.Write( Log.Severity.Message, "Loaded map: " + file + " (~" + ( GC.GetTotalMemory( true ) - beforeSingle ) + ")" );

                var fileOnly = Path.GetFileName( filename );
                File.WriteAllText(
                    Path.Combine( _session.Settings.TempFolder, fileOnly + ".address.json" ),
                    JsonConvert.SerializeObject( map.Source, jsonSettings )
                );

                File.WriteAllText(
                    Path.Combine( _session.Settings.TempFolder, fileOnly + ".labels.json" ),
                    JsonConvert.SerializeObject( map.Labels, jsonSettings )
                );
            }

            Log.Write( Log.Severity.Message, "Loaded maps (~" + ( GC.GetTotalMemory( true ) - beforeTotal ) + ")" );


            // load opcode layers for the disassembler
            _session.Machine.Disassembler.ClearLayers();

            beforeTotal = GC.GetTotalMemory( true );

            foreach( var table in _session.Settings.OpcodeTables )
            {
                var file = _session.Settings.Locate( table, "opcodes" );

                beforeSingle = GC.GetTotalMemory( true );
                _session.Machine.Disassembler.AddLayer( file );
                Log.Write( Log.Severity.Message, "Loaded opcode layer: " + file + " (~" + ( GC.GetTotalMemory( true ) - beforeSingle ) + ")" );
            }

            Log.Write( Log.Severity.Message, "Loaded opcode layers (~" + ( GC.GetTotalMemory( true ) - beforeTotal ) + ")" );


        }
    }
}
