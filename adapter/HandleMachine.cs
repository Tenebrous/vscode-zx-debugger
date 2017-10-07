namespace ZXDebug
{
    public class HandleMachine
    {
        public Session Session;

        public HandleMachine( Session session )
        {
            Session = session;
        }

        public void Configure()
        {
            Session.Machine.PausedEvent             += Paused;
            Session.Machine.ContinuedEvent          += Continued;
            Session.Machine.DisassemblyUpdatedEvent += DisassemblyUpdated;
        }

        void Paused()
        {
            Session.VSCode.Stopped( 1, "step", "step" );

            //TestHeatMap();
        }

        void Continued()
        {
            Session.VSCode.Continued( true );
        }

        void DisassemblyUpdated()
        {
            _needVSCodeRefresh = true;
        }
    }
}
