﻿using Newtonsoft.Json;

namespace VSCode
{
    public class Settings
    {
        public delegate void DeserializingHandler( Settings pSettings );
        public event DeserializingHandler DeserializingEvent;

        public delegate void DeserializedHandler( Settings pSettings );
        public event DeserializedHandler DeserializedEvent;

        public virtual void FromJSON( string pJSON )
        {
            DeserializingEvent?.Invoke( this );

            // apply basic settings
            JsonConvert.PopulateObject( pJSON, this, new JsonSerializerSettings() {});

            var wrapper = new Wrapper() { workspaceConfiguration = this };
            JsonConvert.PopulateObject( pJSON, wrapper );

            DeserializedEvent?.Invoke( this );

            Log.Write( Log.Severity.Message, JsonConvert.SerializeObject( this, Formatting.Indented ) );
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
