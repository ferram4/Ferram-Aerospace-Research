/*
Ferram Aerospace Research v0.14.6
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Ferram Aerospace Research is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
                        Regex, for adding RPM support
            			Duxwing, for copy editing the readme
 * 
 * Kerbal Engineer Redux created by Cybutek, Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License
 *      Referenced for starting point for fixing the "editor click-through-GUI" bug
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 * Toolbar integration powered by blizzy78's Toolbar plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARCollections
{
    class LLRedBlackTree<T>
    {
        private const bool RED = true;
        private const bool BLACK = false;

        class RedBlackTreeNode
        {
            public T data;
            public RedBlackTreeNode left, right, parent;
            public bool color;

            public RedBlackTreeNode(T newData, RedBlackTreeNode newParent)
            {
                this.color = RED;
                data = newData;
                parent = newParent;
            }
        }
        private RedBlackTreeNode _treeRoot = null;
        private IComparer<T> _comparer;
        private int _count = 0;

        public int Count { get { return _count; } }

        public LLRedBlackTree() : this(Comparer<T>.Default) { }

        public LLRedBlackTree(IComparer<T> newComparer)
        {
            _comparer = newComparer;
        }

        private RedBlackTreeNode FindNode(T data, RedBlackTreeNode node)
        {
            int cmp = _comparer.Compare(data, node.data);

            if (cmp == 0)
                return node;
            else if (cmp < 0)
                return FindNode(data, node.left);
            else if (cmp > 0)
                return FindNode(data, node.right);

            return null;
        }

        public void FindNearestData(T inputData, out T lowerData, out T upperData)
        {
            RedBlackTreeNode leftNode = FindNodeOrNodeToLeft(inputData, _treeRoot, null);

            if(leftNode != null)
            {
                lowerData = leftNode.data;
                upperData = Next(leftNode.data, leftNode);
                return;
            }
            lowerData = default(T);
            upperData = Min(_treeRoot).data;
        }

        private RedBlackTreeNode FindNodeOrNodeToLeft(T data, RedBlackTreeNode node, RedBlackTreeNode leftNode)
        {
            int cmp = _comparer.Compare(data, node.data);

            if (cmp == 0)
                return node;
            else if(cmp < 0)
            {
                if (node.left != null)
                    return FindNodeOrNodeToLeft(data, node.left, leftNode);
                else
                    return leftNode;
            }
            else if (cmp > 0)
            {
                leftNode = node;
                if (node.right != null)
                    return FindNodeOrNodeToLeft(data, node.right, leftNode);
                else
                    return leftNode;
            }

            return null;
        }

        public T Next(T data)
        {
            return Next(data, _treeRoot);
        }

        private T Next(T data, RedBlackTreeNode node)
        {
            node = FindNode(data, node);
            if (node == null)
                return default(T);

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
                    return default(T);

            } while (_comparer.Compare(data, node.data) > 0);

            return node.data;
        }

        public T Prev(T data)
        {
            return Prev(data, _treeRoot);
        }

        private T Prev(T data, RedBlackTreeNode node)
        {
            node = FindNode(data, node);
            if (node == null)
                return default(T);

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
                    return default(T);

            } while (_comparer.Compare(data, node.data) < 0);

            return node.data;
        }
        
        public void Insert(T data)
        {
            _treeRoot = Insert(data, _treeRoot, null);
            if (_treeRoot != null)
                _treeRoot.color = BLACK;
            _count++;
        }

        private RedBlackTreeNode Insert(T data, RedBlackTreeNode node, RedBlackTreeNode parent)
        {
            if (node == null)
                return new RedBlackTreeNode(data, parent);

            int cmp = _comparer.Compare(data, node.data);

            //if (cmp == 0)
            //    node.data = data;
            //else
            if (cmp < 0)
                node.left = Insert(data, node.left, node);
            else if (cmp >= 0)
                node.right = Insert(data, node.right, node);

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
            _treeRoot = DeleteMin(_treeRoot);
            if(_treeRoot != null)
                _treeRoot.color = BLACK;
        }

        private RedBlackTreeNode DeleteMin(RedBlackTreeNode node)
        {
            if (node.left == null)
            {
                _count--;
                return null;
            }

            if (!isRed(node.left) && !isRed(node.left.left))
                node = MoveRedLeft(node);

            node.left = DeleteMin(node.left);
            if (node.left != null)
                node.left.parent = node;

            return FixUp(node);
        }

        public void Delete(T data)
        {
            _treeRoot = Delete(data, _treeRoot);
            if(_treeRoot != null)
                _treeRoot.color = BLACK;
        }

        private RedBlackTreeNode Delete(T data, RedBlackTreeNode node)
        {
            if (_comparer.Compare(data, node.data) < 0)
            {
                if (!isRed(node.left) && !isRed(node.left.left))
                    node = MoveRedLeft(node);

                node.left = Delete(data, node.left);
                if(node.left != null)
                    node.left.parent = node;
            }
            else
            {
                if (isRed(node.left))
                    node = RotateRight(node);

                if (_comparer.Compare(data, node.data) == 0 && (node.right == null))
                {
                    _count--;
                    return null;
                }

                if (node.right == null || (!isRed(node.right) && !isRed(node.right.left)))
                    node = MoveRedRight(node);

                if (_comparer.Compare(data, node.data) == 0)
                {
                    node.data = Min(node.right).data;
                    node.right = DeleteMin(node.right);
                    if(node.right != null)
                        node.right.parent = node;
                }
                else
                {
                    node.right = Delete(data, node.right);
                    if (node.right != null)
                        node.right.parent = node;
                }
            }
            return FixUp(node);
        }

        public void Clear()
        {
            while (_count > 0)
                DeleteMin();
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
                node = RotateLeft(node);
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
        
        public List<T> InOrderTraversal()
        {
            List<T> returnList = new List<T>();
            InOrderTraversal(_treeRoot, ref returnList);
            return returnList;
        }

        private void InOrderTraversal(RedBlackTreeNode node, ref List<T> returnList)
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
            if (root.left != null)
                root.left.parent = root;

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
            if (root.right != null)
                root.right.parent = root;

            pivot.color = root.color;
            root.color = RED;
            return pivot;
        }

        public bool TrySwapValues(T val1, T val2)
        {
            RedBlackTreeNode node1, node2;
            node1 = FindNode(val1, _treeRoot);
            if (node1 == null)
                return false;

            node2 = FindNode(val2, _treeRoot);
            if (node2 == null)
                return false;

            SwapNodeValues(node1, node2);

            return true;
        }

        private void SwapNodeValues(RedBlackTreeNode node1, RedBlackTreeNode node2)
        {
            T tmp = node1.data;
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
