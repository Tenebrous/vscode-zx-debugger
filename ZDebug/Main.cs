using System;
using System.IO;

namespace ZDebug
{
    public class ZMain
	{
		static void Main(string[] argv)
		{
            File.WriteAllText("C:\\Temp\\log.log", "started\n");

            var debug = new Session();

		    debug.Run();

		    Log("exit");
        }

	    static object logLock = new object();
	    public static void Log(string pMessage)
	    {
	        lock( logLock )
	        {
	            File.AppendAllText( "C:\\Temp\\log.log", pMessage + "\n" );
	        }
	    }
    }
}

