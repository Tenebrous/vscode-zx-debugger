using System.Collections.Generic;
using Spectrum;

namespace ZXDebug.SourceMapper
{
    public class AddressDetails
    {
        public BankID      Bank;

        public ushort      Address;
        public FileLine    Source;

        public ushort      LabelledAddress;
        public FileLine    LabelledSource;
        public List<Label> Labels;

        public string GetRelativeText()
        {
            string text = Labels != null ? Labels[0].Name : "";
            string offset = null;

            //if( LabelledSource != null && Source != null
            // && LabelledSource.File == Source.File 
            // && LabelledSource.Line != Source.Line )
            //{ 
            //    offset = $"+{Source.Line - LabelledSource.Line}";
            //}
            //else 
            if( LabelledAddress != Address )
            {
                offset = $"+{Address - LabelledAddress}";
            }

            if( Convert.Settings.LabelPosition == Convert.FormatSettings.LabelPositionEnum.Left )
                return $"{text}{offset} {Address.ToHex()}";
            else
                return $"{Address.ToHex()} {text}{offset}";
        }
    }
}