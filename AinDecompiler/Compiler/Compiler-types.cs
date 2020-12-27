using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler.Compiler
{
    public partial class Compiler
    {
        internal class StringListAndDictionary
        {
            public bool DoNotCombine = false;

            public StringListAndDictionary()
            {
                this.List = new List<string>();
            }
            public StringListAndDictionary(IList<string> list)
            {
                this.List = list;
                if (list.Count > 0)
                {
                    AddExistingItems();
                }
            }

            private void AddExistingItems()
            {
                for (int i = 0; i < List.Count; i++)
                {
                    NameToIndex[List[i]] = i;
                }
            }

            public IList<string> List;
            public Dictionary<string, int> NameToIndex = new Dictionary<string, int>();
            public int Add(string name)
            {
                if (!DoNotCombine)
                {
                    if (name == null) name = "";
                    if (NameToIndex.ContainsKey(name))
                    {
                        return NameToIndex[name];
                    }
                }
                int index = List.Count;
                NameToIndex[name] = index;
                List.Add(name);
                return index;
            }
        }

        //class ListAndDictionaryCOW<T> : ListAndDictionary<T> where T : IVariable, new()
        //{
        //    public ListAndDictionaryCOW(IDictionary<string, IVariable> parent, AinFile root, IList<T> list, IDictionary<string, int> nameToIndex, string listName, string dictionaryName)
        //        : base(parent, root, list, nameToIndex)
        //    {
        //        this.listName = listName;
        //        this.dictionaryName = dictionaryName;
        //    }

        //    string listName, dictionaryName;

        //    void ReplaceItems()
        //    {
        //        var listField = typeof(AinFile).GetField(listName);
        //        if (listField != null && 
        //            !listField.IsStatic && 
        //            listField.IsPublic && 
        //            !listField.IsInitOnly && 
        //            typeof(IList<T>).IsAssignableFrom(listField.FieldType))
        //        {
        //            var newList = Activator.CreateInstance(listField.FieldType) as IList<T>;

        //        }
        //        else
        //        {
        //            throw new InvalidOperationException();
        //        }
        //    }

        //    protected bool dirty = false;
        //    public override T Get(string name, bool createSymbol)
        //    {
        //        if (NameToIndex.ContainsKey(name))
        //        {
        //            return List[NameToIndex[name]];
        //        }

        //        if (this.dirty == true)
        //        {
        //            return base.Get(name, createSymbol);
        //        }
        //        else
        //        {
        //            ReplaceItems();
        //            this.dirty = true;
        //            return base.Get(name, createSymbol);
        //        }
        //    }
        //}

        internal class ListAndDictionary<T> where T : IVariable, new()
        {
            protected AinFile root;
            public ListAndDictionary(IDictionary<string, IVariable> parent, AinFile root)
            {
                this.root = root;
                this.parent = parent;
                this.List = new List<T>();
                this.NameToIndex = new Dictionary<string, int>();
                AddExistingItems();
            }
            public ListAndDictionary(IDictionary<string, IVariable> parent, AinFile root, IList<T> list)
            {
                this.root = root;
                this.parent = parent;
                this.List = list;
                this.NameToIndex = new Dictionary<string, int>();
                AddExistingItems();
            }

            public ListAndDictionary(IDictionary<string, IVariable> parent, AinFile root, IList<T> list, IDictionary<string, int> nameToIndex)
            {
                this.root = root;
                this.parent = parent;
                this.List = list;
                this.NameToIndex = nameToIndex;
                AddExistingItems();
            }
            protected IDictionary<string, IVariable> parent;
            public IList<T> List;
            public IDictionary<string, int> NameToIndex;
            protected Dictionary<string, T> orphans = new Dictionary<string, T>();
            public T Get(string name)
            {
                return Get(name, true);
            }
            public bool Contains(string name)
            {
                return NameToIndex.ContainsKey(name);
            }
            private void AddExistingItems()
            {
                for (int i = 0; i < List.Count; i++)
                {
                    parent[List[i].Name] = List[i];
                    NameToIndex[List[i].Name] = i;
                }
                if (typeof(T) == typeof(HllLibrary))
                {
                    for (int i0 = 0; i0 < List.Count; i0++)
                    {
                        var item = List[i0] as HllLibrary;
                        for (int i1 = 0; i1 < item.Functions.Count; i1++)
                        {
                            var func = item.Functions[i1];
                            parent[func.FullName] = func;
                            NameToIndex[func.FullName] = i1;
                        }
                    }
                }
            }

            /*virtual*/
            public T Get(string name, bool createSymbol)
            {
                if (NameToIndex.ContainsKey(name))
                {
                    return List[NameToIndex[name]];
                }
                else
                {
                    T newItem;
                    if (orphans.ContainsKey(name))
                    {
                        newItem = orphans[name];
                        if (!createSymbol)
                        {
                            orphans[name] = newItem;
                            return newItem;
                        }
                        else
                        {
                            orphans.Remove(name);
                        }
                    }
                    else
                    {
                        newItem = new T();
                        newItem.Name = name;
                        newItem.Root = root;
                    }


                    if (createSymbol)
                    {
                        int index = List.Count;

                        newItem.Index = index;
                        List.Add(newItem);
                        this.NameToIndex.Add(name, index);

                        if (!parent.ContainsKey(name))
                        {
                            parent.Add(name, newItem);
                        }
                    }
                    else
                    {
                        orphans[name] = newItem;
                    }
                    return newItem;
                }
            }
        }

    }
}
