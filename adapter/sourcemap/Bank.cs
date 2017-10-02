using Spectrum;

namespace ZXDebug.SourceMapper
{
    public class Bank
    {
        public BankID ID;

        public SpatialDictionary<ushort, Address> Symbols;

        public Bank()
        {
            Symbols = new SpatialDictionary<ushort, Address>( NewSymbol );
        }

        Address NewSymbol( ushort pAddress )
        {
            return new Address() { BankID = ID, Location = pAddress };
        }

        public override string ToString()
        {
            return ID.ToString();
        }
    }
}