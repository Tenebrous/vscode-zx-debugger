using System.Collections.Generic;
using Spectrum;

namespace ZXDebug.SourceMapper
{
    /// <summary>
    /// A collection of Map files
    /// </summary>
    public class Maps : List<Map>
    {
        public class GetLabelsResult
        {
            public BankID Bank;
            public ushort Address;
            public List<Label> Labels;
        }

        public string SourceRoot;
        public Files Files = new Files();


        public GetLabelsResult GetLabelsAt( BankID bankId, ushort address, ushort maxDistance = 0 )
        {
            GetLabelsResult result = null;

            ushort highest = 0;
            foreach( var map in this )
            {
                if( !map.Labels.TryGetValueOrBelow( bankId, address, out var actualAddress, out var labels ) )
                    continue;

                if( actualAddress < highest || address - actualAddress > maxDistance )
                    continue;

                highest = actualAddress;

                result = new GetLabelsResult()
                {
                    Bank = bankId,
                    Address = address,
                    Labels = labels
                };
            }

            return result;
        }

        public AddressDetails GetAddressDetails( BankID bankId, ushort address, ushort maxDistance = 0 )
        {
            var result = new AddressDetails()
            {
                Bank = bankId,
                Address = address
            };

            ushort highestLabel = 0;

            foreach( var map in this )
            {
                if( result.Source == null && map.Source.TryGetValue( bankId, address, out var source ) )
                    result.Source = source;

                if( map.Labels.TryGetValueOrBelow( bankId, address, out var actualAddress, out var labels ) )
                {
                    if( actualAddress >= highestLabel && address - actualAddress <= maxDistance )
                    {
                        highestLabel = actualAddress;
                        result.Labels = labels;
                        result.LabelledAddress = actualAddress;
                    }

                    if( actualAddress == address )
                        result.LabelledSource = result.Source;
                    else
                        map.Source.TryGetValue( bankId, actualAddress, out result.LabelledSource );
                }
            }

            //Logging.Write( Logging.Severity.Message, result.ToString() );

            return result;
        }

        public Map Add( string filename )
        {
            var map = new Map( this, SourceRoot, filename );
            base.Add( map );

            return map;
        }

        public LabelLocation GetLabel( string label )
        {
            foreach( var map in this )
                if( map.ByLabel.TryGetValue( label, out var result ) )
                    if( result.Count > 0 )
                        return result[0];

            return null;
        }
    }
}
