using Newtonsoft.Json;

namespace VSCode
{
    public class Settings
    {
        public delegate void DeserializingHandler( Settings pSettings );
        public event DeserializingHandler DeserializingEvent;

        public delegate void DeserializedHandler( Settings pSettings );
        public event DeserializedHandler DeserializedEvent;

        public virtual void FromJSON( string json )
        {
            DeserializingEvent?.Invoke( this );

            // apply basic settings
            JsonConvert.PopulateObject( json, this, new JsonSerializerSettings() );

            var wrapper = new Wrapper() { workspaceConfiguration = this };
            JsonConvert.PopulateObject( json, wrapper );

            DeserializedEvent?.Invoke( this );
        }

        public virtual void Validate()
        {
        }
    }

    class Wrapper
    {
        public Settings workspaceConfiguration;
    }
}
