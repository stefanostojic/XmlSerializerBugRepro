using System;
using System.Collections;
using System.Collections.Generic;

namespace XmlSerializerBugRepro.Models.Collections
{
    public class CustomList<T> : IEnumerable<T>
        //, ICollection // Fix #4
    {
        private readonly List<T> _innerList = new List<T>();

        public void Add(T item) => _innerList.Add(item);

        public IEnumerator<T> GetEnumerator() => _innerList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _innerList.GetEnumerator();

        // Fix #4:
        //void ICollection.CopyTo(Array array, int index) => ((ICollection)_innerList).CopyTo(array, index);
        //bool ICollection.IsSynchronized => false;
        //object ICollection.SyncRoot => this;
    }
}
