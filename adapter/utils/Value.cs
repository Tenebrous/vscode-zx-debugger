using System;
using System.Collections.Generic;

namespace ZXDebug
{
    public class Value
    {
        public int ID;
        public string Name;
        public Value Parent;
        public List<Value> Children = new List<Value>();

        public Action<Value, string, string> OnChange;

        Dictionary<int, Value> _all;
        Dictionary<string, Value> _allByName;
        Dictionary<int, Value> _children;
        Dictionary<string, Value> _childrenByName;
        
        public Value()
        {
            _all = new Dictionary<int, Value>();
            _allByName = new Dictionary<string, Value>( StringComparer.InvariantCultureIgnoreCase );
            _children = new Dictionary<int, Value>();
            _childrenByName = new Dictionary<string, Value>( StringComparer.InvariantCultureIgnoreCase );
        }

        Value( Value pParent, ValueRefresher pRefresher = null, ValueGetter pGet = null, ValueSetter pSet = null, Value.ValueFormatter pFormatter = null )
        {
            Parent = pParent;
            _all = pParent._all;
            _allByName = pParent._allByName;
            _children = new Dictionary<int, Value>();
            _childrenByName = new Dictionary<string, Value>();

            Getter = pGet;
            Setter = pSet;
            Formatter = pFormatter;
            Refresher = pRefresher;
        }

        public Value Create( string pName, ValueRefresher pRefresher = null, ValueGetter pGet = null, ValueSetter pSet = null, Value.ValueFormatter pFormat = null )
        {
            var value = new Value( this, pRefresher, pGet, pSet, pFormat )
            {
                ID = _all.Count + 1,
                Name = pName
            };

            _all[value.ID] = value;
            _allByName[value.Name] = value;

            _children[value.ID] = value;
            _childrenByName[value.Name] = value;

            Children.Add( value );

            return value;
        }

        public Value All( int pID )
        {
            Value result;
            _all.TryGetValue( pID, out result );
            return result;
        }

        public bool HasAllByName( string pName )
        {
            return _allByName.ContainsKey( pName );
        }
        public Value AllByName( string pName )
        {
            Value result;

            if( !_allByName.TryGetValue( pName, out result ) )
                result = Create( pName );

            return result;
        }

        public Value Child( int pID )
        {
            Value result;
            _children.TryGetValue( pID, out result );
            return result;
        }

        public bool HasChildByName( string pName )
        {
            return _allByName.ContainsKey( pName );
        }
        public Value ChildByName( string pName )
        {
            Value result;

            if( !_childrenByName.TryGetValue( pName, out result ) )
                result = Create( pName );

            return result;
        }

        public delegate string ValueGetter( Value pValue );
        public ValueGetter Getter { get; set; }

        public delegate void ValueSetter( Value pValue, string pNew );
        public ValueSetter Setter { get; set; }

        public delegate void ValueRefresher( Value pValue );
        public ValueRefresher Refresher { get; set; }

        public delegate string ValueFormatter( Value pValue );
        public ValueFormatter Formatter { get; set; }

        bool _doingRefresh;
        public void Refresh()
        {
            if( _doingRefresh )
                return;

            _doingRefresh = true;
            Refresher?.Invoke( this );
            _doingRefresh = false;
        }

        bool _doingOnChange;
        string _content;
        public string Content
        {
            set
            {
                var old = _content; _content = value;

                if( _doingOnChange ) return;

                _doingOnChange = true;
                OnChange?.Invoke( this, old, _content );
                _doingOnChange = false;
            }

            get 
            {
                if( Getter != null )
                    _content = Getter(this);

                return _content; 
            }
        }

        public string Formatted
        {
            get
            {
                if( Formatter != null )
                    return Formatter( this );

                return _content;
            }
        }

        public override string ToString()
        {
            return $"{Name} = {Content}";
        }

        public void ClearChildren()
        {
            foreach( var c in _children )
            {
                _all.Remove( c.Key );
                _allByName.Remove( c.Value.Name );
            }

            _children.Clear();
            _childrenByName.Clear();
        }
    }
}