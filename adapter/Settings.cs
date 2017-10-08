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
            "workspaceConfiguration": {
                "format": {
                    "hexPrefix": "$",
                    "hexSuffix": "",
                    "labelPosition": "right"
                },
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
        // from json
        public string   ProjectFolder;
        public string[] SourceMaps;
        public string[] OpcodeTables;
        public bool     StopOnEntry;

        // links to other class's settings, filled in when deserializing json from vscode
        public DisassemblerSettings Disassembler;
        public Convert.FormatSettings Format;


        // derived from above
        public string   ExtensionPath;
        public string   TempFolder;

        public Settings()
        {
            Format = Convert.Settings;

            DeserializedEvent += SettingsUpdated;
        }

        void SettingsUpdated( VSCode.Settings pSettings )
        {
            // check some settings

            if( string.IsNullOrWhiteSpace( ProjectFolder ) )
                throw new Exception( "Property 'projectFolder' is missing or empty." );

            if( !Directory.Exists( ProjectFolder ) )
                throw new Exception( "Property 'projectFolder' refers to a folder that could not be found." );


            // get temp folder

            TempFolder = Path.Combine( ProjectFolder, ".zxdbg" );
            Directory.CreateDirectory( TempFolder );
            

            // get extension path for additional files

            var exe = new FileInfo( System.Reflection.Assembly.GetEntryAssembly().Location );
            var path = exe.Directory;
            
            if( path.Name == "bin" )
                path = path.Parent;

            ExtensionPath = path.FullName;
        }

        
        public string Locate( string filename, string extFolder )
        {
            if( File.Exists( filename ) )
                return filename;

            var path = Path.Combine( ProjectFolder, filename );
            if( File.Exists( path ) )
                return path;

            path = Path.Combine( ExtensionPath, filename );
            if( File.Exists( path ) )
                return path;

            path = Path.Combine( ExtensionPath, extFolder, filename );
            if( File.Exists( path ) )
                return path;

            throw new FileNotFoundException( "Can't find file", filename );
        }
    }
}
