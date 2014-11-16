using System;
using System.Collections.Generic;
using FerramAerospaceResearch.FARCollections;
using UnityEngine;
using KSP;

namespace FerramAerospaceResearch.FARTests
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class FARCollectionTests : MonoBehaviour
    {
        void Start()
        {
            LLRedBlackTree<int> tree = new LLRedBlackTree<int>();
            tree.Insert(5);
            tree.Insert(1);
            tree.Insert(10);

            PrintTree(tree);

            tree.Insert(3);
            tree.Insert(7);
            tree.Insert(-1);

            PrintTree(tree);

            tree.Delete(5);

            PrintTree(tree);

            tree.Insert(5);
            tree.Insert(11);
            tree.Delete(3);

            PrintTree(tree);
        }

        private void PrintTree(LLRedBlackTree<int> tree)
        {
            List<int> list = tree.InOrderTraversal();
            string s = "";
            for (int i = 0; i < list.Count; i++)
                s += list[i].ToString() + ", ";

            Debug.Log(s);
            s = "";
            for(int i = 0; i < list.Count; i++)
            {
                s += "Value: " + list[i].ToString() + " Prev: " + tree.Prev(list[i]) + " Next: " + tree.Next(list[i]) + "\n\r";
            }
            Debug.Log(s);
        }
    }
}
