using System.Collections.Generic;
using System.Runtime.InteropServices;
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
            //foreach( var map in this )
            //{
            //    if( map.BankAddress.TryGetValue( pBank, out var address ) )
            //        if( address.TryGetValue( pAddress, out var range, out var link ) )
            //            return link;
            //}
            //foreach( var map in this )
            //    if( map.Banks.TryGetValue( pBank, out var bank ) )
            //        if( bank.Symbols.TryGetValue( pAddress, out var value ) )
            //            return value;

            return null;
        }

        public Address FindRecentWithLabel( BankID pBank, ushort pAddress, ushort pMaxDistance = 0xFFFF )
        {
            Address result = null;
            ushort highest = 0;

            //foreach( var map in this )
            //{
            //    if( !map.Banks.TryGetValue( pBank, out var bank ) )
            //        continue;

            //    foreach( var s in bank.Symbols )
            //    {
            //        var sym = s.Value;

            //        if( sym.Location <= highest || sym.Location > pAddress || ( pAddress - sym.Location ) > pMaxDistance )
            //            continue;

            //        if( sym.Labels == null || sym.Labels.Count <= 0 )
            //            continue;

            //        highest = sym.Location;
            //        result = sym;
            //    }
            //}

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
