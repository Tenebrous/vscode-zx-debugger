using System;
using System.IO;
using Newtonsoft.Json;

namespace ZXDebug
{
    public class HandleMachine : Loggable
    {
        Session _session;

        public HandleMachine( Session session )
        {
            _session = session;
        }

        public void Configure()
        {
            _session.Machine.ConnectedEvent          += Connected;
            _session.Machine.PausedEvent             += Paused;
            _session.Machine.ContinuedEvent          += Continued;
            _session.Machine.DisconnectedEvent       += Disconnected;

            _session.Machine.DisassemblyUpdatedEvent += DisassemblyUpdated;
            _session.Machine.MachineCapsChangedEvent += MachineCapsChangedEvent;

            _session.Settings.Disassembler = _session.Machine.Disassembler.Settings;
            _session.Settings.DeserializedEvent += SettingsUpdated;
        }

        void Connected()
        {
            _session.Machine.Caps.Read();
            MachineCapsChangedEvent();
        }

        void Paused()
        {
            _session.VSCode.Stopped( 1, "step", "step" );
        }

        void Continued()
        {
            _session.VSCode.Continued( true );
        }

        void Disconnected()
        {
        }

        void MachineCapsChangedEvent()
        {
            LogMessage( "Updated capabilities: " + _session.Machine.Caps.ToString() );
            _session.Settings.Resolve( _session.Machine.Caps );
        }

        void DisassemblyUpdated()
        {
            _session.VSCode.NeedRefresh = true;
        }

        void SettingsUpdated()
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

            foreach( var filename in _session.Settings.Maps )
            {
                var file = _session.Settings.Locate( filename, "maps" );

                beforeSingle = GC.GetTotalMemory( true );
                var map = _session.Machine.SourceMaps.Add( file );
                LogMessage( "Loaded map: " + file + " (~" + ( GC.GetTotalMemory( true ) - beforeSingle ) + ")" );

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

            LogMessage( "Loaded maps (~" + ( GC.GetTotalMemory( true ) - beforeTotal ) + ")" );


            // load opcode layers for the disassembler
            _session.Machine.Disassembler.ClearLayers();

            beforeTotal = GC.GetTotalMemory( true );

            foreach( var table in _session.Settings.Opcodes )
            {
                var file = _session.Settings.Locate( table, "opcodes" );

                beforeSingle = GC.GetTotalMemory( true );
                _session.Machine.Disassembler.AddLayer( file );
                LogMessage( "Loaded opcode layer: " + file + " (~" + ( GC.GetTotalMemory( true ) - beforeSingle ) + ")" );
            }

            LogMessage( "Loaded opcode layers (~" + ( GC.GetTotalMemory( true ) - beforeTotal ) + ")" );


        }
    }
}
