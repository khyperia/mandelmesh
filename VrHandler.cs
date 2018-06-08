using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Valve.VR;

namespace Mandelmesh
{
    class VrControls
    {
        float oldDistance;

        public VrControls()
        {
        }

        public void Update(Matrix left, Matrix right)
        {
            var leftCenter = Vector3.Transform(new Vector3(0, 0, 0), left);
            var rightCenter = Vector3.Transform(new Vector3(0, 0, 0), right);
            var diff = leftCenter - rightCenter;
            var distance = diff.Length();
            oldDistance = distance;
            Console.WriteLine(oldDistance);
        }

        public Matrix GetMatrix()
        {
            return Matrix.CreateScale(oldDistance);
        }
    }

    class VrHandler
    {
        static VrHandler()
        {
            var error = EVRInitError.None;
            OpenVR.Init(ref error);
            if (error != EVRInitError.None)
            {
                throw new Exception($"VR Init Error: {error}");
            }
        }

        public static void Shutdown() => OpenVR.Shutdown();

        private readonly VrControls _vrControls;
        private readonly CVRSystem _vrSystem;
        private readonly CVRCompositor _vrCompositor;
        private readonly uint _width;
        private readonly uint _height;
        private readonly TrackedDevicePose_t[] _renderPose = new TrackedDevicePose_t[3];
        private readonly TrackedDevicePose_t[] _gamePose = new TrackedDevicePose_t[3];
        private BasicEffect _leftEffect;
        private BasicEffect _rightEffect;
        private RenderTarget2D _leftTarget;
        private RenderTarget2D _rightTarget;
        private IntPtr _leftTargetHandle;
        private IntPtr _rightTargetHandle;
        public Matrix ViewModel { get; private set; }

        public VrHandler()
        {
            _vrControls = new VrControls();
            _vrSystem = OpenVR.System;
            _vrCompositor = OpenVR.Compositor;
            _vrSystem.GetRecommendedRenderTargetSize(ref _width, ref _height);
        }

        private void Check(EVRCompositorError error, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            if (error != EVRCompositorError.None)
            {
                throw new Exception($"OpenVR error {error} in {memberName} at {filePath}:{lineNumber}");
            }
        }

        public void Go(GraphicsDevice graphicsDevice, Action<BasicEffect> render)
        {
            Check(_vrCompositor.WaitGetPoses(_renderPose, _gamePose));
            var left = Matrix.Transpose(ToMonogameMatrix(_renderPose[1].mDeviceToAbsoluteTracking));
            var right = Matrix.Transpose(ToMonogameMatrix(_renderPose[2].mDeviceToAbsoluteTracking));
            _vrControls.Update(left, right);

            Render(EVREye.Eye_Left, ref _leftEffect, ref _leftTarget, ref _leftTargetHandle, graphicsDevice, _renderPose[0], render);
            Render(EVREye.Eye_Right, ref _rightEffect, ref _rightTarget, ref _rightTargetHandle, graphicsDevice, _renderPose[0], render);
        }

        private Matrix ToMonogameMatrix(HmdMatrix44_t mat)
        {
            var output = new Matrix(mat.m0, mat.m1, mat.m2, mat.m3, mat.m4, mat.m5, mat.m6, mat.m7, mat.m8, mat.m9, mat.m10, mat.m11, mat.m12, mat.m13, mat.m14, mat.m15);
            return output;
        }

        private Matrix ToMonogameMatrix(HmdMatrix34_t mat)
        {
            var output = new Matrix(mat.m0, mat.m1, mat.m2, mat.m3, mat.m4, mat.m5, mat.m6, mat.m7, mat.m8, mat.m9, mat.m10, mat.m11, 0, 0, 0, 1);
            return output;
        }

        private static FieldInfo _renderTargetGlTextureFieldInfo;

        private int GetGlHandle(RenderTarget2D target)
        {
            if (_renderTargetGlTextureFieldInfo == null)
            {
                _renderTargetGlTextureFieldInfo = target.GetType().GetField("glTexture", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            return (int)_renderTargetGlTextureFieldInfo.GetValue(target);
        }

        private Matrix GetProjection(EVREye eye)
        {
            float left = 0, right = 0, top = 0, bottom = 0;
            var near = 0.5f;
            var far = 20.0f;
            _vrSystem.GetProjectionRaw(eye, ref left, ref right, ref top, ref bottom);
            left *= near;
            right *= near;
            top *= near;
            bottom *= near;
            return Matrix.CreatePerspectiveOffCenter(left, right, bottom, top, near, far);
        }

        private Matrix GetEyeView(EVREye eye)
        {
            // instead of Model * View * Projection the model is Model * View * Eye * Projection
            return ToMonogameMatrix(_vrSystem.GetEyeToHeadTransform(eye));
        }

        private void Render(EVREye eye, ref BasicEffect effect, ref RenderTarget2D renderTarget, ref IntPtr renderTargetHandle, GraphicsDevice graphicsDevice, TrackedDevicePose_t device, Action<BasicEffect> render)
        {
            if (effect == null)
            {
                effect = new BasicEffect(graphicsDevice);
            }

            if (renderTarget == null)
            {
                renderTarget = new RenderTarget2D(graphicsDevice, (int)_width, (int)_height, false, SurfaceFormat.Color, DepthFormat.Depth16, 1, RenderTargetUsage.DiscardContents, false);
                renderTargetHandle = (IntPtr)GetGlHandle(renderTarget);
            }

            var viewMatrix = Matrix.Invert(Matrix.Transpose(ToMonogameMatrix(device.mDeviceToAbsoluteTracking)));
            var modelMatrix = _vrControls.GetMatrix();
            var matrix = modelMatrix * viewMatrix * GetEyeView(eye) * GetProjection(eye);
            ViewModel = modelMatrix * viewMatrix;

            effect.Projection = matrix;
            effect.View = Matrix.Identity;
            effect.World = Matrix.Identity;
            effect.VertexColorEnabled = true;

            graphicsDevice.SetRenderTarget(renderTarget);
            render(effect);
            graphicsDevice.SetRenderTarget(null);

            var tex = new Texture_t
            {
                eColorSpace = EColorSpace.Auto,
                eType = ETextureType.OpenGL,
                handle = renderTargetHandle,
            };
            Check(_vrCompositor.Submit(eye, ref tex, IntPtr.Zero, EVRSubmitFlags.Submit_Default)); // TODO: GlRenderBuffer?
        }
    }
}
