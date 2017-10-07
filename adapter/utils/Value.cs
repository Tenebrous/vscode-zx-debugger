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

        Value( Value parent, ValueRefresher refresher = null, ValueGetter getter = null, ValueSetter setter = null, Value.ValueFormatter formatter = null )
        {
            Parent = parent;
            _all = parent._all;
            _allByName = parent._allByName;
            _children = new Dictionary<int, Value>();
            _childrenByName = new Dictionary<string, Value>();

            Getter = getter;
            Setter = setter;
            Formatter = formatter;
            Refresher = refresher;
        }

        public Value Create( string name, ValueRefresher refresher = null, ValueGetter getter = null, ValueSetter setter = null, Value.ValueFormatter formatter = null )
        {
            var value = new Value( this, refresher, getter, setter, formatter )
            {
                ID = _all.Count + 1,
                Name = name
            };

            _all[value.ID] = value;
            _allByName[value.Name] = value;

            _children[value.ID] = value;
            _childrenByName[value.Name] = value;

            Children.Add( value );

            return value;
        }

        public Value All( int id )
        {
            _all.TryGetValue( id, out var result );
            return result;
        }

        public bool HasAllByName( string name )
        {
            return _allByName.ContainsKey( name );
        }
        public Value AllByName( string name )
        {
            if( !_allByName.TryGetValue( name, out var result ) )
                result = Create( name );

            return result;
        }

        public Value Child( int id )
        {
            _children.TryGetValue( id, out var result );
            return result;
        }

        public bool HasChildByName( string name )
        {
            return _allByName.ContainsKey( name );
        }
        public Value ChildByName( string name )
        {
            if( !_childrenByName.TryGetValue( name, out var result ) )
                result = Create( name );

            return result;
        }

        public delegate string ValueGetter( Value value );
        public ValueGetter Getter { get; set; }

        public delegate void ValueSetter( Value value, string newContent );
        public ValueSetter Setter { get; set; }

        public delegate void ValueRefresher( Value value );
        public ValueRefresher Refresher { get; set; }

        public delegate string ValueFormatter( Value value );
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