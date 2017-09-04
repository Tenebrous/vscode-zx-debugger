using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ZDebug
{
    public class ConnectionZEsarUX
    {
        TcpClient _client;
        NetworkStream _stream;

        bool _step = false;

        public bool Start()
        {
            _client = new TcpClient();

            try
            {
                ZMain.Log( "zesarux: connecting..." );
                _client.Connect( "127.0.0.1", 10000 );
            }
            catch (Exception e)
            {
                ZMain.Log( "zesarux: error " + e );
                return false;
            }

            _stream = _client.GetStream();

            ZMain.Log( "zesarux: connected." );

            return true;
        }

        public void Stop()
        {
            ZMain.Log("zesarux: disconnecting...");

            Read();

            if ( _stream != null )
            {
                _stream.Close();
                _stream = null;
            }

            if( _client != null )
            {
                _client.Close();
                _client = null;
            }

            ZMain.Log("zesarux: disconnecting.");
        }

        StringBuilder s = new StringBuilder();

        public bool Read()
        {
            if( _client == null || _stream == null )
                if( !Start() )
                    return false;

            while( _stream.DataAvailable )
                s.Append((char)_stream.ReadByte());

            if( s.Length > 0 )
            {
                var data = s.ToString().Split( '\n' );

                foreach( var line in data )
                {
                    ZMain.Log( "zesarux: <- [" + line + "]" );
                    Process( line );
                }

                s.Clear();
            }

            return true;
        }

        public void Process( string pResult )
        {
            switch( pResult )
            {
                case "command> ":
                    _step = false;
                    break;

                case "command@cpu-step> ":
                    _step = true;
                    break;
            }
        }

        public bool Send( string pMessage )
        {
            if (_client == null || _stream == null)
                if (!Start())
                    return false;

            ZMain.Log("zesarux: -> [" + pMessage + "]");

            var bytes = System.Text.Encoding.ASCII.GetBytes( pMessage + "\n\r" );
            _stream.Write( bytes, 0, bytes.Length );
            _stream.Flush();

            Thread.Sleep( 100 );

            Read();

            return true;
        }

        public bool IsConnected
        {
            get { return _client != null && _client.Connected; }
        }
    }
}
