using Spectrum;

namespace ZXDebug.SourceMap
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
            return new Address() { Location = pAddress };
        }
    }
}