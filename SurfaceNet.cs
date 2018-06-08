using System;
using System.Collections.Generic;
using System.Diagnostics;

// This class is based on:
// https://0fps.net/2012/07/12/smooth-voxel-terrain-part-2/

// Vertex is at center of box, iff verts of box are not all uniform.
// Create faces based on positive/negative values, between centers of boxes. (Get a shifted-by-0.5 minecraft world)
// But actually, move the vertex:
//     Calculate the points of all edge crossings on edges of box.
//     Place vertex at center-of-mass of all edge crossing points.

namespace Mandelmesh
{
    public class SurfaceNet
    {
        private Func<Vector, double> _field;
        private Func<Vector, Vector> _normalField;
        private TreeCoord _coords;
        private uint _resolution;
        private List<int> _cachedInd;
        private List<Vector> _cachedVert;
        private List<Vector> _cachedNorm;

        public SurfaceNet()
        {
            _cachedInd = new List<int>();
            _cachedVert = new List<Vector>();
            _cachedNorm = new List<Vector>();
        }

        public void Update(Func<Vector, double> field, Func<Vector, Vector> normalField, TreeCoord coords, uint resolution)
        {
            _field = field;
            _normalField = normalField;
            _coords = coords;
            _resolution = resolution;
        }

        public void Go(out int[] quadIndices, out Vector[] vertices, out Vector[] normals)
        {
            // this shouldn't reset capacity
            _cachedInd.Clear();
            _cachedVert.Clear();
            _cachedNorm.Clear();
            Go(_cachedInd, _cachedVert, _cachedNorm);
            quadIndices = _cachedInd.ToArray();
            vertices = _cachedVert.ToArray();
            normals = _cachedNorm.ToArray();
        }

