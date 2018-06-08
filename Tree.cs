using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Mandelmesh
{
    public struct TreeCoord
    {
        const double _mboxDiameter = 4.1;
        public TreeCoord(int x, int y, int z, int depth)
        {
            X = x;
            Y = y;
            Z = z;
            Depth = depth;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public int Depth { get; }

        public TreeCoord[] Children() => new[]
            {
                new TreeCoord(X * 2 + 0, Y * 2 + 0, Z * 2 + 0, Depth + 1),
                new TreeCoord(X * 2 + 1, Y * 2 + 0, Z * 2 + 0, Depth + 1),
                new TreeCoord(X * 2 + 0, Y * 2 + 1, Z * 2 + 0, Depth + 1),
                new TreeCoord(X * 2 + 1, Y * 2 + 1, Z * 2 + 0, Depth + 1),
                new TreeCoord(X * 2 + 0, Y * 2 + 0, Z * 2 + 1, Depth + 1),
                new TreeCoord(X * 2 + 1, Y * 2 + 0, Z * 2 + 1, Depth + 1),
                new TreeCoord(X * 2 + 0, Y * 2 + 1, Z * 2 + 1, Depth + 1),
                new TreeCoord(X * 2 + 1, Y * 2 + 1, Z * 2 + 1, Depth + 1),
            };

        public double ScaleLocalToGlobal(double value)
        {
            value /= Math.Pow(2, Depth);
            value *= _mboxDiameter;
            return value;
        }

        public Vector TransformLocalToGlobal(Vector value)
        {
            value += new Vector(X, Y, Z);
            value /= Math.Pow(2, Depth);

            value -= new Vector(0.5, 0.5, 0.5);
            value *= _mboxDiameter;

            return value;
        }
    }

    public class Tree
    {
        // public accessor for enumeration only
        public Dictionary<TreeCoord, Chunk> Chunks { get; }
        private readonly TreeCoord _root;
        private readonly ConcurrentQueue<TreeCoord> _toSplit; // main -> worker thread
        // del, addIndex, addValue
        private readonly ConcurrentQueue<(TreeCoord, TreeCoord[], Chunk[])> _toAdd; // worker -> main thread

        public Tree(TreeCoord root)
        {
            _root = root;
            Chunks = new Dictionary<TreeCoord, Chunk>();
            _toSplit = new ConcurrentQueue<TreeCoord>();
            _toAdd = new ConcurrentQueue<(TreeCoord, TreeCoord[], Chunk[])>();
            _toSplit.Enqueue(_root);
            new Thread(WorkLoop) { IsBackground = true }.Start();
        }

        private void WorkLoop()
        {
            var surfaceNet = new SurfaceNet();
            while (true)
            {
                if (_toSplit.TryDequeue(out var result))
                {
                    WorkOne(result, surfaceNet);
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
        }

        private void WorkOne(TreeCoord input, SurfaceNet surfaceNet)
        {
            var children = input.Children();
            var result = new Chunk[children.Length];
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                Console.WriteLine($"Generating {child.X} {child.Y} {child.Z} {child.Depth}");
                var chunk = new Chunk(child);
                chunk.Render(surfaceNet);
                result[i] = chunk;
            }
            _toAdd.Enqueue((input, children, result));
        }

        public void Refresh(Action<Chunk> finalize)
        {
            while (_toAdd.TryDequeue(out var result))
            {
                var (toDel, addIndex, addValue) = result;
                if (Chunks.ContainsKey(toDel))
                {
                    var existing = Chunks[toDel];
                    Chunks.Remove(toDel);
                    existing.Dispose();
                }
                for (var i = 0; i < addIndex.Length; i++)
                {
                    finalize(addValue[i]);
                    Chunks.Add(addIndex[i], addValue[i]);
                    //if (addIndex[i].Depth < 1)
                    //{
                    //    _toSplit.Enqueue(addIndex[i]);
                    //}
                }
            }
        }
    }
}
