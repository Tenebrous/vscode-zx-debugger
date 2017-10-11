using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Policy;
using Newtonsoft.Json;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnassignedField.Global

namespace ZXDebug
{
    public class RuleSet
    {
        public string Rule;

        public List<string> SetMaps;
        public List<string> SetOpcodes;

        public List<string> AddMaps;
        public List<string> AddOpcodes;
    }

    public class SettingsValues
    {
        public string ProjectFolder;
        public bool   StopOnEntry;

        public string EXEPath;
        public string ExtensionPath;
        public string TempFolder;

        public List<string> Maps;
        public List<string> Opcodes;
        public RuleSet[]    Rules;

        public DisassemblerSettings   Disassembler;
        public Convert.FormatSettings Format;
    }

    public class Settings : SettingsValues
    {
        public delegate void DeserializedHandler();
        public event DeserializedHandler DeserializedEvent;

        List<SettingsValues> _layers = new List<SettingsValues>();

        public Settings()
        {
        }

        public void Clear()
        {
            _layers.Clear();

            Format = Convert.Settings;

            // get exe path & extension path for additional files
            var exe = new FileInfo( System.Reflection.Assembly.GetEntryAssembly().Location );
            var path = exe.Directory;

            EXEPath = path.FullName;

            if( path.Name == "bin" )
                path = path.Parent;

            ExtensionPath = path.FullName;
        }

        public void AddLayer( string json )
        {
            var layer = new Settings();
            JsonConvert.PopulateObject( json, layer );
            _layers.Add( layer );
        }

        public void ResolveRules( HashSet<string> values )
        {
            var finalMaps = new List<string>();
            var finalOpcodes = new List<string>();

            foreach( var rule in Rules )
            {
                var elements = rule.Rule.Split( new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries  );

                var apply = true;

                foreach( var element in elements )
                {
                    if( element.StartsWith( "!" ) && values.Contains( element.Substring( 1 ) ) )
                    {
                        apply = false;
                        break;
                    }
                    else if( !values.Contains( element ) )
                    {
                        apply = false;
                        break;
                    }
                }

                if( !apply )
                    continue;

                if( rule.SetMaps != null )
                {
                    Maps.Clear();
                    Maps.AddRange( rule.SetMaps );
                }

                if( rule.AddMaps != null )
                    Maps.AddRange( rule.AddMaps );

                if( rule.SetOpcodes != null )
                {
                    Opcodes.Clear();
                    Opcodes.AddRange( rule.AddOpcodes );
                }

                if( rule.AddOpcodes != null )
                    Opcodes.AddRange( rule.AddOpcodes );
            }


            // remove duplicates
            

            Deserialized();
        }

        public void SettingsUpdated()
        {
            // check some settings

            if( string.IsNullOrWhiteSpace( ProjectFolder ) )
                throw new Exception( "Property 'projectFolder' is missing or empty." );

            if( !Directory.Exists( ProjectFolder ) )
                throw new Exception( "Property 'projectFolder' refers to a folder that could not be found." );


            // get temp folder

            TempFolder = Path.Combine( ProjectFolder, ".zxdbg" );
            Directory.CreateDirectory( TempFolder );
        }

        
        public string Locate( string filename, string extFolder = null )
        {
            if( File.Exists( filename ) )
                return filename;

            var path = Path.Combine( ProjectFolder, filename );
            if( File.Exists( path ) )
                return path;

            path = Path.Combine( EXEPath, filename );
            if( File.Exists( path ) )
                return path;

            path = Path.Combine( ExtensionPath, filename );
            if( File.Exists( path ) )
                return path;

            if( extFolder != null )
            {
                path = Path.Combine( ExtensionPath, extFolder, filename );
                if( File.Exists( path ) )
                    return path;
            }

            throw new FileNotFoundException( "Can't find file", filename );
        }


        public void Deserialized()
        {
            DeserializedEvent?.Invoke();
        }
    }
}
