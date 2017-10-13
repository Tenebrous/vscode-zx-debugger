using System.Collections.Generic;
using Newtonsoft.Json;

namespace ZXDebug
{
    public class OpcodeTable
    {
        [JsonIgnore] public string ID;

        public Dictionary<byte, string> SubTables = new Dictionary<byte, string>();
        public Dictionary<byte, string> Opcodes = new Dictionary<byte, string>();
    }
}