using System;
using System.Collections.Generic;

namespace VSCodeDebugger
{
    public class Value
    {
        public int ID;
        public int Priority;
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

        Value( Value pParent, Action<Value> pRefresher = null, ValueGetter pGet = null, ValueSetter pSet = null, Value.ValueFormatter pFormatter = null )
        {
            Parent = pParent;
            _all = pParent._all;
            _allByName = pParent._allByName;
            _children = new Dictionary<int, Value>();
            _childrenByName = new Dictionary<string, Value>();

            _getter = pGet;
            _setter = pSet;
            _formatter = pFormatter;
        }

        public Value Create( string pName, Action<Value> pRefresher = null, ValueGetter pGet = null, ValueSetter pSet = null, Value.ValueFormatter pFormat = null )
        {
            var value = new Value( this, pRefresher, pGet, pSet, pFormat )
            {
                ID = this._all.Count + 1,
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

        ValueGetter _getter;
        public ValueGetter Getter
        {
            get { return _getter; }
            set { _getter = value; }
        }


        public delegate void ValueSetter( Value pValue, string pNew );
        
        ValueSetter _setter;
        public ValueSetter Setter
        {
            get { return _setter; }
            set { _setter = value; }
        }

        Action<Value> _refresher;
        public Action<Value> Refresher
        {
            get { return _refresher; }
            set { _refresher = value; }
        }


        public delegate string ValueFormatter( Value pValue );

        ValueFormatter _formatter;
        public ValueFormatter Formatter
        {
            get { return _formatter; }
            set { _formatter = value; }
        }

        bool _doingRefresh;
        public void Refresh()
        {
            if( _doingRefresh )
                return;

            _doingRefresh = true;
            _refresher?.Invoke( this );
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
                if( _getter != null )
                    _content = _getter(this);

                 return _content; 
            }
        }

        public string Formatted
        {
            get
            {
                if( _formatter != null )
                    return _formatter( this );

                return _content;
            }
        }

        public override string ToString()
        {
            return string.Format( "{0} = {1}", Name, Content );
        }
    }
}