        private void Go(List<int> quadIndices, List<Vector> vertices, List<Vector> normals)
        {
            var timer = Stopwatch.StartNew();
            var subtractAmount = _coords.ScaleLocalToGlobal(1.0 / _resolution) * 2;

            var gridValues = new double[_resolution, _resolution, _resolution];
            for (var z = 0; z < _resolution; z++)
            {
                for (var y = 0; y < _resolution; y++)
                {
                    for (var x = 0; x < _resolution; x++)
                    {
                        gridValues[x, y, z] = _field(_coords.TransformLocalToGlobal(new Vector(x, y, z)/(_resolution-2))) - subtractAmount;
                    }
                }
            }

            var sampleTime = timer.Elapsed.TotalMilliseconds;

            // Find the location of all vertices
            var vertIndecies = new int[_resolution - 1, _resolution - 1, _resolution - 1];
            for (var z = 0; z < _resolution - 1; z++)
            {
                for (var y = 0; y < _resolution - 1; y++)
                {
                    for (var x = 0; x < _resolution - 1; x++)
                    {
                        // Take the center of mass of all edges that pass through the cube's edge, that's the vert coord
                        var count = 0;
                        var sum = new Vector(0, 0, 0);

                        if (FindEdge(gridValues, x, y, z, 0, 0, 0, 1, 0, 0, out var v1)) { sum += v1; count++; }
                        if (FindEdge(gridValues, x, y, z, 0, 0, 0, 0, 1, 0, out var v2)) { sum += v2; count++; }
                        if (FindEdge(gridValues, x, y, z, 0, 0, 0, 0, 0, 1, out var v3)) { sum += v3; count++; }

                        if (FindEdge(gridValues, x, y, z, 1, 0, 0, 1, 1, 0, out var v4)) { sum += v4; count++; }
                        if (FindEdge(gridValues, x, y, z, 1, 0, 0, 1, 0, 1, out var v5)) { sum += v5; count++; }

                        if (FindEdge(gridValues, x, y, z, 0, 1, 0, 1, 1, 0, out var v6)) { sum += v6; count++; }
                        if (FindEdge(gridValues, x, y, z, 0, 1, 0, 0, 1, 1, out var v7)) { sum += v7; count++; }

                        if (FindEdge(gridValues, x, y, z, 0, 0, 1, 1, 0, 1, out var v8)) { sum += v8; count++; }
                        if (FindEdge(gridValues, x, y, z, 0, 0, 1, 0, 1, 1, out var v9)) { sum += v9; count++; }

                        if (FindEdge(gridValues, x, y, z, 1, 1, 0, 1, 1, 1, out var va)) { sum += va; count++; }
                        if (FindEdge(gridValues, x, y, z, 1, 0, 1, 1, 1, 1, out var vb)) { sum += vb; count++; }
                        if (FindEdge(gridValues, x, y, z, 0, 1, 1, 1, 1, 1, out var vc)) { sum += vc; count++; }

                        if (count > 0)
                        {
                            vertIndecies[x, y, z] = vertices.Count;
                            var vertPoint = sum / count + new Vector(x, y, z);
                            vertPoint = _coords.TransformLocalToGlobal(vertPoint/(_resolution-2));
                            vertices.Add(vertPoint);
                        }
                        else
                        {
                            vertIndecies[x, y, z] = -1;
                        }
                    }
                }
            }

            // Calculate faces
            for (var z = 0; z < _resolution - 2; z++)
            {
                for (var y = 0; y < _resolution - 2; y++)
                {
                    for (var x = 0; x < _resolution - 1; x++)
                    {
                        if ((gridValues[x + 0, y + 1, z + 1] <= 0) != (gridValues[x + 1, y + 1, z + 1] <= 0))
                        {
                            quadIndices.Add(vertIndecies[x + 0, y + 0, z + 0]);
                            quadIndices.Add(vertIndecies[x + 0, y + 0, z + 1]);
                            quadIndices.Add(vertIndecies[x + 0, y + 1, z + 1]);

                            quadIndices.Add(vertIndecies[x + 0, y + 0, z + 0]);
                            quadIndices.Add(vertIndecies[x + 0, y + 1, z + 1]);
                            quadIndices.Add(vertIndecies[x + 0, y + 1, z + 0]);
                        }
                    }
                }
            }

            for (var z = 0; z < _resolution - 2; z++)
            {
                for (var y = 0; y < _resolution - 1; y++)
                {
                    for (var x = 0; x < _resolution - 2; x++)
                    {
                        if ((gridValues[x + 1, y + 0, z + 1] <= 0) != (gridValues[x + 1, y + 1, z + 1] <= 0))
                        {
                            quadIndices.Add(vertIndecies[x + 0, y + 0, z + 0]);
                            quadIndices.Add(vertIndecies[x + 0, y + 0, z + 1]);
                            quadIndices.Add(vertIndecies[x + 1, y + 0, z + 1]);

                            quadIndices.Add(vertIndecies[x + 0, y + 0, z + 0]);
                            quadIndices.Add(vertIndecies[x + 1, y + 0, z + 1]);
                            quadIndices.Add(vertIndecies[x + 1, y + 0, z + 0]);
                        }
                    }
                }
            }

            for (var z = 0; z < _resolution - 1; z++)
            {
                for (var y = 0; y < _resolution - 2; y++)
                {
                    for (var x = 0; x < _resolution - 2; x++)
                    {
                        if ((gridValues[x + 1, y + 1, z + 0] <= 0) != (gridValues[x + 1, y + 1, z + 1] <= 0))
                        {
                            quadIndices.Add(vertIndecies[x + 0, y + 0, z + 0]);
                            quadIndices.Add(vertIndecies[x + 0, y + 1, z + 0]);
                            quadIndices.Add(vertIndecies[x + 1, y + 1, z + 0]);

                            quadIndices.Add(vertIndecies[x + 0, y + 0, z + 0]);
                            quadIndices.Add(vertIndecies[x + 1, y + 1, z + 0]);
                            quadIndices.Add(vertIndecies[x + 1, y + 0, z + 0]);
                        }
                    }
                }
            }

            var quadTime = timer.Elapsed.TotalMilliseconds - sampleTime;

            foreach (var position in vertices)
            {
                normals.Add(_normalField(position));
            }

            var normalTime = timer.Elapsed.TotalMilliseconds - (sampleTime + quadTime);
            Console.WriteLine($"inds:{quadIndices.Count,-10} verts:{vertices.Count,-10} sample:{sampleTime,-10:N2} quad_find:{quadTime,-10:N2} normal_gen:{normalTime,-10:N2}");
        }

        // returns value in range [0-1], for all three dims
        // i.e. (x,y,z) is *not* added to the result.
        private static bool FindEdge(
            double[,,] grid,
            int x, int y, int z,
            int dx1, int dy1, int dz1,
            int dx2, int dy2, int dz2,
            out Vector point)
        {
            var v1 = grid[x + dx1, y + dy1, z + dz1];
            var v2 = grid[x + dx2, y + dy2, z + dz2];
            if ((v1 <= 0) == (v2 <= 0))
            {
                point = default(Vector);
                return false;
            }
            // y = m * x + b
            // 0 = (v2 - v1) * x + v1
            // x = -v1 / (v2 - v1)
            // x = v1 / (v1 - v2)
            var edgeDist = v1 / (v1 - v2);
            var d1 = new Vector(dx1, dy1, dz1);
            var d2 = new Vector(dx2, dy2, dz2);
            point = d1 * (1 - edgeDist) + d2 * edgeDist;
            return true;
        }
    }
}
