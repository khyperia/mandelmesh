using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Valve.VR;

namespace Mandelmesh
{
    class VrControls
    {
        Vector3 _oldLeft;
        Vector3 _oldRight;
        Matrix _model;

        public VrControls()
        {
            _model = Matrix.Identity;
            _oldLeft = -Vector3.UnitX;
            _oldRight = Vector3.UnitX;
        }

        public void Update(VRControllerState_t leftControllerState, Matrix left, VRControllerState_t rightControllerState, Matrix right)
        {
            var newLeft = Vector3.Transform(new Vector3(0, 0, 0), left);
            var newRight = Vector3.Transform(new Vector3(0, 0, 0), right);

            if (newLeft == Vector3.Zero || newRight == Vector3.Zero || newLeft == newRight)
            {
                return;
            }
            var leftGrabbed = leftControllerState.rAxis1.x == 1;
            var rightGrabbed = rightControllerState.rAxis1.x == 1;
            if (!leftGrabbed || !rightGrabbed)
            {
                _oldLeft = newLeft;
                _oldRight = newRight;
                return;
            }
            var oldDiff = _oldLeft - _oldRight;
            var newDiff = newLeft - newRight;
            var oldCenter = (_oldLeft + _oldRight) / 2;
            var newCenter = (newLeft + newRight) / 2;
            var axis = Vector3.Cross(oldDiff, newDiff);
            if (axis == Vector3.Zero)
            {
                axis = Vector3.UnitX;
            }
            else
            {
                axis = Vector3.Normalize(axis);
            }
            // oldDiff/newDiff guaranteed to be nonzero (at check at beginning of method)
            var dotAngle = Vector3.Dot(Vector3.Normalize(oldDiff), Vector3.Normalize(newDiff));
            var angle = (float)Math.Acos(Math.Max(-1, Math.Min(1, dotAngle)));
            // matrix mul order goes left to right
            var rotation = Matrix.CreateFromAxisAngle(axis, angle);
            var scale = Matrix.CreateScale(newDiff.Length() / oldDiff.Length());
            var translation = Matrix.CreateTranslation(newCenter - oldCenter);
            var res = Matrix.CreateTranslation(-oldCenter) * rotation * scale * Matrix.CreateTranslation(oldCenter) * translation;
            _model = _model * res;
            _oldLeft = newLeft;
            _oldRight = newRight;

            if (double.IsNaN(res.M11) || Keyboard.GetState().IsKeyDown(Keys.Space))
            {
                //Console.WriteLine("---");
                //Console.WriteLine("left:" + newLeft);
                //Console.WriteLine("right:" + newRight);
                //Console.WriteLine("axis:" + axis);
                //Console.WriteLine("dotAngle:" + dotAngle);
                //Console.WriteLine("angle:" + angle);
                //Console.WriteLine("rot:" + rotation);
                //Console.WriteLine("scale:" + scale);
                //Console.WriteLine("trans:" + translation);
                //Console.WriteLine("res:" + res);
                //Console.WriteLine("model:" + _model);
                _model = Matrix.Identity;
            }
        }

        public Matrix GetMatrix() => _model;
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
        private TrackedDevicePose_t[] _renderPose;
        private TrackedDevicePose_t[] _gamePose;
        //private readonly float _frameDuration;
        //private readonly float _vsyncToPhotons;
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
            _renderPose = new TrackedDevicePose_t[1];
            _gamePose = new TrackedDevicePose_t[1];
            //var error = default(ETrackedPropertyError);
            //var displayFrequency = _vrSystem.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DisplayFrequency_Float, ref error);
            //Check(error);
            //_frameDuration = 1.0f / displayFrequency;
            //_vsyncToPhotons = _vrSystem.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_SecondsFromVsyncToPhotons_Float, ref error);
            //Check(error);
        }

        private void Check(EVRCompositorError error, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            if (error != EVRCompositorError.None)
            {
                throw new Exception($"OpenVR error {error} in {memberName} at {filePath}:{lineNumber}");
            }
        }

