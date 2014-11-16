using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARCollections
{
    public class LLRedBlackTree<TKey, TVal>
    {
        private const bool RED = true;
        private const bool BLACK = false;

        class RedBlackTreeNode
        {
            public TKey key;
            public TVal data;
            public RedBlackTreeNode left, right, parent;
            public bool color;

            public RedBlackTreeNode(TKey newKey, TVal newData, RedBlackTreeNode newParent)
            {
                this.color = RED;
                data = newData;
                key = newKey;
                parent = newParent;
            }
        }
        private RedBlackTreeNode treeRoot = null;
        private Comparer<TKey> comparer;

        public LLRedBlackTree() : this(Comparer<TKey>.Default) { }

        public LLRedBlackTree(Comparer<TKey> newComparer)
        {
            comparer = newComparer;
        }

        public TVal Search(TKey key)
        {
            return Search(key, treeRoot);
        }

        private TVal Search(TKey key, RedBlackTreeNode node)
        {
            node = FindNode(key, node);
            if (node == null)
                return default(TVal);

            return node.data;
        }

        private RedBlackTreeNode FindNode(TKey key, RedBlackTreeNode node)
        {
            int cmp = comparer.Compare(key, node.key);

            if (cmp == 0)
                return node;
            else if (cmp < 0)
                return FindNode(key, node.left);
            else if (cmp > 0)
                return FindNode(key, node.right);

            return null;
        }

        public TVal Next(TKey key)
        {
            return Next(key, treeRoot);
        }

        private TVal Next(TKey key, RedBlackTreeNode node)
        {
            node = FindNode(key, node);
            if (node == null)
                return default(TVal);

            if (node.right != null)
            {
                node = node.right;
                while (node.left != null)
                    node = node.left;
                return node.data;
            }

            do
            {
                node = node.parent;
                if(node == null)
                    return default(TVal);

            } while (comparer.Compare(key, node.key) > 0);

            return node.data;
        }

        public TVal Prev(TKey key)
        {
            return Prev(key, treeRoot);
        }

        private TVal Prev(TKey key, RedBlackTreeNode node)
        {
            node = FindNode(key, node);
            if (node == null)
                return default(TVal);

            if (node.left != null)
            {
                node = node.left;
                while (node.right != null)
                    node = node.right;
                return node.data;
            }
            do
            {
                node = node.parent;
                if (node == null)
                    return default(TVal);

            } while (comparer.Compare(key, node.key) < 0);

            return node.data;
        }
        
        public void Insert(TKey key, TVal data)
        {
            treeRoot = Insert(key, data, treeRoot, null);
            if (treeRoot != null)
                treeRoot.color = BLACK;
        }

        private RedBlackTreeNode Insert(TKey key, TVal data, RedBlackTreeNode node, RedBlackTreeNode parent)
        {
            if (node == null)
                return new RedBlackTreeNode(key, data, parent);

            int cmp = comparer.Compare(key, node.key);

            if (cmp == 0)
                node.data = data;
            else if (cmp < 0)
                node.left = Insert(key, data, node.left, node);
            else if (cmp > 0)
                node.right = Insert(key, data, node.right, node);

            if (isRed(node.right) && !isRed(node.left))
                node = RotateLeft(node);

            if (isRed(node.left) && isRed(node.left.left))
                node = RotateRight(node);

            if (isRed(node.left) && isRed(node.right))
                ColorFlip(node);

            return node;
        }

        public void DeleteMin()
        {
            treeRoot = DeleteMin(treeRoot);
            if(treeRoot != null)
                treeRoot.color = BLACK;
        }

        private RedBlackTreeNode DeleteMin(RedBlackTreeNode node)
        {
            if (node.left == null)
                return null;

            if (!isRed(node.left) && !isRed(node.left.left))
                node = MoveRedLeft(node);

            node.left = DeleteMin(node.left);

            return FixUp(node);
        }

        public void Delete(TKey key)
        {
            treeRoot = Delete(key, treeRoot);
            if(treeRoot != null)
                treeRoot.color = BLACK;
        }

        private RedBlackTreeNode Delete(TKey key, RedBlackTreeNode node)
        {
            if (comparer.Compare(key, node.key) < 0)
            {
                if (!isRed(node.left) && !isRed(node.left.left))
                    node = MoveRedLeft(node);
                node.left = Delete(key, node.left);
                if(node.left != null)
                    node.left.parent = node;
            }
            else
            {
                if (isRed(node.left))
                    node = RotateRight(node);

                if (comparer.Compare(key, node.key) == 0 && (node.right == null))
                    return null;

                if (node.right == null || (!isRed(node.right) && !isRed(node.right.left)))
                    node = MoveRedRight(node);

                if (comparer.Compare(key, node.key) == 0)
                {
                    node.data = Search(Min(node.right).key, node.right);
                    node.key = Min(node.right).key;
                    node.right = DeleteMin(node.right);
                    if(node.right != null)
                        node.right.parent = node;
                }
                else
                {
                    node.right = Delete(key, node.right);
                    if (node.right != null)
                        node.right.parent = node;
                }
            }
            return FixUp(node);
        }

        private RedBlackTreeNode Min(RedBlackTreeNode node)
        {
            while (node.left != null)
            {
                node = node.left;
            }
            return node;
        }

        private RedBlackTreeNode MoveRedLeft(RedBlackTreeNode node)
        {
            ColorFlip(node);
            if(node.right != null && isRed(node.right.left))
            {
                node.right = RotateRight(node.right);
                node = RotateLeft(node);
                ColorFlip(node);
            }
            return node;
        }

        private RedBlackTreeNode MoveRedRight(RedBlackTreeNode node)
        {
            ColorFlip(node);
            if (node.left != null && isRed(node.left.left))
            {
                node = RotateRight(node);
                ColorFlip(node);
            }
            return node;
        }

        private RedBlackTreeNode FixUp(RedBlackTreeNode node)
        {
            if(isRed(node.right))
            {
                RotateLeft(node);
            }
            if (node.left != null && isRed(node.left) && isRed(node.left.left))
            {
                node = RotateRight(node);
            }
            if (isRed(node.left) && isRed(node.right))
            {
                ColorFlip(node);
            }

            return node;
        }
        
        public List<TVal> InOrderTraversal()
        {
            List<TVal> returnList = new List<TVal>();
            InOrderTraversal(treeRoot, ref returnList);
            return returnList;
        }

        private void InOrderTraversal(RedBlackTreeNode node, ref List<TVal> returnList)
        {
            if (node == null)
                return;

            InOrderTraversal(node.left, ref returnList);
            returnList.Add(node.data);
            InOrderTraversal(node.right, ref returnList);
        }

        private RedBlackTreeNode RotateRight(RedBlackTreeNode root)
        {
            RedBlackTreeNode pivot = root.left;
            root.left = pivot.right;
            pivot.right = root;

            pivot.parent = root.parent;
            root.parent = pivot;

            pivot.color = root.color;
            root.color = RED;
            return pivot;
        }

        private RedBlackTreeNode RotateLeft(RedBlackTreeNode root)
        {
            RedBlackTreeNode pivot = root.right;
            root.right = pivot.left;
            pivot.left = root;

            pivot.parent = root.parent;
            root.parent = pivot;

            pivot.color = root.color;
            root.color = RED;
            return pivot;
        }

        private void SwapNodeValues(RedBlackTreeNode node1, RedBlackTreeNode node2)
        {
            TVal tmp = node1.data;
            node1.data = node2.data;
            node2.data = tmp;
        }

        private bool isRed(RedBlackTreeNode node)
        {
            if(node != null)
                return node.color;

            return BLACK;
        }

        private void ColorFlip(RedBlackTreeNode node)
        {
            node.color = !node.color;
            if (node.left != null)
                node.left.color = !node.left.color;
            if (node.right != null)
                node.right.color = !node.right.color;
        }
    }
}
