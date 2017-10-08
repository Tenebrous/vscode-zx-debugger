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

        static Session _session;

        static void Main(string[] argv)
        {
            Log.MaxSeverityConsole = Log.Severity.Message;
            Log.MaxSeverityLog     = Log.Severity.Debug;


            // set up 
            _session = new Session();
            _session.Values = new ValueTree();
            _session.Settings = new Settings();
            _session.VSCode = new VSCode.Connection();
            _session.MachineConnection = new ZEsarUX.Connection();
            _session.Machine = new Machine( _session.MachineConnection );

            _session.HandleMachine = new HandleMachine( _session );
            _session.HandleVSCode = new HandleVSCode( _session );
            _session.HandleValueTree = new HandleValueTree( _session );

            _session.HandleMachine.Configure();
            _session.HandleVSCode.Configure();
            _session.HandleValueTree.Configure();

            while( _session.EventLoop() )
                ;
        }


        //static void SaveDebug()
        //{
        //    File.WriteAllText(
        //        Path.Combine( _tempFolder, "map_data.json" ),
        //        JsonConvert.SerializeObject(
        //            _session.Machine.SourceMaps,
        //            new JsonSerializerSettings()
        //            {
        //                Formatting = Formatting.Indented,
        //                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        //                NullValueHandling = NullValueHandling.Ignore
        //            }
        //        )
        //    );
        //
        //    File.WriteAllText(
        //        Path.Combine( _tempFolder, "map_files.json" ),
        //        JsonConvert.SerializeObject(
        //            _session.Machine.SourceMaps.Files,
        //            new JsonSerializerSettings()
        //            {
        //                Formatting = Formatting.Indented,
        //                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        //                NullValueHandling = NullValueHandling.Ignore
        //            }
        //        )
        //    );
        //}
    }
}

