using Spectrum;

namespace ZXDebug.SourceMap
{
    public class Banks : Cache<BankID, Bank>
    {
        public Banks() : base( NewBank ) { }

        static Bank NewBank( BankID pBank )
        {
            return new Bank() { ID = pBank };
        }
    }
}