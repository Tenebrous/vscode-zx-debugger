using System;
using System.IO;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnassignedField.Global

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
        public string   ProjectFolder;
        public string[] SourceMaps;
        public string[] OpcodeTables;
        public bool     StopOnEntry;

        public string ExtensionPath;

        public DisassemblerSettings Disassembler;
        public Format.FormatSettings Format;

        public Settings()
        {
            // fill in defaults if required
        }

        public override void Validate()
        {
            // check cwd

            if( string.IsNullOrWhiteSpace( ProjectFolder ) )
                throw new Exception( "Property 'projectFolder' is missing or empty." );

            if( !Directory.Exists( ProjectFolder ) )
                throw new Exception( "Property 'projectFolder' refers to a folder that could not be found." );


            // get extension path for additional files

            var exe = new FileInfo( System.Reflection.Assembly.GetEntryAssembly().Location );
            var path = exe.Directory;
            
            if( path.Name == "bin" )
                path = path.Parent;

            ExtensionPath = path.FullName;
        }
    }
}
