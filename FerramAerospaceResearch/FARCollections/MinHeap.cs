using System;
using System.Collections.Generic;

namespace FerramAerospaceResearch.FARCollections
{
    class MinHeap<T>
    {
        List<T> _heapArray;
        IComparer<T> _comparer;

        public MinHeap() : this(Comparer<T>.Default) { }
        public MinHeap(IComparer<T> comparer)
        {
            _heapArray = new List<T>();
            _comparer = comparer;
        }

        public MinHeap(List<T> items) : this(items, Comparer<T>.Default) { }
        public MinHeap(List<T> items, IComparer<T> comparer)
        {
            _heapArray = new List<T>();
            _heapArray.AddRange(items);
            _comparer = comparer;
        }

        public void Insert(T item)
        {
            int insertIndex = _heapArray.Count;
            _heapArray.Add(item);
            UpHeap(insertIndex);
        }

        public void DeleteMin()
        {
            int lastIndex = _heapArray.Count - 1;

            _heapArray[0] = _heapArray[lastIndex];
            _heapArray.RemoveAt(lastIndex);
            MinHeapify(0);
        }

        private void UpHeap(int index)
        {
            int parentIndex = ParentIndex(index);
            int cmp = _comparer.Compare(_heapArray[index], _heapArray[parentIndex]);

            if(cmp < 0)
            {
                SwapNodes(index, parentIndex);  //If this index is smaller than its parent, it must move up the heap
                UpHeap(parentIndex);            //And then recurse
            }
        }

        private void MinHeapify(int index)
        {
            int childIndex;

            childIndex = LeftChild(index);       //get the child indices

            if (childIndex >= _heapArray.Count)     //if this happens, we're at the bottom of the heap already
                return;

            int cmp = 0;

            if(childIndex + 1 < _heapArray.Count)   //make sure that even if children exist, that there's a right child before we compare
                cmp = _comparer.Compare(_heapArray[childIndex], _heapArray[childIndex + 1]);     //and find the smaller of the two; right child is always left child + 1

            if(cmp > 0)
                childIndex++;       //if the right child is smaller, then we need to use its index

            cmp = _comparer.Compare(_heapArray[index], _heapArray[childIndex]);

            if(cmp > 0)     //if the child is smaller than its parent, they must swap
            {
                SwapNodes(index, childIndex);
                MinHeapify(childIndex);       //And now recurse down
            }
        }

        private void BuildMinHeap()
        {
            int length = (_heapArray.Count / 2) - 1;
            for(int i = length; i <= 0; i--)
                MinHeapify(i);
        }

        private int LeftChild(int parentIndex)
        {
            return 2 * parentIndex + 1;
        }

        private int ParentIndex(int childIndex)
        {
            return (childIndex - 1) / 2;        //take advantage of rounding to integer to make this simpler
        }

        private void SwapNodes(int index1, int index2)
        {
            T tmp = _heapArray[index1];
            _heapArray[index1] = _heapArray[index2];
            _heapArray[index2] = tmp;
        }
    }
}
