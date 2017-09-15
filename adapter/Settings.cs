using System;
using System.IO;

namespace ZXDebug
{
    public class Settings : VSCode.Settings
    {
        public string   cwd;
        public string[] maps;
        public bool     stopOnEntry;

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
        }
    }
}
