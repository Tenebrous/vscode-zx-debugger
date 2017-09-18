using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Spectrum;

namespace ZXDebug
{
    /// <summary>
    /// Stores parsed information from a single memory map (e.g. .dbg)
    /// </summary>
    public class Map
    {
        public MapBanks Banks = new MapBanks();
        public MapSources Files = new MapSources();

        public Map( string pFilename )
        {
            // banktype_bankid addr(h) ("filename":line) (label1 (label2...)) (; comment)
            //
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

        public class MapBanks : Cache<BankID, MapBank>
        {
            public MapBanks() : base( NewBank ) { }

            static MapBank NewBank( BankID pBank )
            {
                return new MapBank() { Bank = pBank };
            }
        }

        public class MapBank
        {
            public BankID Bank;

            public Cache<ushort, MapAddress> Symbols;

            public MapBank()
            {
                Symbols = new Cache<ushort, MapAddress>( NewSymbol );
            }

            MapAddress NewSymbol( ushort pAddress )
            {
                return new MapAddress() { Address = pAddress };
            }
        }

        public class MapAddress
        {
            public ushort Address;
            public MapSource File;
            public int Line;
            public string[] Labels;
            public string Comment;
        }

        public class MapSources : Cache<string, MapSource>
        {
            public MapSources() : base( NewSource ) { }

            static MapSource NewSource( string pFile )
            {
                return new MapSource() { Filename = pFile };
            }
        }

        public class MapSource
        {
            public string Filename;
        }
    }

    /// <summary>
    /// A collection of Map files
    /// </summary>
    public class Maps : List<Map>
    {
        public Map.MapAddress Find( BankID pBank, ushort pAddress )
        {
            Map.MapAddress value = null;

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
