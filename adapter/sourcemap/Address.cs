using System.Collections.Generic;

namespace ZXDebug.SourceMap
{
    public class Address
    {
        public Map          Map;
        public ushort       Location;
        public File         File;
        public int          Line;
        public List<string> Labels;
        public string       Comment;
    }
}
