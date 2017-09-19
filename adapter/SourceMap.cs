using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using Spectrum;

namespace ZXDebug
{
    /// <summary>
    /// Stores parsed information from a single memory map (e.g. .dbg)
    /// </summary>
    public class SourceMap
    {
        public string Filename;

        public SourceBanks Banks = new SourceBanks();
        public Sources Files = new Sources();

        public SourceMap( string pFilename )
        {
            // banktype_bankid addr(h) ("filename":line) (label1 (label2...)) (; comment)
            //
            // ROM_0 0000h start                 ; cold start restart
            // BANK_02 8000h "C:\Users\m\AppData\Local\Temp\zcc53216.asm":1367 __Start

            Filename = pFilename;

            var fileLine = new Regex(
                @"(?i)^(?'banktype'ROM|RAM|BANK|ALL)(?:_)?((?'bankid'\d*))? (?'addr'[0-9a-f]+h?)( (?'file'"".*?""):(?'line'\d*))?(?'labels'(?'label' [a-z0-9_]+)*)(?'comment' *;.*?)?$"
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

                        int bankID;
                        int.TryParse( bankIDStr, out bankID );

                        var address = Format.Parse( addressStr );

                        var bank = new BankID( bankTypeStr, bankID );
                        var symBank = Banks[bank];
                        var sym = symBank.Symbols[address];

                        var labelCaptures = labels.Captures;
                        sym.Labels = new string[labelCaptures.Count];
                        for( var i = 0; i < labelCaptures.Count; i++ )
                            sym.Labels[i] = labelCaptures[i].Value.Trim();

                        sym.Comment = commentStr.Trim();
                        if( sym.Comment.StartsWith( ";" ) )
                            sym.Comment = sym.Comment.Substring( 1 ).Trim();

                        if( !string.IsNullOrWhiteSpace( fileStr ) )
                        {
                            var file = Files[fileStr];
                            sym.File = file;
                            sym.Line = int.Parse( lineStr );
                        }

                        sym.Map = this;
                    }
                }
            }
        }

        public class SourceBanks : Cache<BankID, SourceBank>
        {
            public SourceBanks() : base( NewBank ) { }

            static SourceBank NewBank( BankID pBank )
            {
                return new SourceBank() { Bank = pBank };
            }
        }

        public class SourceBank
        {
            public BankID Bank;

            public Cache<ushort, SourceAddress> Symbols;

            public SourceBank()
            {
                Symbols = new Cache<ushort, SourceAddress>( NewSymbol );
            }

            SourceAddress NewSymbol( ushort pAddress )
            {
                return new SourceAddress() { Address = pAddress };
            }
        }

        public class SourceAddress
        {
            public SourceMap Map;
            public ushort    Address;
            public Source    File;
            public int       Line;
            public string[]  Labels;
            public string    Comment;
        }

        public class Sources : Cache<string, Source>
        {
            public Sources() : base( NewSource ) { }

            static Source NewSource( string pFile )
            {
                return new Source() { Filename = pFile };
            }
        }

        public class Source
        {
            public string Filename;
        }
    }

    /// <summary>
    /// A collection of Map files
    /// </summary>
    public class SourceMaps : List<SourceMap>
    {
        public SourceMap.SourceAddress Find( BankID pBank, ushort pAddress )
        {
            SourceMap.SourceAddress value = null;

            foreach( var map in this )
            {
                if( !map.Banks.TryGetValue( pBank, out var bank ) )
                    continue;

                if( !bank.Symbols.TryGetValue( pAddress, out value ) )
                    continue;
            }

            return value;
        }
    }
}
