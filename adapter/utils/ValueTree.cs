using System;
using System.Collections.Generic;

namespace ZXDebug
{
    public class ValueTree
    {
        public int ID;
        public string Name;
        public ValueTree Parent;
        public List<ValueTree> Children = new List<ValueTree>();

        public Action<ValueTree, string, string> OnChange;

        Dictionary<int, ValueTree> _all;
        Dictionary<string, ValueTree> _allByName;
        Dictionary<int, ValueTree> _children;
        Dictionary<string, ValueTree> _childrenByName;
        
        public ValueTree()
        {
            _all = new Dictionary<int, ValueTree>();
            _allByName = new Dictionary<string, ValueTree>( StringComparer.InvariantCultureIgnoreCase );
            _children = new Dictionary<int, ValueTree>();
            _childrenByName = new Dictionary<string, ValueTree>( StringComparer.InvariantCultureIgnoreCase );
        }

        ValueTree( ValueTree parent, ValueRefresher refresher = null, ValueGetter getter = null, ValueSetter setter = null, ValueTree.ValueFormatter formatter = null )
        {
            Parent = parent;
            _all = parent._all;
            _allByName = parent._allByName;
            _children = new Dictionary<int, ValueTree>();
            _childrenByName = new Dictionary<string, ValueTree>();

            Getter = getter;
            Setter = setter;
            Formatter = formatter;
            Refresher = refresher;
        }

        public ValueTree Create( string name, ValueRefresher refresher = null, ValueGetter getter = null, ValueSetter setter = null, ValueTree.ValueFormatter formatter = null )
        {
            var value = new ValueTree( this, refresher, getter, setter, formatter )
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

        public ValueTree All( int id )
        {
            _all.TryGetValue( id, out var result );
            return result;
        }

        public bool HasAllByName( string name )
        {
            return _allByName.ContainsKey( name );
        }
        public ValueTree AllByName( string name )
        {
            if( !_allByName.TryGetValue( name, out var result ) )
                result = Create( name );

            return result;
        }

        public ValueTree Child( int id )
        {
            _children.TryGetValue( id, out var result );
            return result;
        }

        public bool HasChildByName( string name )
        {
            return _allByName.ContainsKey( name );
        }
        public ValueTree ChildByName( string name )
        {
            if( !_childrenByName.TryGetValue( name, out var result ) )
                result = Create( name );

            return result;
        }

        public delegate string ValueGetter( ValueTree value );
        public ValueGetter Getter { get; set; }

        public delegate void ValueSetter( ValueTree value, string newContent );
        public ValueSetter Setter { get; set; }

        public delegate void ValueRefresher( ValueTree value );
        public ValueRefresher Refresher { get; set; }

        public delegate string ValueFormatter( ValueTree value );
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