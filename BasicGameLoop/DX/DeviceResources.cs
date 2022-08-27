using SharpGen.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Direct3D11.Debug;
using Vortice.DXGI;
using Vortice.DXGI.Debug;
using Vortice.Mathematics;

namespace DX
{
    /// <summary>
    /// Controls all the DirectX device resources.
    /// </summary>
    class DeviceResources
    {
        // Direct3D objects.
        private IDXGIFactory2 _dxgiFactory;
        private ID3D11Device1 _d3dDevice;
        private ID3D11DeviceContext1 _d3dContext;
        private IDXGISwapChain1 _swapChain;
        private ID3DUserDefinedAnnotation _d3dAnnotation;

        // Direct3D rendering objects. Required for 3D.
        private ID3D11Texture2D _renderTarget;
        private ID3D11Texture2D _depthStencil;
        private ID3D11RenderTargetView _d3dRenderTargetView;
        private ID3D11DepthStencilView _d3dDepthStencilView;
        private Viewport _screenViewport;

        // Direct3D properties.
        private Format _backBufferFormat;
        private Format _depthBufferFormat;
        private int _backBufferCount;
        private FeatureLevel _d3dMinFeatureLevel;

        // Cached device properties.
        private IntPtr _window;
        private FeatureLevel _d3dFeatureLevel;
        private Rect _outputSize;
        private Rectangle _bounds;

        // HDR Support
        private ColorSpaceType _colorSpace;

        // DeviceResources options (see flags above)
        private DeviceResourcesOptions _options;

        // The IDeviceNotify can be held directly as it owns the DeviceResources.
        private IDeviceNotify _deviceNotify;

        public DeviceResources(Format backBufferFormat = Format.B8G8R8A8_UNorm,
                               Format depthBufferFormat = Format.D32_Float,
                               int backBufferCount = 2,
                               FeatureLevel minFeatureLevel = FeatureLevel.Level_10_0,
                               DeviceResourcesOptions flags = DeviceResourcesOptions.FlipPresent)
        {
            _backBufferFormat = backBufferFormat;
            _depthBufferFormat = depthBufferFormat;
            _backBufferCount = backBufferCount;
            _d3dMinFeatureLevel = minFeatureLevel;
            _d3dFeatureLevel = FeatureLevel.Level_9_1;
            _outputSize = new Rect(0, 0, 1, 1);
            _colorSpace = ColorSpaceType.RgbFullG22NoneP709;
            _options = flags | DeviceResourcesOptions.FlipPresent;            
        }

        // Device Accessors.
        public Rect OutputSize => _outputSize;

        // Direct3D Accessors.
        public ID3D11Device1 D3DDevice => _d3dDevice;
        public ID3D11DeviceContext1 D3DDeviceContext => _d3dContext;
        public IDXGISwapChain1 SwapChain => _swapChain;
        public IDXGIFactory2 DXGIFactory => _dxgiFactory;
        public IntPtr Window => _window;
        public FeatureLevel DeviceFeatureLevel => _d3dFeatureLevel;
        public ID3D11Texture2D RenderTarget => _renderTarget;
        public ID3D11Texture2D DepthStencil => _depthStencil;
        public ID3D11RenderTargetView RenderTargetView => _d3dRenderTargetView;
        public ID3D11DepthStencilView DepthStencilView => _d3dDepthStencilView;
        public Format BackBufferFormat => _backBufferFormat;
        public Format DepthBufferFormat => _depthBufferFormat;
        public Viewport ScreenViewport => _screenViewport;
        public int BackBufferCount => _backBufferCount;
        public ColorSpaceType ColorSpace => _colorSpace;
        public DeviceResourcesOptions DeviceOptions => _options;

