using Spectrum;

namespace ZXDebug.SourceMapper
{
    public class Banks : Cache<BankID, Bank>
    {
        public Banks() : base( NewBank ) { }

        static Bank NewBank( BankID bankId )
        {
            return new Bank() { ID = bankId };
        }
    }
}