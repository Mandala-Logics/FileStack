using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MandalaLogics.Path
{
    public class ObjectTreeNode<T> : IReadOnlyList<ObjectTreeNode<T>>
    {
        //PUBLIC PROPERTIES
        public bool HasParent => parent is ObjectTreeNode<T>;
        public bool IsRoot => parent is null;
        public bool HasChildren => Count > 0;
        public ObjectTreeNode<T> Root
        {
            get
            {
                var n = this;

                while (n.HasParent)
                {
                    n = n.parent;
                }

                return n;
            }
        }
        public int Depth
        {
            get
            {
                var n = this;
                int ret = 0;

                while (n.HasParent)
                {
                    n = n.parent;
                    ret++;
                }

                return ret;
            }
        }
        public ObjectTreeNode<T> Parent
        {
            get
            {
                return parent ?? throw new InvalidOperationException("This is a root node and does not have a parent or siblings.");
            }
        }
        public int Count => children.Count;
        public T Value { get; }

        public ObjectTreeNode<T> this[int index] => children[index];
        public IReadOnlyList<ObjectTreeNode<T>> Children { get; }

        //PRIVATE PROPERTIES
        private readonly List<ObjectTreeNode<T>> children = new List<ObjectTreeNode<T>>();
        private readonly ObjectTreeNode<T> parent;

        //CONSTRUCTORS
        private protected ObjectTreeNode(ObjectTreeNode<T> parent, T value)
        {
            this.parent = parent;
            Value = value;

            Children = new ReadOnlyListWrapper<ObjectTreeNode<T>>(children);
        }

        //PRIVATE METHODS
        private string GetSerialString(int mod = 0)
        {
            var sb = new StringBuilder();

            for (int x = 0; x < Depth - mod; x++)
            {
                sb.Append("| ");
            }

            sb.Append(Value);

            return sb.ToString();
        }
        private void DoWriteNode(StreamWriter writer, int start)
        {
            writer.WriteLine(GetSerialString(start));

            foreach (var child in children)
            {
                child.DoWriteNode(writer, start);
            }
        }

        //PUBLIC METHODS
        public void WriteTree(StreamWriter writer)
        {
            if (writer is null) { throw new ArgumentNullException(nameof(writer)); }

            DoWriteNode(writer, Depth);
        }
        public ObjectTreeNode<T> AddChild(T value)
        {
            var child = new ObjectTreeNode<T>(this, value);

            children.Add(child);

            return child;
        }
        public ObjectTreeNode<T> AddSibling(T value) => parent.AddChild(value);
        public IEnumerator<ObjectTreeNode<T>> GetEnumerator() => new ObjectTreeNodeEnumerator<T>(this);
        IEnumerator IEnumerable.GetEnumerator() => new ObjectTreeNodeEnumerator<T>(this);
        public override string ToString() => Value.ToString();

        //STATIC METHODS
        public static ObjectTreeNode<T> CreateRoot(T value)
        {
            return new ObjectTreeNode<T>(null, value);
        }
        public static ObjectTreeNode<T> ReadTree(StreamReader reader, Func<string, int, T> parseFunc)
        {
            if (reader is null) { throw new ArgumentNullException(nameof(reader)); }

            var match = new TreeNodeMatch(parseFunc, reader.ReadLine(), 1);

            var node = new ObjectTreeNode<T>(null, match.Value);
            var ret = node;
            int currDepth = match.Depth, diff, x;
            int n = 1;

            while (!reader.EndOfStream)
            {
                match = new TreeNodeMatch(parseFunc, reader.ReadLine(), ++n);

                diff = currDepth - match.Depth;

                if (diff == 0) //at same level
                {
                    if (node.IsRoot) { throw new IOException("Cannnot have two nodes which are both at 'root' level, line: " + n); }

                    node = node.Parent.AddChild(match.Value);
                }
                else if (diff == -1) //moving to children
                {
                    node = node.AddChild(match.Value);
                }
                else if (diff > 0) //going back up
                {
                    for (x = 0; x <= diff; x++)
                    {
                        node = node.parent;
                    }

                    node = node.AddChild(match.Value);
                }
                else { throw new IOException("Tree structue cannot jump in depth, bypassing creating children, line: " + n); } //diff is < -1

                currDepth = match.Depth;
            }

            return ret;
        }

        //NESTED STRCUTS
        private readonly struct TreeNodeMatch
        {
            //STATIC PROPERTIES
            private static readonly System.Text.RegularExpressions.Regex lineRegex = new System.Text.RegularExpressions.Regex(@"^((?<depth>\|\s)*)(?<value>\S+)\s*($|;|#)", RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

            //PUBLIC PROPERTIES
            public int Depth { get; }
            public T Value { get; }
            public Match Match { get; }

            //CONSTRCUTORS
            public TreeNodeMatch(Func<string, int, T> parseFunc, string line, int n)
            {
                Match = lineRegex.Match(line);

                if (!Match.Success) { throw new IOException($"Line {n} does not apprear to be part of a StringTree structure."); }

                Depth = Match.Groups["depth"].Captures.Count;

                try
                {
                    Value = parseFunc.Invoke(Match.Groups[2].Value, n);
                }
                catch (TargetInvocationException e)
                {
                    throw new IOException($"Parse function failed on line {n}: " + e.InnerException.Message);
                }
            }
        }
    }
    
    /// <summary>
    /// Enumerates an ObjectTreeNode<T> and all nodes in its subtree.
    /// Supports Depth-First (default) and Breadth-First traversal.
    /// </summary>
    public class ObjectTreeNodeEnumerator<T> : IEnumerator<ObjectTreeNode<T>>, IEnumerable<ObjectTreeNode<T>>
    {
        public enum Traversal
        {
            DepthFirst,
            BreadthFirst
        }

        private readonly ObjectTreeNode<T> root;
        private readonly Traversal traversal;

        // State containers for traversal:
        // - For DFS we use a Stack<ObjectTreeNode<T>>
        // - For BFS we use a Queue<ObjectTreeNode<T>>
        private Stack<ObjectTreeNode<T>> dfsStack;
        private Queue<ObjectTreeNode<T>> bfsQueue;

        private ObjectTreeNode<T> current;

        /// <summary>
        /// Create an enumerator beginning at <paramref name="root"/> using the requested traversal.
        /// </summary>
        public ObjectTreeNodeEnumerator(ObjectTreeNode<T> root, Traversal traversal = Traversal.DepthFirst)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.traversal = traversal;
            Reset();
        }

        /// <summary>
        /// Returns the current node (typed).
        /// </summary>
        public ObjectTreeNode<T> Current => current;

        object IEnumerator.Current => Current;

        /// <summary>
        /// Advance to the next node in the traversal.
        /// </summary>
        public bool MoveNext()
        {
            switch (traversal)
            {
                case Traversal.DepthFirst:
                    return MoveNextDfs();
                case Traversal.BreadthFirst:
                    return MoveNextBfs();
                default:
                    throw new InvalidOperationException("Unknown traversal type.");
            }
        }

        private bool MoveNextDfs()
        {
            if (dfsStack.Count == 0)
            {
                current = null;
                return false;
            }

            // Pop next node
            var node = dfsStack.Pop();
            current = node;

            // Push children in reverse order so the first child is visited first (preserve Dir() order).
            // We rely on children being accessible via Children (IReadOnlyList) and Count property.
            var children = node.Children;
            for (int i = children.Count - 1; i >= 0; i--)
            {
                dfsStack.Push(children[i]);
            }

            return true;
        }

        private bool MoveNextBfs()
        {
            if (bfsQueue.Count == 0)
            {
                current = null;
                return false;
            }

            var node = bfsQueue.Dequeue();
            current = node;

            // Enqueue children in natural order so they are processed FIFO.
            foreach (var child in node.Children)
            {
                bfsQueue.Enqueue(child);
            }

            return true;
        }

        /// <summary>
        /// Reset enumerator to initial state (before the root).
        /// </summary>
        public void Reset()
        {
            current = null;

            if (traversal == Traversal.DepthFirst)
            {
                dfsStack = new Stack<ObjectTreeNode<T>>();
                // Push the root so it is returned first.
                dfsStack.Push(root);
                bfsQueue = null;
            }
            else
            {
                bfsQueue = new Queue<ObjectTreeNode<T>>();
                bfsQueue.Enqueue(root);
                dfsStack = null;
            }
        }

        public void Dispose()
        {
            // Nothing to dispose in this simple implementation.
            // If you later add unmanaged resources, free them here.
        }

        // IEnumerable support so you can use "foreach (var n in new ObjectTreeNodeEnumerator<T>(root))"
        public IEnumerator<ObjectTreeNode<T>> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}