        /// <summary>
        /// Configures the Direct3D device, and stores handles to it and the device context.
        /// </summary>
        public void CreateDeviceResources()
        {
            var creationFlags = DeviceCreationFlags.BgraSupport;

#if DEBUG
            if (D3D11.SdkLayersAvailable())
            {
                // If the project is in a debug build, enable debugging via SDK Layers with this flag.
                creationFlags |= DeviceCreationFlags.Debug;
            }
            else
            {
                Debug.WriteLine("WARNING: Direct3D Debug Device is not available\n");
            }
#endif

            CreateFactory();

            // Determines whether tearing support is available for fullscreen borderless windows.
            if (_options.HasFlag(DeviceResourcesOptions.AllowTearing))
            {
                var factory5 = _dxgiFactory.QueryInterface<IDXGIFactory5>();
                var allowTearing = factory5.PresentAllowTearing;
                if (!allowTearing)
                {
                    _options &= ~DeviceResourcesOptions.AllowTearing;
                    Debug.WriteLine("WARNING: Variable refresh rate displays not support");
                }
            }

            // Disable HDR if we are on an OS that can't support FLIP swap effects
            if (_options.HasFlag(DeviceResourcesOptions.EnableHDR))
            {
                var factory5 = _dxgiFactory.QueryInterfaceOrNull<IDXGIFactory5>();
                if (factory5 == null)
                {
                    _options &= ~DeviceResourcesOptions.EnableHDR;
                    Debug.WriteLine("WARNING: HDR swap chains not supported");
                }
            }

            // Disable FLIP if not on a supporting OS
            if (_options.HasFlag(DeviceResourcesOptions.FlipPresent))
            {
                IDXGIFactory4 factory4 = _dxgiFactory.QueryInterfaceOrNull<IDXGIFactory4>();
                if (factory4 == null)
                {
                    _options &= ~DeviceResourcesOptions.FlipPresent;
                    Debug.WriteLine("INFO: Flip swap effects not supported");
                }
            }

            // Determine DirectX hardware feature levels this app will support.
            var featureLevels = new FeatureLevel[]
            {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0,
                FeatureLevel.Level_9_3,
                FeatureLevel.Level_9_2,
                FeatureLevel.Level_9_1
            };

            int featLevelCount = featureLevels.Count(f => f >= _d3dMinFeatureLevel);

            if (featLevelCount == 0)
            {
                throw new ArgumentOutOfRangeException("minFeatureLevel too high");
            }

            GetHardwareAdapter(out IDXGIAdapter1 adapter);

            // Create the Direct3D 11 API device object and a corresponding context.
            ID3D11Device device = null;
            ID3D11DeviceContext context = null;

            var result = Result.Fail;
            if (adapter != null)
            {
                result = D3D11.D3D11CreateDevice(
                    adapter,
                    DriverType.Unknown,
                    creationFlags,
                    featureLevels,
                    out device,             // Returns the Direct3D device created.
                    out _d3dFeatureLevel,   // Returns feature level of device created.
                    out context             // Returns the device immediate context.
                    );
            }
#if !DEBUG
            else
            {
                throw new Exception("No Direct3D hardware device found");
            }
#else
            if (result.Failure)
            {
                // If the initialization fails, fall back to the WARP device.
                // For more information on WARP, see:
                // http://go.microsoft.com/fwlink/?LinkId=286690
                result = D3D11.D3D11CreateDevice(
                    null,
                    DriverType.Warp, // Create a WARP device instead of a hardware device.
                    creationFlags,
                    featureLevels,
                    out device,
                    out _d3dFeatureLevel,
                    out context
                    );

                if (result.Success)
                {
                    Debug.WriteLine("Direct3D Adapter - WARP");
                }
            }
#endif
            result.CheckError();

#if DEBUG
            var d3dDebug = device.QueryInterface<ID3D11Debug>();
            var d3dInfoQueue = d3dDebug.QueryInterface<ID3D11InfoQueue>();

            d3dInfoQueue.SetBreakOnSeverity(MessageSeverity.Corruption, true);
            d3dInfoQueue.SetBreakOnSeverity(MessageSeverity.Error, true);

            var filter = new Vortice.Direct3D11.Debug.InfoQueueFilter
            {
                DenyList = new Vortice.Direct3D11.Debug.InfoQueueFilterDescription
                {
                    Ids = new MessageId[] { MessageId.SetPrivateDataChangingParams }
                }
            };
            d3dInfoQueue.AddStorageFilterEntries(filter);
#endif

            _d3dDevice = device.QueryInterface<ID3D11Device1>();
            _d3dContext = context.QueryInterface<ID3D11DeviceContext1>();
            _d3dAnnotation = context.QueryInterface<ID3DUserDefinedAnnotation>();
        }

