using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARCollections
{
    public class RedBlackTree<T>
    {
        private RedBlackTreeNode<T> treeRoot = null;
        private Comparer<T> comparer;

        public RedBlackTree() : this(Comparer<T>.Default) { }

        public RedBlackTree(Comparer<T> newComparer)
        {
            comparer = newComparer;
        }

        public void Insert(T key)
        {
            RedBlackTreeNode<T> newNode = Insert(key, treeRoot, null, true);
            InsertionReOrderTree(newNode);
        }

        private RedBlackTreeNode<T> Insert(T key, RedBlackTreeNode<T> node, RedBlackTreeNode<T> parentNode, bool left)
        {
            if (node == null)
            {
                node = new RedBlackTreeNode<T>(RedBlackTreeNode<T>.NodeColor.red, key, parentNode);

                if (parentNode != null)
                    if (left)
                        parentNode.leftNode = node;
                    else
                        parentNode.rightNode = node;
                else
                    treeRoot = node;

                Debug.Log("Added value " + key.ToString() + " to " + (left ? "the left of " : "the right of ") + (parentNode != null ? parentNode.data.ToString() : "top"));

                return node;
            }

            int result = comparer.Compare(key, node.data);

            if (result == -1)
                return Insert(key, node.leftNode, node, true);
            else if (result == 1)
                return Insert(key, node.rightNode, node, false);
            else
                throw new ArgumentException("Tree already contains this key");
        }

        private void InsertionReOrderTree(RedBlackTreeNode<T> node)
        {
            //If root, ensure that it is black, then return
            RedBlackTreeNode<T> parent = node.parent;
            if (parent == null)
            {
                node.color = RedBlackTreeNode<T>.NodeColor.black;
                return;
            }
            //Check parent color; if it is black, all is fine and we can return
            if (parent.color == RedBlackTreeNode<T>.NodeColor.black)
                return;

            //Need the "grandparent" and "uncle" nodes of this one to continue
            RedBlackTreeNode<T> grandParent = null, uncle = null;
            grandParent = parent.parent;

            if (grandParent != null)
                if (grandParent.leftNode == parent)
                    uncle = grandParent.rightNode;
                else
                    uncle = grandParent.leftNode;

            //If the uncle exists, and it is red, then we set him to black, the grandparent to red, and recurse from there
            if(uncle != null && uncle.color == RedBlackTreeNode<T>.NodeColor.red)
            {
                parent.color = RedBlackTreeNode<T>.NodeColor.black;
                uncle.color = RedBlackTreeNode<T>.NodeColor.black;
                grandParent.color = RedBlackTreeNode<T>.NodeColor.red;
                InsertionReOrderTree(grandParent);
                return;
            }

            if ((node == parent.rightNode) && (parent == grandParent.leftNode))
            {
                RotateLeft(parent);
                node = node.leftNode;
                parent = node.parent;
                grandParent = parent.parent;
            }
            else if((node == parent.leftNode) && (parent == grandParent.rightNode))
            {
                RotateRight(parent);
                node = node.rightNode;
                parent = node.parent;
                grandParent = parent.parent;
            }

            parent.color = RedBlackTreeNode<T>.NodeColor.black;
            grandParent.color = RedBlackTreeNode<T>.NodeColor.red;
            if (node == parent.leftNode)
                RotateRight(grandParent);
            else
                RotateLeft(grandParent);
        }

        public bool Delete(T key)
        {
            RedBlackTreeNode<T> nodeToDelete = Find(key, treeRoot);

            if (nodeToDelete == null)
                return false;


            if (nodeToDelete.color == RedBlackTreeNode<T>.NodeColor.red)         //If the node is red, deleting it is simple
            {
                if (nodeToDelete.leftNode != null)
                    if (nodeToDelete.rightNode != null)      //Both node to delete is at the end of the tree; deletion is simple and nothing needs to change
                    {
                        //Do stuff for having both children

                    }
                    else
                    {
                        //Do stuff for only having a left child
                        DeleteAndSetNewChild(nodeToDelete, nodeToDelete.leftNode);
                        return true;
                    }
                else if (nodeToDelete.rightNode != null)
                {
                    //Do stuff for only having a right child
                    DeleteAndSetNewChild(nodeToDelete, nodeToDelete.rightNode);
                    return true;
                }

                //Do stuff for having no children
                DeleteAndSetNewChild(nodeToDelete, null);
                return true;
            }
        }

        private void DeleteAndSetNewChild(RedBlackTreeNode<T> node, RedBlackTreeNode<T> newChild)
        {
            if (node.parent != null)
                if (node.parent.leftNode == node)
                    node.parent.leftNode = newChild;
                else
                    node.parent.rightNode = newChild;

            if (newChild != null)
                newChild.color = RedBlackTreeNode<T>.NodeColor.black;

            node = null;
        }

        private RedBlackTreeNode<T> FindMaxInSubtree(RedBlackTreeNode<T> subtreeRoot)
        {
            if (subtreeRoot.rightNode != null)
                return FindMaxInSubtree(subtreeRoot.rightNode);
            else
                return subtreeRoot;
        }

        public RedBlackTreeNode<T> Find(T key)
        {
            return Find(key, treeRoot);
        }

        private RedBlackTreeNode<T> Find(T key, RedBlackTreeNode<T> node)
        {
            if (node == null)
                return node;

            int result = comparer.Compare(key, node.data);
            if (result == 0)
                return node;
            else if (result == -1)
                return Find(key, node.leftNode);
            else
                return Find(key, node.rightNode);
        }

        public List<T> InOrderTraversal()
        {
            List<T> returnList = new List<T>();
            InOrderTraversal(treeRoot, ref returnList);
            return returnList;
        }

        private void InOrderTraversal(RedBlackTreeNode<T> node, ref List<T> returnList)
        {
            if (node == null)
                return;

            InOrderTraversal(node.leftNode, ref returnList);
            returnList.Add(node.data);
            InOrderTraversal(node.rightNode, ref returnList);
        }

        private void RotateRight(RedBlackTreeNode<T> root)
        {
            RedBlackTreeNode<T> pivot = root.leftNode;  //Assign the pivot temporarily so it doesn't leave scope

            root.leftNode = pivot.rightNode;            //The pivot's left node becomes the root's right node
            if (pivot.rightNode != null)
                pivot.rightNode.parent = root;                //Ensure that the parent is reset as well

            pivot.parent = root.parent;

            if (root.parent == null)
            {
                treeRoot = pivot;
            }
            else
            {
                if (root == root.parent.leftNode)
                    root.parent.leftNode = pivot;
                else
                    root.parent.rightNode = pivot;
            }
            pivot.rightNode = root;
            root.parent = pivot;
        }

        private void RotateLeft(RedBlackTreeNode<T> root)
        {
            RedBlackTreeNode<T> pivot = root.rightNode;  //Assign the pivot temporarily so it doesn't leave scope

            root.rightNode = pivot.leftNode;            //The pivot's left node becomes the root's right node
            if (pivot.leftNode != null)
                pivot.leftNode.parent = root;                //Ensure that the parent is reset as well

            pivot.parent = root.parent;

            if(root.parent == null)
            {
                treeRoot = pivot;
            }
            else
            {
                if (root == root.parent.leftNode)
                    root.parent.leftNode = pivot;
                else
                    root.parent.rightNode = pivot;
            }
            pivot.leftNode = root;
            root.parent = pivot;
        }

        private void SwapNodeValues(RedBlackTreeNode<T> node1, RedBlackTreeNode<T> node2)
        {
            T tmp = node1.data;
            RedBlackTreeNode<T>.NodeColor tmpColor = node1.color;
            node1.data = node2.data;
            node1.color = node2.color;
            node2.data = tmp;
            node2.color = tmpColor;
        }
    }
}
