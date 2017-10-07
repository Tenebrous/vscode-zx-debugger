namespace ZXDebug.SourceMapper
{
    public class File
    {
        public readonly string Filename;

        public File()
        {
        }

        public File( string filename )
        {
            Filename = filename;
        }

        public override string ToString()
        {
            return Filename;
        }

        public override bool Equals( object other )
        {
            return other is File && this == (File)other;
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

        public static bool operator ==( File left, File right )
        {
            return left.Filename == right.Filename;
        }

        public static bool operator !=( File left, File right )
        {
            return !( left == right );
        }
    }
}