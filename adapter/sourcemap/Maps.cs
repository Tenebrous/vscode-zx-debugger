using System.Collections.Generic;
using Spectrum;

namespace ZXDebug.SourceMapper
{
    /// <summary>
    /// A collection of Map files
    /// </summary>
    public class Maps : List<Map>
    {
        public string SourceRoot;
        public Files Files = new Files();
        
        public Address Find( BankID pBank, ushort pAddress )
        {
            foreach( var map in this )
                if( map.AddressToSource.TryGetValue( pBank, pAddress, out var pLine ) )
                    return new Address()
                    {
                        BankID = pBank,
                        Location = pAddress,
                        File = pLine.File,
                        Line = pLine.Line
                    };

            return null;
        }

        public Address FindRecentWithLabel( BankID pBank, ushort pAddress, ushort pMaxDistance = 0xFFFF )
        {
            Address result = null;
            ushort highest = 0;

            foreach( var map in this )
                if( map.Labels.TryGetValueOrBelow( pBank, pAddress, out var actualAddress, out var pLabels ) )
                    if( actualAddress >= highest && (pAddress - actualAddress) < pMaxDistance )
                        result = new Address()
                        {
                            BankID = pBank,
                            Location = actualAddress,
                            Labels = pLabels
                        };

            return result;
        }

        public Map Add( string pFilename )
        {
            var map = new Map( this, SourceRoot, pFilename );
            base.Add( map );

            return map;
        }
    }
}
