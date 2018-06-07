using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Mandelmesh
{
    static class Program
    {
        [STAThread]
        public static void Main()
        {
            using (var example = new Display())
            {
                example.Run();
            }
        }
    }

    public class Display : Game
    {
        GraphicsDeviceManager graphics;
        private BasicEffect _basicEffect;
        private readonly Tree _tree;

        public Display()
        {
            graphics = new GraphicsDeviceManager(this);
            _tree = new Tree(new TreeCoord(0, 0, 0, 0));
            Window.AllowUserResizing = true;
        }

        protected override void LoadContent()
        {
            base.LoadContent();
            _basicEffect = new BasicEffect(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            _tree.Refresh(chunk => chunk.Upload(GraphicsDevice));
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            var time = gameTime.TotalGameTime.TotalSeconds;
            var aspect = (float)Window.ClientBounds.Width / Window.ClientBounds.Height;
            _basicEffect.World = Matrix.Identity;
            _basicEffect.View = Matrix.CreateLookAt(new Vector3((float)Math.Cos(time / 10) * 5, 0.5f, (float)Math.Sin(time / 10) * 5), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
            _basicEffect.Projection = Matrix.CreatePerspectiveFieldOfView((float)Math.PI / 4, aspect, 1, 100);
            _basicEffect.VertexColorEnabled = true;

            foreach (var chunk in _tree.Chunks)
            {
                chunk.Draw(GraphicsDevice, _basicEffect);
            }

            base.Draw(gameTime);
        }
    }
}
