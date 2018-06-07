using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mandelmesh
{
    public class Chunk : IDisposable
    {
        private readonly TreeCoord _coord;
        private Vector[] _vertices;
        private Vector[] _normals;
        private int[] _indices;
        VertexBuffer _vertexBuffer;
        IndexBuffer _indexBuffer;

        public Chunk(TreeCoord coord)
        {
            _coord = coord;
        }

        public void Render()
        {
            var net = new SurfaceNet(Mandelbox.De, Mandelbox.Normal, _coord, Constants.GridResolution);
            net.Go(out _indices, out _vertices, out _normals);
        }

        public void Upload(GraphicsDevice device)
        {
            if (_indices.Length == 0)
            {
                Console.WriteLine("Empty chunk");
                return;
            }

            var convertedVerts = new VertexPositionColor[_vertices.Length];
            for (var i = 0; i < convertedVerts.Length; i++)
            {
                var vert = _vertices[i];
                var norm = _normals[i];
                convertedVerts[i] = new VertexPositionColor(
                    position: new Vector3((float)vert.X, (float)vert.Y, (float)vert.Z),
                    color: new Color((float)norm.X, (float)norm.Y, (float)norm.Z));
            }
            _vertexBuffer = new VertexBuffer(device, typeof(VertexPositionColor), convertedVerts.Length, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(convertedVerts);

            // var convertedVerts = new VertexPosition[_vertices.Length];
            // for (var i = 0; i < convertedVerts.Length; i++)
            // {
            //     var vert = _vertices[i];
            //     var norm = _normals[i];
            //     convertedVerts[i] = new VertexPosition(
            //         position: new Vector3((float)vert.X, (float)vert.Y, (float)vert.Z));
            // }
            // _vertexBuffer = new VertexBuffer(device, typeof(VertexPosition), convertedVerts.Length, BufferUsage.WriteOnly);
            // _vertexBuffer.SetData(convertedVerts);

            _indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, _indices.Length, BufferUsage.WriteOnly);
            _indexBuffer.SetData(_indices);
        }

        public void Draw(GraphicsDevice device, BasicEffect basicEffect)
        {
            device.Indices = _indexBuffer;
            device.SetVertexBuffer(_vertexBuffer);
            device.RasterizerState = new RasterizerState() { CullMode = CullMode.None };

            foreach (var pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawIndexedPrimitives(PrimitiveType.LineList, 0, 0, _indices.Length / 3);
            }
        }

        public void Dispose()
        {
            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();
        }
    }
}
