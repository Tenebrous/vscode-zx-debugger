using Spectrum;

namespace ZXDebug.SourceMapper
{
    public class Bank
    {
        public BankID ID;

        public Bank()
        {
        }

        public override string ToString()
        {
            return ID.ToString();
        }
    }
}
