using System;

namespace ZXDebug.SourceMapper
{
    public class Files : Cache<string, File>
    {
        // case-insensitive filenames? probably ok for now
        public Files() : base( NewSource, StringComparer.OrdinalIgnoreCase ) { }

        static File NewSource( string pFile )
        {
            return new File() { Filename = pFile };
        }
    }
}