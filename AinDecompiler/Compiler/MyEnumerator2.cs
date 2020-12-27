using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler.Compiler
{
    class MyEnumerator2<T> where T : class, new()
    {
        static T defaultValue = new T();

        IList<T> array;
        int index = 0;
        int lastCommittedIndex = 0;
        public MyEnumerator2(IList<T> array)
        {
            this.array = array;
        }
        public void Accept()
        {
            lastCommittedIndex = index;
        }
        public void Rollback()
        {
            index = lastCommittedIndex;
        }
        public int Index
        {
            get
            {
                return this.index;
            }
            set
            {
                this.index = value;
            }
        }
        public T Peek()
        {
            if (index >= 0 && index < array.Count)
            {
                return array[index];
            }
            else
            {
                return null;
            }
        }
        public T PeekAhead(int displacement)
        {
            int i = index + displacement;
            if (i >= 0 && i < array.Count)
            {
                return array[i];
            }
            else
            {
                return defaultValue;
            }
        }
        public T Read()
        {
            var returnValue = Peek();
            index++;
            return returnValue;
        }
    }
}
