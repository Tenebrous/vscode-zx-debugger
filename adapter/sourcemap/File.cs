namespace ZXDebug.SourceMapper
{
    public class File
    {
        public readonly string Filename;

        public File()
        {
        }

        public File( string pFilename )
        {
            Filename = pFilename;
        }

        public override string ToString()
        {
            return Filename;
        }

        public override bool Equals( object pOther )
        {
            return pOther is File && this == (File)pOther;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Filename.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==( File x, File y )
        {
            return x.Filename == y.Filename;
        }

        public static bool operator !=( File x, File y )
        {
            return !( x == y );
        }
    }
}