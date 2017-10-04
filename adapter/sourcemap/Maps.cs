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


        public GetLabelsResult GetLabels( BankID pBank, ushort pAddress, ushort pMaxDistance = 0 )
        {
            GetLabelsResult result = null;

            ushort highest = 0;
            foreach( var map in this )
            {
                if( !map.Labels.TryGetValueOrBelow( pBank, pAddress, out var actualAddress, out var labels ) )
                    continue;

                if( actualAddress < highest || pAddress - actualAddress > pMaxDistance )
                    continue;

                highest = actualAddress;

                result = new GetLabelsResult()
                {
                    Bank = pBank,
                    Address = pAddress,
                    Labels = labels
                };
            }

            return result;
        }

        public AddressDetails GetAddressDetails( BankID pBank, ushort pAddress, ushort pMaxLabelDistance = 0 )
        {
            var result = new AddressDetails()
            {
                Bank = pBank,
                Address = pAddress
            };

            ushort highestLabel = 0;

            foreach( var map in this )
            {
                if( result.Source == null && map.Source.TryGetValue( pBank, pAddress, out var source ) )
                    result.Source = source;

                if( map.Labels.TryGetValueOrBelow( pBank, pAddress, out var actualAddress, out var labels ) )
                {
                    if( actualAddress >= highestLabel && pAddress - actualAddress <= pMaxLabelDistance )
                    {
                        highestLabel = actualAddress;
                        result.Labels = labels;
                        result.LabelledAddress = actualAddress;
                    }

                    if( actualAddress == pAddress )
                        result.LabelledSource = result.Source;
                    else
                        map.Source.TryGetValue( pBank, actualAddress, out result.LabelledSource );
                }
            }

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
