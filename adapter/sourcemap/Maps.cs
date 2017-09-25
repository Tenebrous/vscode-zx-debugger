using System.Collections.Generic;
using Spectrum;

namespace ZXDebug.SourceMap
{
    /// <summary>
    /// A collection of Map files
    /// </summary>
    public class Maps : List<Map>
    {
        public Address Find( BankID pBank, ushort pAddress )
        {
            Address value = null;

            foreach( var map in this )
            {
                if( !map.Banks.TryGetValue( pBank, out var bank ) )
                    continue;

                if( !bank.Symbols.TryGetValue( pAddress, out value ) )
                    continue;
            }

            return value;
        }
    }
}