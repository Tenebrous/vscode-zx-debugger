using Newtonsoft.Json;

namespace VSCode
{
    public class Settings
    {
        public virtual void FromJSON( string pJSON )
        {
            JsonConvert.PopulateObject( pJSON, this );
        }

        public virtual void Validate()
        {
        }
    }
}
