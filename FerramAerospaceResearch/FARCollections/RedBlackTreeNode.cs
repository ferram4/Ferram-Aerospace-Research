using System;


namespace FerramAerospaceResearch.FARCollections
{

    public class RedBlackTreeNode<T>
    {
        public T data;
        public RedBlackTreeNode<T> parent;
        public RedBlackTreeNode<T> leftNode;
        public RedBlackTreeNode<T> rightNode;
        public NodeColor color;

        public enum NodeColor
        {
            black,
            red
        }

        public RedBlackTreeNode() : this(NodeColor.black) { }

        public RedBlackTreeNode(NodeColor nodeColor)
        {
            color = nodeColor;
        }

        public RedBlackTreeNode(NodeColor nodeColor, T nodeData) : this(nodeColor, nodeData, null) { }

        public RedBlackTreeNode(NodeColor nodeColor, T nodeData, RedBlackTreeNode<T> nodeParent)
        {
            parent = nodeParent;
            color = nodeColor;
            data = nodeData;
        }
    }
}
