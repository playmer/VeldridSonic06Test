using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;
using System.Numerics;
using Veldrid;
using Veldrid.ImageSharp;
using Veldrid.Sdl2;
using Veldrid.SPIRV;
using Veldrid.StartupUtilities;
using Veldrid.Utilities;
using System.Diagnostics;
using ImGuiNET;

namespace VeldridTest
{

    class Renderer
    {
        Stopwatch mClock = new Stopwatch();
        double mPreviousElapsed;

        private Sdl2Window mWindow = null;
        private GraphicsDevice mGraphicsDevice;
        private CommandList mCommandList;
        private DeviceBuffer mVertexBuffer;
        private DeviceBuffer mIndexBuffer;
        private Swapchain mSwapchain;
        private Shader[] mShaders;
        private Pipeline mPipeline;
        private ResourceFactory mFactory;
        public ImGuiRenderer mImguiRenderer;
        bool mWindowResized = true;

        // Camera and Display
        DeviceBuffer mProjectionBuffer;
        DeviceBuffer mViewBuffer;
        DeviceBuffer mWorldBuffer;

        // Model Data
        VertexPositionTexture[] mVertices;
        ushort[] mIndices;
        Texture mSurfaceTexture;
        TextureView mSurfaceTextureView;

        private ResourceSet mProjViewSet;
        private ResourceSet mWorldTextureSet;

        // Movement Data:
        Vector3 mTranslation = new Vector3(0,0,0);
        Vector3 mScale = new Vector3(1, 1, 1);
        Vector3 mRotation = new Vector3(0, 0, 0);

        private const string VertexCode = @"
#version 450
layout(set = 0, binding = 0) uniform ProjectionBuffer
{
    mat4 Projection;
};
layout(set = 0, binding = 1) uniform ViewBuffer
{
    mat4 View;
};
layout(set = 1, binding = 0) uniform WorldBuffer
{
    mat4 World;
};
layout(location = 0) in vec3 Position;
layout(location = 1) in vec2 TexCoords;
layout(location = 0) out vec2 fsin_texCoords;
void main()
{
    vec4 worldPosition = World * vec4(Position, 1);
    vec4 viewPosition = View * worldPosition;
    vec4 clipPosition = Projection * viewPosition;
    gl_Position = clipPosition;
    fsin_texCoords = TexCoords;
}";

        private const string FragmentCode = @"
#version 450
layout(location = 0) in vec2 fsin_texCoords;
layout(location = 0) out vec4 fsout_color;
layout(set = 1, binding = 1) uniform texture2D SurfaceTexture;
layout(set = 1, binding = 2) uniform sampler SurfaceSampler;
void main()
{
    fsout_color =  texture(sampler2D(SurfaceTexture, SurfaceSampler), fsin_texCoords);
}";

        public Renderer(Sdl2Window aWindow)
        {
            mClock.Start();
            mPreviousElapsed = mClock.Elapsed.TotalSeconds;
            

            mWindow = aWindow;
            mWindow.Resized += () =>
            {
                mWindowResized = true;
            };

            GraphicsDeviceOptions options = new GraphicsDeviceOptions(
                debug: false,
                swapchainDepthFormat: PixelFormat.R16_UNorm,
                syncToVerticalBlank: true,
                resourceBindingModel: ResourceBindingModel.Improved,
                preferDepthRangeZeroToOne: true,
                preferStandardClipSpaceYDirection: true);

            mGraphicsDevice = VeldridStartup.CreateGraphicsDevice(aWindow, options);
            mSwapchain = mGraphicsDevice.MainSwapchain;
            mFactory = mGraphicsDevice.ResourceFactory;


            mImguiRenderer = new ImGuiRenderer(mGraphicsDevice, mSwapchain.Framebuffer.OutputDescription, aWindow.Width, aWindow.Height);
        }

        public void ChooseMesh()
        {
            // CPU Resources
            
            // Texture
            ImageSharpTexture inputImage = new ImageSharpTexture("JoshIconC.png", false);

            // Vertices
            mVertices = GetCubeVertices();

            // Indices
            mIndices = GetCubeIndices();

            // GPU Resources
            
            // Texture
            mSurfaceTexture = inputImage.CreateDeviceTexture(mGraphicsDevice, mFactory);
            mSurfaceTextureView = mFactory.CreateTextureView(mSurfaceTexture);

            // VertexBuffer
            mVertexBuffer = mFactory.CreateBuffer(new BufferDescription((uint)(VertexPositionTexture.SizeInBytes * mVertices.Length), BufferUsage.VertexBuffer));
            mGraphicsDevice.UpdateBuffer(mVertexBuffer, 0, mVertices);

            // IndexBuffer
            mIndexBuffer = mFactory.CreateBuffer(new BufferDescription(sizeof(ushort) * (uint)mIndices.Length, BufferUsage.IndexBuffer));
            mGraphicsDevice.UpdateBuffer(mIndexBuffer, 0, mIndices);
        }

