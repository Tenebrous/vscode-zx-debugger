using System.Collections.Generic;

namespace ZXDebug.SourceMapper
{
    public class File
    {
        public string Filename;

        public Dictionary<int, Address> Lines = new Dictionary<int, Address>();

        public override string ToString()
        {
            return Filename;
        }
    }
}