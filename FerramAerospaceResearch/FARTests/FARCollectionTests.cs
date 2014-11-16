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
            System.Random rand = new System.Random();
            int counter = 0;

            while(counter < 10)
            {
                int removalInt = rand.Next(100);
                tree.Insert(removalInt);
                for (int j = 0; j < 5; j++)
                    tree.Insert(rand.Next(100));
                PrintTree(tree);
                tree.Delete(removalInt);
                counter++;
            }
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
                int cur, prev, next;
                cur = list[i];
                prev = tree.Prev(cur);
                next = tree.Next(cur);
                s += "Value: " + cur.ToString() + " Prev: " + prev + " Next: " + next + (prev >= cur ? " Error: prev > input value" : "") + (next <= cur ? " Error: next < input value" : "") + "\n\r";
            }
            Debug.Log(s);
            Debug.Log("Count: " + tree.Count);
        }
    }
}
