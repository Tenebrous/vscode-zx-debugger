using System.Collections.Generic;
using System.Runtime.Remoting.Contexts;
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
            foreach( var map in this )
                if( map.Banks.TryGetValue( pBank, out var bank ) )
                    if( bank.Symbols.TryGetValue( pAddress, out var value ) )
                        return value;

            return null;
        }

        public Address FindPreviousLabel( BankID pBank, ushort pAddress )
        {
            Address result = null;
            ushort highest = 0;

            foreach( var map in this )
            {
                if( !map.Banks.TryGetValue( pBank, out var bank ) )
                    continue;

                foreach( var s in bank.Symbols )
                {
                    var sym = s.Value;

                    if( sym.Location <= highest || sym.Location > pAddress )
                        continue;

                    if( sym.Labels == null || sym.Labels.Count <= 0 )
                        continue;

                    highest = sym.Location;
                    result = sym;
                }
            }

            return result;
        }
    }
}