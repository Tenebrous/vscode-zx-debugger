using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ZDebug
{
    public class ConnectionZEsarUX
    {
        public Action OnPaused;
        public Action OnContinued;

        TcpClient _client;
        NetworkStream _stream;
        bool _connected;
        bool _paused;
        
        public bool Start()
        {
            if( _connected )
                return true;

            _client = new TcpClient();

            try
            {
                ZMain.Log( "zesarux: connecting..." );
                _client.Connect( "127.0.0.1", 10000 );
                _stream = _client.GetStream();
                _connected = true;
                ZMain.Log( "zesarux: connected." );
            }
            catch ( Exception e )
            {
                ZMain.Log( "zesarux: error " + e );
                return false;
            }

            return _connected;
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

        public void Pause()
        {
            Send("enter-cpu-step");
        }

        public void Continue()
        {
            Send("exit-cpu-step");
        }


        StringBuilder _s = new StringBuilder();

        public bool Read()
        {
            if( !Start() )
                return false;

            while( _stream.DataAvailable )
                _s.Append((char)_stream.ReadByte());

            if( _s.Length > 0 )
            {
                var data = _s.ToString().Split( '\n' );

                foreach( var line in data )
                {
                    ZMain.Log( "zesarux: <- [" + line + "]" );
                    Process( line );
                }

                _s.Clear();
            }

            return true;
        }

        public void Process( string pResult )
        {
            switch( pResult )
            {
                case "command> ":

                    if( _paused )
                        OnContinued?.Invoke();

                    _paused = false;
                    ZMain.Log( "zesarux: is running" );

                    break;

                case "command@cpu-step> ":

                    if( !_paused )
                        OnPaused?.Invoke();

                    _paused = true;
                    ZMain.Log("zesarux: is paused");

                    break;
            }
        }

        public bool Send( string pMessage )
        {
            if (!Start())
                return false;

            ZMain.Log("zesarux: -> [" + pMessage + "]");

            var bytes = System.Text.Encoding.ASCII.GetBytes( pMessage + "\n\n" );
            _stream.Write( bytes, 0, bytes.Length );
            _stream.Flush();

            Thread.Sleep( 150 );

            Read();

            return true;
        }

        public bool IsConnected
        {
            get { return _client != null && _client.Connected; }
        }
    }
}