        private static VertexPositionTexture[] GetCubeVertices()
        {
            VertexPositionTexture[] vertices = new VertexPositionTexture[]
            {
                // Top
                new VertexPositionTexture(new Vector3(-0.5f, +0.5f, -0.5f), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(+0.5f, +0.5f, -0.5f), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(+0.5f, +0.5f, +0.5f), new Vector2(1, 1)),
                new VertexPositionTexture(new Vector3(-0.5f, +0.5f, +0.5f), new Vector2(0, 1)),
                // Bottom                                                             
                new VertexPositionTexture(new Vector3(-0.5f,-0.5f, +0.5f),  new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(+0.5f,-0.5f, +0.5f),  new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(+0.5f,-0.5f, -0.5f),  new Vector2(1, 1)),
                new VertexPositionTexture(new Vector3(-0.5f,-0.5f, -0.5f),  new Vector2(0, 1)),
                // Left                                                               
                new VertexPositionTexture(new Vector3(-0.5f, +0.5f, -0.5f), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(-0.5f, +0.5f, +0.5f), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(-0.5f, -0.5f, +0.5f), new Vector2(1, 1)),
                new VertexPositionTexture(new Vector3(-0.5f, -0.5f, -0.5f), new Vector2(0, 1)),
                // Right                                                              
                new VertexPositionTexture(new Vector3(+0.5f, +0.5f, +0.5f), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(+0.5f, +0.5f, -0.5f), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(+0.5f, -0.5f, -0.5f), new Vector2(1, 1)),
                new VertexPositionTexture(new Vector3(+0.5f, -0.5f, +0.5f), new Vector2(0, 1)),
                // Back                                                               
                new VertexPositionTexture(new Vector3(+0.5f, +0.5f, -0.5f), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(-0.5f, +0.5f, -0.5f), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(-0.5f, -0.5f, -0.5f), new Vector2(1, 1)),
                new VertexPositionTexture(new Vector3(+0.5f, -0.5f, -0.5f), new Vector2(0, 1)),
                // Front                                                              
                new VertexPositionTexture(new Vector3(-0.5f, +0.5f, +0.5f), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(+0.5f, +0.5f, +0.5f), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(+0.5f, -0.5f, +0.5f), new Vector2(1, 1)),
                new VertexPositionTexture(new Vector3(-0.5f, -0.5f, +0.5f), new Vector2(0, 1)),
            };

            return vertices;
        }

        private static ushort[] GetCubeIndices()
        {
            ushort[] indices =
            {
                0,1,2, 0,2,3,
                4,5,6, 4,6,7,
                8,9,10, 8,10,11,
                12,13,14, 12,14,15,
                16,17,18, 16,18,19,
                20,21,22, 20,22,23,
            };

            return indices;
        }

        public void Transform()
        {
            if (ImGui.Begin("Transformation"))
            {
                ImGui.DragFloat3("Translation", ref mTranslation, .1f);
                ImGui.DragFloat3("Scale", ref mScale, .1f);
                ImGui.DragFloat3("Rotation", ref mRotation, .1f);

                ImGui.End();
            }
        }

        public void CreateResources()
        {
            mProjectionBuffer = mFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            mViewBuffer = mFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            mWorldBuffer = mFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));

            ChooseMesh();

