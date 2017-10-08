using Spectrum;
using ZXDebug;

public class Session
{
    public Settings Settings;
    public MachineConnection MachineConnection;

    public VSCode.Connection VSCode;
    public HandleVSCode HandleVSCode;

    public Machine Machine;
    public HandleMachine HandleMachine;

    public ValueTree Values;
    public HandleValueTree HandleValueTree;

    public bool Running = true;

    public bool EventLoop()
    {
        var vsactive = VSCode.Process();
        var dbgactive = MachineConnection.Process();

        if( !vsactive )
        {
            if( VSCode.NeedRefresh )
            {
                System.Threading.Thread.Sleep( 150 );
                VSCode.Refresh();
                VSCode.NeedRefresh = false;
            }
            else
            {
                System.Threading.Thread.Sleep( 1 );
            }
        }

        return Running;
    }
}