        private void Check(ETrackedPropertyError error, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            if (error != ETrackedPropertyError.TrackedProp_Success)
            {
                throw new Exception($"OpenVR error {error} in {memberName} at {filePath}:{lineNumber}");
            }
        }

        public void Go(GraphicsDevice graphicsDevice, Action<BasicEffect> render)
        {
            var leftHandIndex = _vrSystem.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
            var rightHandIndex = _vrSystem.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
            var maxHandIndex = Math.Max(leftHandIndex, rightHandIndex);
            if (maxHandIndex >= _renderPose.Length && maxHandIndex != uint.MaxValue)
            {
                _renderPose = new TrackedDevicePose_t[maxHandIndex + 1];
                _gamePose = new TrackedDevicePose_t[maxHandIndex + 1];
            }
            Check(_vrCompositor.WaitGetPoses(_renderPose, _gamePose));
            if (leftHandIndex != uint.MaxValue && rightHandIndex != uint.MaxValue)
            {
                var leftControllerState = default(VRControllerState_t);
                _vrSystem.GetControllerState(leftHandIndex, ref leftControllerState, (uint)Marshal.SizeOf<VRControllerState_t>());
                var rightControllerState = default(VRControllerState_t);
                _vrSystem.GetControllerState(rightHandIndex, ref rightControllerState, (uint)Marshal.SizeOf<VRControllerState_t>());
                var left = Matrix.Transpose(ToMonogameMatrix(_renderPose[leftHandIndex].mDeviceToAbsoluteTracking));
                var right = Matrix.Transpose(ToMonogameMatrix(_renderPose[rightHandIndex].mDeviceToAbsoluteTracking));
                _vrControls.Update(leftControllerState, left, rightControllerState, right);
            }

            Render(EVREye.Eye_Left, ref _leftEffect, ref _leftTarget, ref _leftTargetHandle, graphicsDevice, _renderPose[0], render);
            Render(EVREye.Eye_Right, ref _rightEffect, ref _rightTarget, ref _rightTargetHandle, graphicsDevice, _renderPose[0], render);
        }

        //private float SecondsUntilPhotons()
        //{
        //    // https://github.com/ValveSoftware/openvr/wiki/IVRSystem::GetDeviceToAbsoluteTrackingPose
        //    var secondsSinceLastVsync = 0f;
        //    var pulFrameCounter = 0UL;
        //    _vrSystem.GetTimeSinceLastVsync(ref secondsSinceLastVsync, ref pulFrameCounter);
        //    var predictedSecondsFromNow = _frameDuration - secondsSinceLastVsync + _vsyncToPhotons;
        //    return predictedSecondsFromNow;
        //}

        private Matrix ToMonogameMatrix(HmdMatrix44_t mat)
        {
            var output = new Matrix(
                mat.m0, mat.m1, mat.m2, mat.m3,
                mat.m4, mat.m5, mat.m6, mat.m7,
                mat.m8, mat.m9, mat.m10, mat.m11,
                mat.m12, mat.m13, mat.m14, mat.m15);
            return output;
        }

        private Matrix ToMonogameMatrix(HmdMatrix34_t mat)
        {
            var output = new Matrix(
                mat.m0, mat.m1, mat.m2, mat.m3,
                mat.m4, mat.m5, mat.m6, mat.m7,
                mat.m8, mat.m9, mat.m10, mat.m11,
                0, 0, 0, 1);
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
            // Dunno why _vrSystem.GetProjectionMatrix doesn't work. Maybe different semantics of the matrix?
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

        // instead of Model * View * Projection the model is Model * View * Eye * Projection
        private Matrix GetEyeView(EVREye eye) => Matrix.Invert(Matrix.Transpose(ToMonogameMatrix(_vrSystem.GetEyeToHeadTransform(eye))));

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

            // I have absolutely no idea why inverse+transpose is here.
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
