namespace Spectrum
{
    public class Bank
    {
        public BankID ID;
        public bool   IsPagedIn;
        public ushort LastAddress;
        public string Name => ID.ToString();
    }
}