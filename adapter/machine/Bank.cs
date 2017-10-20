using System.Text;

namespace Spectrum
{
    public class Bank
    {
        public BankID ID;
        public bool   IsPagedIn;
        public ushort PagedAddress;
        public ushort Length;
        public string Name => ID.ToString();

        public override string ToString()
        {
            return $"{PagedAddress:X4}:{ID}";
        }
    }
}