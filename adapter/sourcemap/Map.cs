using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Spectrum;
using ZXDebug.utils;

namespace ZXDebug.SourceMapper
{
    public class SourceLine
    {
        public File File;
        public int Line;
    }


    /// <summary>
    /// Stores parsed information from a single memory map (e.g. .dbg or .map)
    /// </summary>
    public class Map
    {
        public string SourceRoot;
        public string Filename;
        public Maps Maps;

        public SpatialDictionary<BankID, ushort, SourceLine> AddressToSource = new SpatialDictionary<BankID, ushort, SourceLine>();
        public SpatialDictionary<BankID, ushort, List<string>> Labels = new SpatialDictionary<BankID, ushort, List<string>>();

        Regex regexDbg = new Regex(
                @"(?i)^(?'bank'(ROM|RAM|BANK|DIV|ALL)(_)?(\d*)?) (?'addr'[0-9a-f]+h?)( (?'file'"".*?""):(?'line'\d*))?(?'labels'(?'label' [a-z0-9_]+)*)(?'comment' *;.*?)?$",
                RegexOptions.Compiled
            );

        Regex regexMap = new Regex(
                @"(?i)^(?'label'.*?)\s*=\s*(\$(?'addr'[0-9a-fA-F]*))\s?;\s(?'type'addr)\s*,\s*(?'scope'.*?)\s*,\s*(?'def'.*?)\s*,\s*(?'module'.*?)\s*,\s*(?'bank'.*?)\s*,\s*(?'file'.*):(?'line'\d*).*?$",
                RegexOptions.Compiled
            );

        /// <summary>
        /// Create a new map from the referenced file
        /// </summary>
        /// <param name="pParent">Mapper</param>
        /// <param name="pSourceRoot">Root folder for relative source paths</param>
        /// <param name="pFilename">File to read</param>
        public Map( Maps pParent, string pSourceRoot, string pFilename )
        {
            Maps = pParent;
            SourceRoot = pSourceRoot;
            Filename = pFilename;

            var ext = Path.GetExtension( pFilename )?.ToLower() ?? "";

            if( ext == ".dbg" )
                Parse( pFilename, regexDbg );
            else if( ext == ".map" )
                Parse( pFilename, regexMap );

            System.IO.File.WriteAllText(
                pFilename + ".address.json",
                JsonConvert.SerializeObject(
                    AddressToSource,
                    new JsonSerializerSettings() { Formatting = Formatting.Indented, ReferenceLoopHandling = ReferenceLoopHandling.Ignore }
                )
            );

            System.IO.File.WriteAllText(
                pFilename + ".labels.json",
                JsonConvert.SerializeObject(
                    Labels,
                    new JsonSerializerSettings() { Formatting = Formatting.Indented, ReferenceLoopHandling = ReferenceLoopHandling.Ignore }
                )
            );

        }


        List<string> _tempLabels = new List<string>();
        void Parse( string pFilename, Regex pRegex )
        {
            using( var reader = new StreamReader( pFilename ) )
            {
                string text;

                while( ( text = reader.ReadLine() ) != null )
                {
                    var matches = pRegex.Matches( text );

                    foreach( Match match in matches )
                    {
                        var bankStr    = match.Groups["bank"].Value;
                        var addressStr = match.Groups["addr"].Value;
                        var fileStr    = match.Groups["file"].Value;
                        var lineStr    = match.Groups["line"].Value;
                        var labelStr   = match.Groups["label"].Value;
                        var labelsGrp  = match.Groups["labels"];
                        var commentStr = match.Groups["comment"].Value;
                        var typeStr    = match.Groups["type"].Value;
                        var scopeStr   = match.Groups["scope"].Value;
                        var defStr     = match.Groups["def"].Value;
                        var moduleStr  = match.Groups["module"].Value;

                        if( labelStr.StartsWith( "__ASM_LINE_" ) 
                            || labelStr.StartsWith( "__C_LINE_" ) 
                            || labelStr.StartsWith( "__CLINE_" ) )
                            labelStr = null;

                        _tempLabels.Clear();

                        if( !string.IsNullOrWhiteSpace(labelStr) )
                            _tempLabels.Add( labelStr );

                        for( var i = 0; i < labelsGrp.Captures.Count; i++ )
                            _tempLabels.Add( labelsGrp.Captures[i].Value.Trim() );

                        SaveData( bankStr, addressStr, fileStr, lineStr, _tempLabels );
                    }
                }
            }
        }