            ShaderSetDescription shaderSet = new ShaderSetDescription(
                new[]
                {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                        new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
                },
                mFactory.CreateFromSpirv(
                    new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexCode), "main"),
                    new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentCode), "main")));

            ResourceLayout projViewLayout = mFactory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("ProjectionBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("ViewBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceLayout worldTextureLayout = mFactory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("WorldBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("SurfaceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("SurfaceSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            mPipeline = mFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                RasterizerStateDescription.Default,
                PrimitiveTopology.TriangleList,
                shaderSet,
                new[] { projViewLayout, worldTextureLayout },
                mSwapchain.Framebuffer.OutputDescription));

            mProjViewSet = mFactory.CreateResourceSet(new ResourceSetDescription(
                projViewLayout,
                mProjectionBuffer,
                mViewBuffer));

            mWorldTextureSet = mFactory.CreateResourceSet(new ResourceSetDescription(
                worldTextureLayout,
                mWorldBuffer,
                mSurfaceTextureView,
                mGraphicsDevice.Aniso4xSampler));

            mCommandList = mFactory.CreateCommandList();
        }

        public void Draw()
        {
            double newElapsed = mClock.Elapsed.TotalSeconds;
            float deltaSeconds = (float)(newElapsed - mPreviousElapsed);

            mCommandList.Begin();

            mCommandList.UpdateBuffer(mProjectionBuffer, 0, Matrix4x4.CreatePerspectiveFieldOfView(
                1.0f,
                (float)mWindow.Width / mWindow.Height,
                0.5f,
                100f));

            mCommandList.UpdateBuffer(mViewBuffer, 0, Matrix4x4.CreateLookAt(Vector3.UnitZ * 2.5f, Vector3.Zero, Vector3.UnitY));

            Matrix4x4 rotation = Matrix4x4.CreateScale(mScale)
                * Matrix4x4.CreateFromAxisAngle(Vector3.UnitZ, mRotation.Z)
                * Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, mRotation.Y)
                * Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, mRotation.X)
                * Matrix4x4.CreateTranslation(mTranslation);

            mCommandList.UpdateBuffer(mWorldBuffer, 0, ref rotation);

            mCommandList.SetFramebuffer(mSwapchain.Framebuffer);
            mCommandList.ClearColorTarget(0, RgbaFloat.Black);
            mCommandList.ClearDepthStencil(1f);
            mCommandList.SetPipeline(mPipeline);
            mCommandList.SetVertexBuffer(0, mVertexBuffer);
            mCommandList.SetIndexBuffer(mIndexBuffer, IndexFormat.UInt16);
            mCommandList.SetGraphicsResourceSet(0, mProjViewSet);
            mCommandList.SetGraphicsResourceSet(1, mWorldTextureSet);
            mCommandList.DrawIndexed(36, 1, 0, 0, 0);

            mImguiRenderer.Render(mGraphicsDevice, mCommandList); // [3]
            mCommandList.End();
            mGraphicsDevice.SubmitCommands(mCommandList);
            mGraphicsDevice.SwapBuffers(mSwapchain);
            mGraphicsDevice.WaitForIdle();

            if (mWindowResized)
            {
                mWindowResized = false;
                mGraphicsDevice.ResizeMainWindow((uint)mWindow.Width, (uint)mWindow.Height);
                mImguiRenderer.WindowResized(mWindow.Width, mWindow.Height);
            }
        }

        public void DisposeResources()
        {
            mPipeline.Dispose();
            foreach (Shader shader in mShaders)
            {
                shader.Dispose();
            }
            mCommandList.Dispose();
            mVertexBuffer.Dispose();
            mIndexBuffer.Dispose();
            mGraphicsDevice.Dispose();
        }
    }

    public struct VertexPositionTexture
    {
        public const uint SizeInBytes = 20;

        public float PosX;
        public float PosY;
        public float PosZ;

        public float TexU;
        public float TexV;

        public VertexPositionTexture(Vector3 pos, Vector2 uv)
        {
            PosX = pos.X;
            PosY = pos.Y;
            PosZ = pos.Z;
            TexU = uv.X;
            TexV = uv.Y;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            //var sonic06File = new Sonic06XnoReader("C:/Users/playmer/AppData/Local/Hyper_Development_Team/Sonic '06 Toolkit/Archives/68236/0tiors2s.003/enemy/win32/enemy/eBomber/en_eBomber.xno");

            WindowCreateInfo windowCI = new WindowCreateInfo()
            {
                X = 100,
                Y = 100,
                WindowWidth = 960,
                WindowHeight = 540,
                WindowTitle = "Veldrid Tutorial"
            };
            Sdl2Window window = VeldridStartup.CreateWindow(ref windowCI);

            var renderer = new Renderer(window);
            renderer.CreateResources();

            while (window.Exists)
            {
                var snapShot = window.PumpEvents();
                renderer.mImguiRenderer.Update(1f / 60f, snapShot); // [2]

                if (window.Exists)
                {
                    renderer.Transform();
                    renderer.Draw();
                }
            }
        }
    }
}
