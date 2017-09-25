namespace ZXDebug.SourceMap
{
    public class Files : Cache<string, File>
    {
        public Files() : base( NewSource ) { }

        static File NewSource( string pFile )
        {
            return new File() { Filename = pFile };
        }
    }
}