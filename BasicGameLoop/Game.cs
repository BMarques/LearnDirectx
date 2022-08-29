using DX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D11;

namespace BasicGameLoop
{
    class Game : IDeviceNotify, IDisposable
    {
        private readonly DeviceResources _deviceResources;
        private readonly StepTimer _timer;

        private bool _initialized;

        public Game(string title)
        {
            _deviceResources = new DeviceResources();
            // TODO: Provide parameters for swapchain format, depth/stencil format, and backbuffer count.
            //   Add DeviceResourcesOptions.AllowTearing to opt-in to variable rate displays.
            //   Add DeviceResourcesOptions.EnableHDR for HDR10 display.
            _deviceResources.RegisterDeviceNotify(this);

            _timer = new StepTimer
            {
                Update = Update
            };

            Title = title;
        }

        public string Title { get; private set; }
        public Size DefaultSize => new(800, 600);
        public Action ExitGame { get; set; }

        // Initialize the Direct3D resources required to run
        public void Initialize(IntPtr window, int width, int height, Rectangle bounds)
        {   
            _deviceResources.SetWindow(window, width, height, bounds);

            _deviceResources.CreateDeviceResources();
            CreateDeviceDependentResources();

            _deviceResources.CreateWindowSizeDependentResources();
            CreateWindowSizeDependentResources();

            // TODO: Change the timer settings if you want something other than the default variable timestep mode.
            // e.g. for 60 FPS fixed timestep update logic, call:
            /*
            _timer.SetFixedTimeStep(true);
            _timer.SetTargetElapsedSeconds(1.0 / 60);
            */

            _initialized = true;
        }

        // Executes the basic game loop
        public void Tick()
        {
            _timer.Tick();

            Render();
        }

        #region Message Handlers

        public void OnActivated()
        {
            // TODO: Game is becoming active window.
        }

        public void OnDeactivated()
        {
            // TODO: Game is becoming background window.
        }

        public void OnSuspending()
        {
            // TODO: Game is being power-suspended (or minimized).
        }

        public void OnResuming()
        {
            _timer.ResetElapsedTime();

            // TODO: Game is being power-resumed (or returning from minimize).
        }

        public void OnWindowMoved()
        {
            var r = _deviceResources.OutputSize;
            _deviceResources.WindowSizeChanged(r.Right, r.Bottom);
        }

        public void OnDisplayChange()
        {
            _deviceResources.UpdateColorSpace();
        }

        public void OnWindowSizeChanged(int width, int height)
        {
            if (!_initialized || !_deviceResources.WindowSizeChanged(width, height))
                return;

            CreateWindowSizeDependentResources();

            // TODO: Game window is being resized.

        }
        #endregion

        // Updates the world
        private void Update(StepTimer timer)
        {
            float elapsedTime = (float)_timer.ElapsedSeconds;

            // TODO: Add your game logic here.
        }

        // Draws the scene
        private void Render()
        {
            // Don't try to render anything before the first Update.
            if (_timer.FrameCount == 0)
            {
                return;
            }

            Clear();

            _deviceResources.PIXBeginEvent(nameof(Render));
            var context = _deviceResources.D3DDeviceContext;

            // TODO: Add your rendering code here.


            _deviceResources.PIXEndEvent();

            // Show the new frame.
            _deviceResources.Present();
        }

        // Helper method to clear the back buffers
        private void Clear()
        {
            _deviceResources.PIXBeginEvent(nameof(Clear));

            // Clear the views
            var context = _deviceResources.D3DDeviceContext;
            var renderTarget = _deviceResources.RenderTargetView;
            var depthStencil = _deviceResources.DepthStencilView;

            context.ClearRenderTargetView(renderTarget, FromSystemDrawingColor(Color.CornflowerBlue));
            context.ClearDepthStencilView(depthStencil, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
            context.OMSetRenderTargets(renderTarget, depthStencil);

            // Set the viewport
            var viewport = _deviceResources.ScreenViewport;
            context.RSSetViewport(viewport);

            _deviceResources.PIXEndEvent();
        }

        #region Direct3D Resources

        // These are the resources that depend on the device.
        private void CreateDeviceDependentResources()
        {
            var device = _deviceResources.D3DDevice;

            // TODO: Initialize device dependent objects here (independent of window size).
        }

        // Allocate all memory resources that change on a window SizeChanged event
        private void CreateWindowSizeDependentResources()
        {
            // TODO: Initialize windows-sized dependent objects here.
        }

        public void OnDeviceLost()
        {
            // TODO: Add Direct3D resource cleanup here.
        }

        public void OnDeviceRestored()
        {
            CreateDeviceDependentResources();

            CreateWindowSizeDependentResources();
        }

        #endregion

        private static Vortice.Mathematics.Color FromSystemDrawingColor(Color color) => new(color.R, color.G, color.B, color.A);


        #region IDisposable

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _deviceResources.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Game()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
