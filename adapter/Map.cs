using System.IO;
using System.Text.RegularExpressions;
using Spectrum;

namespace ZXDebug
{
    /// <summary>
    /// Stores parsed information from a memory map (e.g. .dbg)
    /// </summary>
    public class Map
    {
        public SymbolBanks Banks = new SymbolBanks();
        public SymbolFiles Files = new SymbolFiles();

        public Map( string pFilename )
        {
            // ROM_0 0000h start                 ; cold start restart
            // BANK_02 8000h "C:\Users\m\AppData\Local\Temp\zcc53216.asm":1367 __Start

            var fileLine = new Regex(
                @"(?i)^(?'banktype'ROM|BANK)_(?'bankid'\d*) (?'addr'[0-9a-f]+h?)( (?'file'"".*?""):(?'line'\d*))?(?'labels'(?'label' [a-z0-9_]+)*)(?'comment' *;.*?)?$"
            );

            using( var reader = new StreamReader( pFilename ) )
            {
                string text;

                while( ( text = reader.ReadLine() ) != null )
                {
                    var matches = fileLine.Matches( text );
                    foreach( Match match in matches )
                    {
                        var bankTypeStr = match.Groups["banktype"].Value;
                        var bankIDStr   = match.Groups["bankid"].Value;
                        var addressStr  = match.Groups["addr"].Value;
                        var fileStr     = match.Groups["file"].Value;
                        var lineStr     = match.Groups["line"].Value;
                        var labels      = match.Groups["labels"];
                        var commentStr  = match.Groups["comment"].Value;

                        if( labels.Length == 0 )
                            continue;

                        var bankID = int.Parse( bankIDStr );
                        var address = Format.Parse( addressStr );

                        var bank = new BankID( bankTypeStr, bankID );
                        var symBank = Banks[bank];
                        var sym = symBank.Symbols[address];

                        var labelCaptures = labels.Captures;
                        sym.Labels = new string[labelCaptures.Count];
                        for( var i = 0; i < labelCaptures.Count; i++ )
                            sym.Labels[i] = labelCaptures[i].Value;

                        sym.Comment = commentStr;

                        if( !string.IsNullOrWhiteSpace( fileStr ) )
                        {
                            var file = Files[fileStr];
                            sym.File = file;
                            sym.Line = int.Parse( lineStr );
                        }
                    }
                }
            }
        }
    }

    public class SymbolBanks : Cache<BankID, SymbolBank>
    {
        public SymbolBanks() : base( NewBank ) { }

        static SymbolBank NewBank( BankID pBank )
        {
            return new SymbolBank() { Bank = pBank };
        }
    }

    public class SymbolBank
    {
        public BankID Bank;

        public Cache<ushort, SymbolAddress> Symbols;

        public SymbolBank()
        {
            Symbols = new Cache<ushort, SymbolAddress>( NewSymbol );
        }

        SymbolAddress NewSymbol( ushort pAddress )
        {
            return new SymbolAddress() { Address = pAddress };
        }
    }

    public class SymbolAddress
    {
        public ushort Address;
        public SymbolFile File;
        public int Line;
        public string[] Labels;
        public string Comment;
    }

    public class SymbolFiles : Cache<string, SymbolFile>
    {
        public SymbolFiles() : base( NewFile ) { }

        static SymbolFile NewFile( string pFile )
        {
            return new SymbolFile() { Filename = pFile };
        }
    }

    public class SymbolFile
    {
        public string Filename;
    }
}
