using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Spectrum;
using ZXDebug.utils;

namespace ZXDebug.SourceMapper
{
    public class FileLine
    {
        public File File;
        public int Line;

        public FileLine()
        {
        }

        public FileLine( File pFile, int pLine )
        {
            File = pFile;
            Line = pLine;
        }

        public override string ToString()
        {
            return $"{File}:{Line}";
        }

        public override bool Equals( object pOther )
        {
            if( object.ReferenceEquals( pOther, null ) )
                return false;

            if( object.ReferenceEquals( this, pOther ) )
                return true;

            if( GetType() != pOther.GetType() )
                return false;

            var other = (FileLine)pOther;

            return File == other.File && Line == other.Line;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // ReSharper disable NonReadonlyMemberInGetHashCode
                int hash = 17;
                hash = hash * 23 + File.GetHashCode();
                hash = hash * 23 + Line.GetHashCode();
                return hash;
                // ReSharper restore NonReadonlyMemberInGetHashCode
            }
        }

        public static bool operator ==( FileLine x, FileLine y )
        {
            if( object.ReferenceEquals( x, null ) )
            {
                if( object.ReferenceEquals( y, null ) )
                    return true;

                return false;
            }

            return x.Equals( y );
        }

        public static bool operator !=( FileLine x, FileLine y )
        {
            return !( x == y );
        }
    }

    public class Label
    {
        public string Name;
        public string Comment;
    }


    /// <summary>
    /// Stores parsed information from a single memory map (e.g. .dbg or .map)
    /// </summary>
    public class Map
    {
        public string SourceRoot;
        public string Filename;
        public Maps Maps;

        public SpatialDictionary<BankID, ushort, FileLine> Source = new SpatialDictionary<BankID, ushort, FileLine>();
        public SpatialDictionary<BankID, ushort, List<Label>> Labels = new SpatialDictionary<BankID, ushort, List<Label>>();

        Regex _regexDbg = new Regex(
                @"(?i)^(?'bank'(ROM|RAM|BANK|DIV|ALL)(_)?(\d*)?) (?'addr'[0-9a-f]+h?)( (?'file'"".*?""):(?'line'\d*))?(?'labels'(?'label' [a-z0-9_]+)*)( *;(?'comment'.*?))?$",
                RegexOptions.Compiled
            );

        Regex _regexMap = new Regex(
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
                Parse( pFilename, _regexDbg );
            else if( ext == ".map" )
                Parse( pFilename, _regexMap );
        }


        List<Label> _tempLabels = new List<Label>();
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

                        if( string.IsNullOrWhiteSpace( commentStr ) )
                            commentStr = null;
                        else
                            commentStr = commentStr.Trim();

                        _tempLabels.Clear();

                        if( labelsGrp.Captures.Count == 0 )
                            if( !string.IsNullOrWhiteSpace(labelStr) )
                                _tempLabels.Add( new Label() { Name = labelStr, Comment = commentStr } );

                        for( var i = 0; i < labelsGrp.Captures.Count; i++ )
                            _tempLabels.Add( new Label() { Name = labelsGrp.Captures[i].Value.Trim(), Comment = commentStr } );

                        AddMapping( bankStr, addressStr, fileStr, lineStr, _tempLabels );
                    }
                }
            }
        }

        void AddMapping( string pBankStr, string pAddressStr, string pFileStr, string pLineStr, List<Label> pLabels )
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

                if( !Source.TryAdd( bank, address, out var sym,
                        ( pBank, pAddress ) => new FileLine(file, line)
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