        /// <summary>
        /// These resources need to be recreated every time the window size is changed.
        /// </summary>
        public void CreateWindowSizeDependentResources()
        {
            if (_window == IntPtr.Zero)
            {
                throw new ArgumentException("Call SetWindow with a valid win32 window handle");
            }

            // Clear the previous window size specific context.
            _d3dContext.OMSetRenderTargets(0, null, null);
            _d3dRenderTargetView?.Dispose();
            _d3dDepthStencilView?.Dispose();
            _renderTarget?.Dispose();
            _depthStencil?.Dispose();
            _d3dContext?.Flush();

            // Determine the render target size in pixels
            var backBufferWidth = Math.Max((int)(_outputSize.Right - _outputSize.Left), 1);
            var backBufferHeight = Math.Max((int)(_outputSize.Bottom - _outputSize.Top), 1);
            var backBufferFormat = _options.HasFlag(DeviceResourcesOptions.FlipPresent) || _options.HasFlag(DeviceResourcesOptions.AllowTearing) || _options.HasFlag(DeviceResourcesOptions.EnableHDR) ? NoSRGB(_backBufferFormat) : _backBufferFormat;

            if (_swapChain != null)
            {
                // If the swap chain already exists, resize it.
                var result = _swapChain.ResizeBuffers(
                    _backBufferCount,
                    backBufferWidth,
                    backBufferHeight,
                    backBufferFormat,
                    _options.HasFlag(DeviceResourcesOptions.AllowTearing) ? SwapChainFlags.AllowTearing : SwapChainFlags.None);

                if (result.Code == Vortice.DXGI.ResultCode.DeviceRemoved || result == Vortice.DXGI.ResultCode.DeviceReset)
                {
#if DEBUG
                    var code = result.Code == (int)Vortice.DXGI.ResultCode.DeviceRemoved ? _d3dDevice.DeviceRemovedReason.Code : result.Code;
                    string message = $"Device Lost on ResizeBuffers: Reason code 0x{code:X8}";
                    Debug.WriteLine(message);
#endif

                    // If the device was removed for any reason, a new device and swap chain will need to be created.
                    HandleDeviceLost();

                    // Everything is set up now. Do not continue execution of this method. HandleDeviceLost will reenter this method
                    // and correctly set up the new device.
                    return;
                }
                else
                {
                    result.CheckError();
                }
            }
            else
            {
                // Create a descriptor for the swap chain.
                var swapChainDesc = new SwapChainDescription1
                {
                    Width = backBufferWidth,
                    Height = backBufferHeight,
                    Format = backBufferFormat,
                    BufferUsage = Usage.RenderTargetOutput,
                    BufferCount = _backBufferCount,
                    SampleDescription = new SampleDescription
                    {
                        Count = 1,
                        Quality = 0
                    },
                    Scaling = Scaling.Stretch,
                    SwapEffect = _options.HasFlag(DeviceResourcesOptions.FlipPresent) || _options.HasFlag(DeviceResourcesOptions.AllowTearing) || _options.HasFlag(DeviceResourcesOptions.EnableHDR) ? SwapEffect.FlipDiscard : SwapEffect.Discard,
                    AlphaMode = AlphaMode.Ignore,
                    Flags = _options.HasFlag(DeviceResourcesOptions.AllowTearing) ? SwapChainFlags.AllowTearing : SwapChainFlags.None
                };

                var fsSwapChainDesc = new SwapChainFullscreenDescription
                {
                    Windowed = true
                };

                // Create a SwapChain from a Win32 window
                _swapChain = _dxgiFactory.CreateSwapChainForHwnd(
                    _d3dDevice,
                    _window,
                    swapChainDesc,
                    fsSwapChainDesc);

                // This class does not support exclusive full-screen mode and prevents DXGI from responding to the ALT+Enter shortcut
                _dxgiFactory.MakeWindowAssociation(_window, WindowAssociationFlags.IgnoreAltEnter);
            }

            // Handle color space settings for HDR
            UpdateColorSpace();

            // Create a render target view of the swap chain back buffer.
            _swapChain.GetBuffer(0, out _renderTarget).CheckError();

            RenderTargetViewDescription renderTargetViewDescription = new(RenderTargetViewDimension.Texture2D, _backBufferFormat);
            _d3dRenderTargetView = _d3dDevice.CreateRenderTargetView(_renderTarget, renderTargetViewDescription);

            if (_depthBufferFormat != Format.Unknown)
            {
                // Create a depth stencil view for use with 3D rendering if needed
                Texture2DDescription depthStencilDesc = new(
                    _depthBufferFormat,
                    backBufferWidth,
                    backBufferHeight,
                    1,  // This depth stencil view has only one texture.
                    1,  // Use a single mipmap level.
                    BindFlags.DepthStencil
                    );

                _depthStencil = _d3dDevice.CreateTexture2D(depthStencilDesc);

                _d3dDepthStencilView = _d3dDevice.CreateDepthStencilView(_depthStencil);
            }

            // Set the 3D rendering viewport to target the entire window.
            _screenViewport = new Viewport(0f, 0f, backBufferWidth, backBufferHeight, 0f, 1f);
        }