        void SaveData( string pBankStr, string pAddressStr, string pFileStr, string pLineStr, List<string> pLabels )
        {
            var bank = new BankID( pBankStr );
            var address = Format.Parse( pAddressStr, pKnownHex : true );

            if( pLabels != null && pLabels.Count > 0 )
            {
                Labels.TryAdd( bank, address, out var labels );
                labels.AddRange( pLabels );
            }

            if( !string.IsNullOrWhiteSpace( pFileStr ) )
            {
                var normalisedFileStr = Path.GetFullPath( Path.Combine( SourceRoot, pFileStr ) );
                var line = int.Parse( pLineStr );

                Maps.Files.TryAdd( normalisedFileStr, out var file );

                if( !AddressToSource.TryAdd( bank, address, out var sym,
                        ( pBank, pAddress ) => new SourceLine() { File = file, Line = line }
                    )
                )
                {
                    // TryAdd returns false if item was already there, true if it was added

                    if( sym.File != file )
                    {
                        sym.File = file;
                        sym.Line = line;
                    }
                    else if( line > sym.Line )
                    {
                        sym.Line = line;
                    }
                }

                //SourceLink store;

                //FileLine.TryAdd( file, out var fileLine );
                //if( fileLine.TryAdd( line, out var fileLineRange, out store ) )
                //{
                //    store.File = file;
                //    store.BankID = bank;
                //    store.LowerLine = int.MaxValue;
                //    store.LowerAddress = ushort.MaxValue;
                //}
                //fileLine.Extend( fileLineRange, line );
                //if( line < store.LowerLine ) store.LowerLine = line;
                //if( line > store.UpperLine ) store.UpperLine = line;
                //if( address < store.LowerAddress ) store.LowerAddress = address;
                //if( address > store.UpperAddress ) store.UpperAddress = address;

                //BankAddress.TryAdd( bank, out var bankAddress );
                //if( bankAddress.TryAdd( address, out var bankAddressRange, out store ) )
                //{
                //    store.File = file;
                //    store.BankID = bank;
                //    store.LowerLine = int.MaxValue;
                //    store.LowerAddress = ushort.MaxValue;
                //}
                //bankAddress.Extend( bankAddressRange, address );
                //if( line < store.LowerLine ) store.LowerLine = line;
                //if( line > store.UpperLine ) store.UpperLine = line;
                //if( address < store.LowerAddress ) store.LowerAddress = address;
                //if( address > store.UpperAddress ) store.UpperAddress = address;
            }
        }

        static string Dequote( string pValue )
        {
            if( pValue.StartsWith( "\"" ) && pValue.EndsWith( "\"" ) )
                return pValue.Substring( 1, pValue.Length - 2 );

            return pValue;
        }

        static string Decode( string pValue )
        {
            // replace _(hex) values with ascii equivelant

            var result = "";

            for( int i = 0; i < pValue.Length; i++ )
            {
                if( pValue[i] == '_' )
                {
                    var hex = Convert.ToUInt32( pValue.Substring(i+1,2), 16 );
                    i += 2;
                    result += (char)hex;
                }
                else
                    result += pValue[i];
            }

            return result.Trim();
        }

        public override string ToString()
        {
            return Filename;
        }
    }
     
    public class SourceLink
    {
        public File File;
        public int LowerLine;
        public int UpperLine;

        public BankID BankID;
        public ushort LowerAddress;
        public ushort UpperAddress;
    }

    public class AddressMatches : RangeDictionary<ushort, SourceLink>
    {
    }

    public class LineMatches : RangeDictionary<int, SourceLink>
    {
    }
}
