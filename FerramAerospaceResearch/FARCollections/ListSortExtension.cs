using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FerramAerospaceResearch.FARCollections
{
    public static class ListSortExtension
    {
        public static List<T> MergeSort<T>(this List<T> list)
        {
            return MergeSort<T>(list, Comparer<T>.Default);
        }

        public static List<T> MergeSort<T>(this List<T> list, Comparer<T> comparer)
        {
            // Base case. A list of zero or one elements is sorted, by definition.
            if (list.Count <= 1)
                return list;

            int middle = (int)(list.Count * 0.5);

            // Recursive case. First, *divide* the list into equal-sized sublists.
            List<T> left = list.GetRange(0, middle);
            List<T> right = list.GetRange(middle, list.Count - middle);

            left = MergeSort<T>(left, comparer);
            right = MergeSort<T>(right, comparer);

            return MergeSortBuildup<T>(left, right, comparer);
        }

        private static List<T> MergeSortBuildup<T>(List<T> left, List<T> right, Comparer<T> comparer)
        {
            List<T> result = new List<T>();
            while (left.Count > 0 || right.Count > 0)
            {
                if (left.Count > 0 && right.Count > 0)
                {
                    int cmp = comparer.Compare(left[0], right[0]);
                    if (cmp <= 0)
                    {
                        result.Add(left[0]);
                        left.RemoveAt(0);
                    }
                    else
                    {
                        result.Add(right[0]);
                        right.RemoveAt(0);
                    }
                }
                else if (left.Count > 0)
                {
                    result.Add(left[0]);
                    left.RemoveAt(0);
                }
                else if (right.Count > 0)
                {
                    result.Add(right[0]);
                    right.RemoveAt(0);
                }
            }
            return result;
        }

    }
}
