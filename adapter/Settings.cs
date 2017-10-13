using System;
using System.Collections.Generic;
using System.IO;
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

        Dictionary<LayerEnum, SettingsValues> _layers = new Dictionary<LayerEnum, SettingsValues>();

        public Settings()
        {
        }

        public void Clear()
        {
            _layers.Clear();
            Format = Convert.Settings;
        }

        public enum LayerEnum
        {
            Base = 0,
            LaunchAttach = 1,
            User = 2,
            Workspace = 3
        }

        public void SetLayer( LayerEnum layer, string json )
        {
            var data = new Settings();
            JsonConvert.PopulateObject( json, data );
            _layers[layer] = data;
        }

        public void Resolve( HashSet<string> values )
        {
            var finalMaps = new List<string>();
            var finalOpcodes = new List<string>();

            var sorted = new List<KeyValuePair<LayerEnum, SettingsValues>>( _layers );
            sorted.Sort(( left, right ) => left.Key.CompareTo( right.Key ) );

            foreach( var layer in sorted )
            {
                var json = JsonConvert.SerializeObject( layer.Value );
                JsonConvert.PopulateObject( json, this, new JsonSerializerSettings()
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    }
                );

                ApplyRules( values, finalMaps, finalOpcodes );

                if( layer.Value.Maps != null )
                    finalMaps.AddRange( layer.Value.Maps );

                if( layer.Value.Opcodes != null )
                    finalOpcodes.AddRange( layer.Value.Opcodes );
            }

            // todo: remove duplicates

            Maps = Maps ?? new List<string>();
            Maps.Clear();
            Maps.AddRange( finalMaps );

            Opcodes = Opcodes ?? new List<string>();
            Opcodes.Clear();
            Opcodes.AddRange( finalOpcodes );

            Rules = null;


            // validate some settings

            if( string.IsNullOrWhiteSpace( ProjectFolder ) )
                throw new Exception( "Property 'projectFolder' is missing or empty." );

            if( !Directory.Exists( ProjectFolder ) )
                throw new Exception( "Property 'projectFolder' refers to a folder that could not be found." );


            // get temp folder

            TempFolder = Path.Combine( ProjectFolder, ".zxdbg" );
            Directory.CreateDirectory( TempFolder );


            var logjson = JsonConvert.SerializeObject( this,
                new JsonSerializerSettings()
                {
                    NullValueHandling = NullValueHandling.Ignore
                }
            );

            Logging.Write( Logging.Severity.Debug, "Resolved settings: " + logjson );

            Deserialized();
        }

        void ApplyRules( HashSet<string> values, List<string> finalMaps, List<string> finalOpcodes )
        {
            if( Rules == null )
                return;

            foreach( var rule in Rules )
            {
                var elements = rule.Rule.Split( new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );

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
                    finalMaps.Clear();
                    finalMaps.AddRange( rule.SetMaps );
                }

                if( rule.AddMaps != null )
                    finalMaps.AddRange( rule.AddMaps );

                if( rule.SetOpcodes != null )
                {
                    finalOpcodes.Clear();
                    finalOpcodes.AddRange( rule.SetOpcodes );
                }

                if( rule.AddOpcodes != null )
                    finalOpcodes.AddRange( rule.AddOpcodes );
            }
        }

        public string Locate( string filename, string extFolder = null )
        {
            if( File.Exists( filename ) )
                return filename;

            string path;

            if( ProjectFolder != null )
            {
                path = Path.Combine( ProjectFolder, filename );
                if( File.Exists( path ) )
                    return path;
            }

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


        public static string EXEPath;
        public static string ExtensionPath;

        static Settings()
        {
            // get exe path & extension path for additional files
            var exe = new FileInfo( System.Reflection.Assembly.GetEntryAssembly().Location );
            var path = exe.Directory;

            EXEPath = path.FullName;

            if( path.Name == "bin" )
                path = path.Parent;

            ExtensionPath = path.FullName;
        }
    }
}
