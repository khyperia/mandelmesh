using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace Mandelmesh
{
    static class Program
    {
        public static void Main()
        {
            using (var example = new Display())
            {
                example.Run();
            }
        }
    }

    public class FpsTracker
    {
        double _spf;
        double _secondTracker;

        public bool GetFps(double totalSeconds, out double fps)
        {
            _secondTracker += totalSeconds;
            const double weight = 10.0;
            _spf = (_spf * weight + totalSeconds) / (weight + 1);
            fps = 1.0 / _spf;
            if (_secondTracker > 1)
            {
                _secondTracker %= 1;
                return true;
            }
            return false;
        }
    }

    public class Display : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private BasicEffect _basicEffect;
        private readonly Tree _tree;
        private VrHandler _vrHandler;
        private Action<Chunk> _treeRefreshFinalizer;
        private Action<BasicEffect> _draw;
        private readonly FpsTracker _fpsTracker;

        public Display()
        {
            _graphics = new GraphicsDeviceManager(this);
            _tree = new Tree(new TreeCoord(0, 0, 0, 0));
            _fpsTracker = new FpsTracker();
            _graphics.SynchronizeWithVerticalRetrace = false;
            this.IsFixedTimeStep = false;
            Window.AllowUserResizing = true;
        }

        protected override void LoadContent()
        {
            base.LoadContent();
            _basicEffect = new BasicEffect(GraphicsDevice);
            _treeRefreshFinalizer = chunk => chunk.Upload(GraphicsDevice);
            _draw = effect =>
                {
                    GraphicsDevice.Clear(Color.CornflowerBlue);
                    foreach (var chunk in _tree.Chunks)
                    {
                        chunk.Value.Draw(GraphicsDevice, effect);
                    }
                };
        }

        protected override void Update(GameTime gameTime)
        {
            _tree.Refresh(_treeRefreshFinalizer);
            base.Update(gameTime);

            var keyboard = Keyboard.GetState();
            //if (keyboard.IsKeyDown(Keys.Space))
            {
                if (_vrHandler == null)
                {
                    JitBarrier();
                }
            }
        }

        private void JitBarrier() => _vrHandler = new VrHandler();

        protected override void Draw(GameTime gameTime)
        {
            if (_fpsTracker.GetFps(gameTime.ElapsedGameTime.TotalSeconds, out var fps))
            {
                Window.Title = $"Mandelmesh - {fps} fps";
            }

            if (_vrHandler != null)
            {
                _vrHandler.Go(GraphicsDevice, _draw);
            }

            var time = gameTime.TotalGameTime.TotalSeconds;
            var aspect = (float)Window.ClientBounds.Width / Window.ClientBounds.Height;
            _basicEffect.World = Matrix.Identity;
            //_basicEffect.View = Matrix.CreateLookAt(new Vector3((float)Math.Cos(time / 10) * 5, 0.5f, (float)Math.Sin(time / 10) * 5), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
            //_basicEffect.View = Matrix.CreateLookAt(new Vector3(4, 4, 4), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
            _basicEffect.View = _vrHandler.ViewModel;
            //_basicEffect.View = Matrix.Identity;
            _basicEffect.Projection = Matrix.CreatePerspectiveFieldOfView((float)Math.PI / 4, aspect, 0.01f, 5);
            _basicEffect.VertexColorEnabled = true;

            _draw(_basicEffect);

            base.Draw(gameTime);
        }
    }
}