        /// <summary>
        /// This method is called when the Win32 window is created (or re-created).
        /// </summary>
        public void SetWindow(IntPtr window, int width, int height, Rectangle bounds)
        {
            _window = window;

            _outputSize.Width = width;
            _outputSize.Height = height;

            _bounds = bounds;
        }

        // This method is called when the Win32 window changes size
        public bool WindowSizeChanged(float width, float height)
        {
            Rect newRc = new(width, height);
            if (_outputSize.Width == width && _outputSize.Height == height)
            {
                // Handle color space settings for HDR
                UpdateColorSpace();

                return false;
            }

            _outputSize = newRc;
            CreateWindowSizeDependentResources();
            return true;
        }

        /// <summary>
        /// Recreate all device resources and set them back to the current state.
        /// </summary>
        public void HandleDeviceLost()
        {
            if (_deviceNotify != null)
            {
                _deviceNotify.OnDeviceLost();
            }

            _d3dDepthStencilView.Dispose();
            _d3dRenderTargetView.Dispose();
            _renderTarget.Dispose();
            _depthStencil.Dispose();
            _swapChain.Dispose();
            _d3dContext.Dispose();
            _d3dAnnotation.Dispose();

#if DEBUG
            var d3dDebug = _d3dDevice.QueryInterface<ID3D11Debug>();
            d3dDebug.ReportLiveDeviceObjects(ReportLiveDeviceObjectFlags.Summary);
#endif

            _d3dDevice.Dispose();
            _dxgiFactory.Dispose();

            CreateDeviceResources();
            CreateWindowSizeDependentResources();

            _deviceNotify?.OnDeviceRestored();
        }

        public void RegisterDeviceNotify(IDeviceNotify deviceNotify) => _deviceNotify = deviceNotify;

