using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler.Compiler
{
    public partial class Compiler
    {
        public class DictionaryWithParent<TKey, TValue> : IDictionary<TKey, TValue>
        {
            public IDictionary<TKey, TValue> Parent;
            public IDictionary<TKey, TValue> Dic;
            public DictionaryWithParent()
                : this(null)
            {

            }
            public DictionaryWithParent(IDictionary<TKey, TValue> parent)
                : this(parent, new Dictionary<TKey, TValue>())
            {

            }

            public DictionaryWithParent(IDictionary<TKey, TValue> parent, IDictionary<TKey, TValue> dic)
                : base()
            {
                this.Parent = parent;
                this.Dic = dic;
            }
            public DictionaryWithParent(IDictionary<TKey, TValue> parent, int capacity)
                : this(parent, new Dictionary<TKey, TValue>(capacity))
            {

            }



            #region IDictionary<TKey,TValue> Members

            public void Add(TKey key, TValue value)
            {
                Dic.Add(key, value);
            }

            public bool ContainsKey(TKey key)
            {
                if (Dic.ContainsKey(key))
                {
                    return true;
                }
                return Parent != null && Parent.ContainsKey(key);
            }

            public ICollection<TKey> Keys
            {
                get
                {
                    if (Parent != null)
                    {
                        return Dic.Keys.Concat(Parent.Keys).ToArray();
                    }
                    else
                    {
                        return Dic.Keys;
                    }
                }
            }

            public bool Remove(TKey key)
            {
                if (Dic.Remove(key))
                {
                    return true;
                }
                if (Parent != null && Parent.Remove(key))
                {
                    return true;
                }
                return false;
            }

            public bool TryGetValue(TKey key, out TValue value)
            {
                if (Dic.TryGetValue(key, out value))
                {
                    return true;
                }
                if (Parent != null && Parent.TryGetValue(key, out value))
                {
                    return true;
                }
                value = default(TValue);
                return false;
            }

            public ICollection<TValue> Values
            {
                get
                {
                    if (Parent != null)
                    {
                        return Dic.Values.Concat(Parent.Values).ToArray();
                    }
                    else
                    {
                        return Dic.Values;
                    }
                }
            }

            public TValue this[TKey key]
            {
                get
                {
                    TValue value;
                    if (TryGetValue(key, out value))
                    {
                        return value;
                    }
                    else
                    {
                        throw new KeyNotFoundException();
                    }
                }
                set
                {
                    if (Dic.ContainsKey(key))
                    {
                        Dic[key] = value;
                    }
                    else if (Parent != null && Parent.ContainsKey(key))
                    {
                        Parent[key] = value;
                    }
                    else
                    {
                        Dic[key] = value;
                    }
                }
            }

            #endregion

            #region ICollection<KeyValuePair<TKey,TValue>> Members

            public void Add(KeyValuePair<TKey, TValue> item)
            {
                this.Add(item.Key, item.Value);
            }

            public void Clear()
            {
                Dic.Clear();
                if (Parent != null)
                {
                    Parent.Clear();
                }
            }

            public bool Contains(KeyValuePair<TKey, TValue> item)
            {
                TValue value;
                if (this.TryGetValue(item.Key, out value))
                {
                    return Object.Equals(value, item.Value);
                }
                return false;
            }

            public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
            {
                foreach (var pair in this)
                {
                    array[arrayIndex] = pair;
                    arrayIndex++;
                }
            }

            public int Count
            {
                get
                {
                    int parentCount = (Parent != null) ? (Parent.Count) : 0;
                    return parentCount + Dic.Count;
                }
            }

            public bool IsReadOnly
            {
                get
                {
                    return false;
                }
            }

            public bool Remove(KeyValuePair<TKey, TValue> item)
            {
                if (((ICollection<KeyValuePair<TKey, TValue>>)this).Contains(item))
                {
                    return this.Remove(item.Key);
                }
                return false;
            }

            #endregion

            IEnumerable<KeyValuePair<TKey, TValue>> Enumerate()
            {
                foreach (var pair in Dic)
                {
                    yield return pair;
                }
                if (this.Parent != null)
                {
                    foreach (var pair in Parent)
                    {
                        yield return pair;
                    }
                }
            }


            #region IEnumerable<KeyValuePair<TKey,TValue>> Members

            public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            {
                return Enumerate().GetEnumerator();
            }

            #endregion

            #region IEnumerable Members

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return Enumerate().GetEnumerator();
            }

            #endregion
        }

        class VariableDictionaryWithParent : DictionaryWithParent<string, IVariable>
        {
            public VariableDictionaryWithParent()
                : base()
            {

            }

            public VariableDictionaryWithParent(IDictionary<string, IVariable> parent)
                : base(parent)
            {

            }

            public VariableDictionaryWithParent(IDictionary<string, IVariable> parent, IDictionary<string, IVariable> dic)
                : base(parent, dic)
            {

            }


            internal static VariableDictionaryWithParent GetCommonParent(VariableDictionaryWithParent d1, VariableDictionaryWithParent d2)
            {
                HashSet<VariableDictionaryWithParent> s1 = new HashSet<VariableDictionaryWithParent>();
                //fill set 1
                VariableDictionaryWithParent v;
                for (v = d1; v != null; v = v.Parent as VariableDictionaryWithParent)
                {
                    s1.Add(v);
                }

                for (v = d2; v != null; v = v.Parent as VariableDictionaryWithParent)
                {
                    if (s1.Contains(v))
                    {
                        return v;
                    }
                }
                return null;
            }
        }

        class VariableMultiDictionary : MultipleDictionary<string, IVariable>
        {

        }

    }
}
