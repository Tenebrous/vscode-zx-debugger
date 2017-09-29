using Spectrum;

namespace ZXDebug.SourceMapper
{
    public class Bank
    {
        public BankID ID;

        public Cache<ushort, Address> Symbols;

        public Bank()
        {
            Symbols = new Cache<ushort, Address>( NewSymbol );
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