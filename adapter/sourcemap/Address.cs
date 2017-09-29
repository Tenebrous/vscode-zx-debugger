using System.Collections.Generic;
using Spectrum;

namespace ZXDebug.SourceMapper
{
    /// <summary>
    /// Represents a memory address mapped to a source file & line number, including any additional information from the source and map files.
    /// </summary>
    public class Address
    {
        /// <summary>
        /// Map which holds this address
        /// </summary>
        public Map          Map;
        
        /// <summary>
        /// BankID for this line
        /// </summary>
        public BankID       BankID;

        /// <summary>
        /// Memory location
        /// </summary>
        public ushort       Location;

        /// <summary>
        /// Source file
        /// </summary>
        public File         File;

        /// <summary>
        /// Line in source file
        /// </summary>
        public int          Line;

        /// <summary>
        /// Any labels attached to this address
        /// </summary>
        public List<string> Labels;

        /// <summary>
        /// Any additional comment provided
        /// </summary>
        public string       Comment;
            
        public override string ToString()
        {
            if( Labels == null )
                return $"{Location:X4} {Comment}";

            return $"{Location:X4} {string.Join( ",", Labels )} {Comment}";
        }
    }
}