        /// <summary>
        /// Present the contents of the swap chain to the screen.
        /// </summary>
        public void Present()
        {
            Result result;
            if (_options.HasFlag(DeviceResourcesOptions.AllowTearing))
            {
                // Recommended to always use tearing if supported when using a sync interval of 0.
                result = _swapChain.Present(0, PresentFlags.AllowTearing);
            }
            else
            {
                // The first argument instructs DXGI to block until VSync, putting the application
                // to sleep until the next VSync. This ensures we don't waste any cycles rendering
                // frames that will never be displayed to the screen.
                result = _swapChain.Present(1, 0);
            }

            // Discard the contents of the render target.
            // This is a valid operation only when the existing contents will be entirely
            // overwritten. If dirty or scroll rects are used, this call should be removed.
            _d3dContext.DiscardView(_d3dRenderTargetView);

            if (_d3dDepthStencilView != null)
            {
                // Discard the contents of the depth stencil.
                _d3dContext.DiscardView(_d3dDepthStencilView);
            }

            // If the device was removed either by a disconnection or a driver update, we
            // must recreate all the device resources.
            if (result.Code == Vortice.DXGI.ResultCode.DeviceRemoved || result.Code == Vortice.DXGI.ResultCode.DeviceReset)
            {
#if DEBUG
                var code = result.Code == Vortice.DXGI.ResultCode.DeviceRemoved ? _d3dDevice.DeviceRemovedReason.Code : result.Code;
                string message = $"Device Lost on {nameof(Present)}: Reason code 0x{code:X8}";
                Debug.WriteLine(message);
#endif
                HandleDeviceLost();
            }
            else
            {
                result.CheckError();

                if (!_dxgiFactory.IsCurrent)
                {
                    UpdateColorSpace();
                }
            }
        }

        public void UpdateColorSpace()
        {
            if (_dxgiFactory == null)
                return;

            if (!_dxgiFactory.IsCurrent)
            {
                // Output information is cached on the DXGI Factory. If it is stale we need to create a new factory.
                CreateFactory();
            }

            ColorSpaceType colorSpace = ColorSpaceType.RgbFullG22NoneP709;

            bool isDisplayHDR10 = false;

            if (_swapChain != null)
            {
                // To detect HDR support, we will need to check the color space in the primary
                // DXGI output associated with the app at this point in time
                // (using window/display intersection).

                var ax1 = _bounds.Left;
                var ay1 = _bounds.Top;
                var ax2 = _bounds.Right;
                var ay2 = _bounds.Bottom;

                // Get the rectangle bounds of the app window.
                IDXGIOutput bestOutput = null;
                int bestIntersectArea = -1;

                for (var adapterIndex = 0;
                    _dxgiFactory.EnumAdapters(adapterIndex, out IDXGIAdapter adapter).Success;
                    ++adapterIndex)
                {
                    for (var outputIndex = 0;
                         adapter.EnumOutputs(outputIndex, out IDXGIOutput output).Success;
                         ++outputIndex)
                    {
                        // Get the rectangle bounds of current output.
                        OutputDescription desc = output.Description;
                        var r = desc.DesktopCoordinates;

                        // Compute the intersection
                        int intersectArea = ComputeIntersectionArea(ax1, ay1, ax2, ay2, r.Left, r.Top, r.Right, r.Bottom);
                        if (intersectArea > bestIntersectArea)
                        {
                            bestOutput = output;
                            bestIntersectArea = intersectArea;
                        }
                    }
                }

                if (bestOutput != null)
                {
                    IDXGIOutput6 output6 = bestOutput.QueryInterfaceOrNull<IDXGIOutput6>();
                    if (output6 != null)
                    {
                        if (output6.Description1.ColorSpace == ColorSpaceType.RgbFullG2084NoneP2020)
                        {
                            // Display output is HDR10
                            isDisplayHDR10 = true;
                        }
                    }
                }
            }

            if (_options.HasFlag(DeviceResourcesOptions.EnableHDR) && isDisplayHDR10)
            {
                switch (_backBufferFormat)
                {
                    case Format.R10G10B10A2_UNorm:
                        // The application creates the HDR10 signal.
                        colorSpace = ColorSpaceType.RgbFullG2084NoneP2020;
                        break;
                    case Format.R16G16B16A16_Float:
                        // The system creates the HDR10 signal; application uses linear values.
                        colorSpace = ColorSpaceType.RgbFullG10NoneP709;
                        break;
                    default:
                        break;
                }
            }

            _colorSpace = colorSpace;

            IDXGISwapChain3 swapChain3 = _swapChain?.QueryInterfaceOrNull<IDXGISwapChain3>();
            if (swapChain3 != null)
            {
                var colorSpaceSupport = swapChain3.CheckColorSpaceSupport(colorSpace);
                if (colorSpaceSupport.HasFlag(SwapChainColorSpaceSupportFlags.Present))
                {
                    swapChain3.SetColorSpace1(colorSpace);
                }
            }
        }

