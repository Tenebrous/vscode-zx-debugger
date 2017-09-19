using System;
using System.IO;

namespace ZXDebug
{
    public class Settings : VSCode.Settings
    {
        // ReSharper disable InconsistentNaming
        public string   cwd;
        public string[] sourceMaps;
        public string[] opcodeTables;
        public bool     stopOnEntry;
        public string   hexPrefix;
        public string   hexSuffix;
        // ReSharper restore InconsistentNaming

        public string ExtensionPath;

        public Settings()
        {
            // fill in defaults if required
        }

        public override void Validate()
        {
            // check cwd

            if( string.IsNullOrWhiteSpace( cwd ) )
                throw new Exception( "Property 'cwd' is missing or empty." );

            if( !Directory.Exists( cwd ) )
                throw new Exception( "Property 'cwd' refers to a folder that could not be found." );


            // get extension path for additional files

            var exe = new FileInfo( System.Reflection.Assembly.GetEntryAssembly().Location );
            var path = exe.Directory;
            
            if( path.Name == "bin" )
                path = path.Parent;

            ExtensionPath = path.FullName;

            Log.Write( Log.Severity.Message, "Extension path: " + ExtensionPath );
        }
    }
}
