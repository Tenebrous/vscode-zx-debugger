using System;
using System.IO;
using Newtonsoft.Json;

namespace ZXDebug
{
    /*
        example:

        {
            "type": "zxdebug",
            "name": "ZX Debugger",
            "request": "attach",
            "cwd": "d:\\Dev\\ZX\\test1",
            "sourceMaps": [
                "48k_rom.dbg",
                "tmp\\game.map"
            ],
            "opcodeTables": [
                "z80.json",
                "next.json"
            ],
            "stopOnEntry": true,
            "hexPrefix": "$",
            "hexSuffix": "",
            "workspaceConfiguration": {
                "disassembler": {
                    "blankLineBeforeLabel": false
                }
            }
        }

        // note, workspaceConfiguration is merged into the top-level by VSCode.Settings
        // so we can assume that workspaceConfiguration doesn't exist and everything
        // within it is actually one level higher.
    */

    public class Settings : VSCode.Settings
    {
        // ReSharper disable InconsistentNaming
        // ReSharper disable UnassignedField.Global

        [JsonProperty(propertyName:"cwd")]
        public string   ProjectFolder;

        public string[] SourceMaps;
        public string[] OpcodeTables;
        public bool     StopOnEntry;
        public string   HexPrefix;
        public string   HexSuffix;

        public DisassemblerSettings Disassembler;
        
        // ReSharper restore InconsistentNaming
        // ReSharper restore UnassignedField.Global

        public string ExtensionPath;

        public Settings()
        {
            // fill in defaults if required
        }

        public override void Validate()
        {
            // check cwd

            if( string.IsNullOrWhiteSpace( ProjectFolder ) )
                throw new Exception( "Property 'cwd' is missing or empty." );

            if( !Directory.Exists( ProjectFolder ) )
                throw new Exception( "Property 'cwd' refers to a folder that could not be found." );


            // get extension path for additional files

            var exe = new FileInfo( System.Reflection.Assembly.GetEntryAssembly().Location );
            var path = exe.Directory;
            
            if( path.Name == "bin" )
                path = path.Parent;

            ExtensionPath = path.FullName;
        }
    }
}