        // Performance events
        public void PIXBeginEvent(string name) => _d3dAnnotation.BeginEvent(name);
        public void PIXEndEvent() => _d3dAnnotation.EndEvent();
        public void PIXSetMarker(string name) => _d3dAnnotation.SetMarker(name);

        private void CreateFactory()
        {
#if DEBUG
            bool debugDXGI = false;
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 2)) // >= Windows 8.1
            {
                DXGI.DXGIGetDebugInterface1(out IDXGIInfoQueue dxgiInfoQueue);
                if (dxgiInfoQueue != null)
                {
                    debugDXGI = true;

                    DXGI.CreateDXGIFactory2(true, out _dxgiFactory);

                    dxgiInfoQueue.SetBreakOnSeverity(DXGI.DebugAll, InfoQueueMessageSeverity.Error, true);
                    dxgiInfoQueue.SetBreakOnSeverity(DXGI.DebugAll, InfoQueueMessageSeverity.Corruption, true);

                    var filter = new Vortice.DXGI.Debug.InfoQueueFilter
                    {
                        DenyList = new Vortice.DXGI.Debug.InfoQueueFilterDescription
                        {
                            Ids = new[] { 80 } // IDXGIInfoQueue dxgiInfoQueue = 
                        }
                    };
                    dxgiInfoQueue.AddStorageFilterEntries(DXGI.DebugDxgi, filter);
                }
            }
            if (!debugDXGI)
#endif
                DXGI.CreateDXGIFactory1(out _dxgiFactory);
        }

        // This methods acquires the first available hardware adapter.
        // If no such adapter can be found, adapter will be set to null
        private void GetHardwareAdapter(out IDXGIAdapter1 adapter)
        {
            adapter = null;
            IDXGIFactory6 factory6 = _dxgiFactory.QueryInterfaceOrNull<IDXGIFactory6>();
            if (factory6 != null)
            {
                for (var adapterIndex = 0;
                     factory6.EnumAdapterByGpuPreference(
                         adapterIndex,
                         GpuPreference.HighPerformance,
                         out adapter).Success;
                     adapterIndex++)
                {
                    AdapterDescription1 desc = adapter.Description1;
                    if (desc.Flags.HasFlag(AdapterFlags.Software))
                    {
                        // Don't select the Basic Render Driver adapter.
                        continue;
                    }

                    Debug.WriteLine($"Direct3D Adapter ({adapterIndex}): VID:{desc.VendorId:X4}, PID:{desc.DeviceId:X4} - {desc.Description}");

                    break;
                }
            }

            if (adapter == null)
            {
                for (var adapterIndex = 0; _dxgiFactory.EnumAdapters1(0, out adapter).Success; adapterIndex++)
                {
                    AdapterDescription1 desc = adapter.Description1;

                    if (desc.Flags.HasFlag(AdapterFlags.Software))
                    {
                        // Don't select the Basic Render Driver Adapter.
                        continue;
                    }

                    Debug.WriteLine($"Direct3D Adapter {adapterIndex}: VID:{desc.VendorId:X4}, PID:{desc.DeviceId:X4} - {desc.Description}");

                    break;
                }
            }
        }

        private Format NoSRGB(Format fmt)
        {
            switch (fmt)
            {
                case Format.R8G8B8A8_UNorm_SRgb: return Format.R8G8B8A8_UNorm;
                case Format.B8G8R8A8_UNorm_SRgb: return Format.B8G8R8A8_UNorm;
                case Format.B8G8R8X8_UNorm_SRgb: return Format.B8G8R8X8_UNorm;
                default: return fmt;
            }
        }

        private static int ComputeIntersectionArea(int ax1, int ay1, int ax2, int ay2,
                                            int bx1, int by1, int bx2, int by2)
        {
            return Math.Max(01, Math.Min(ax2, bx2) - Math.Max(ax1, bx1)) * Math.Max(01, Math.Min(ay2, by2) - Math.Max(ay1, by1));
        }
    }
}
