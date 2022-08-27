using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace BasicGameLoop
{
    class GameForm : Form
    {
        private const int WM_DISPLAYCHANGE = 0x007E;
        private const int WM_MENUCHAR = 0x0120;
        private const int MNC_CLOSE = 1;

        private readonly Game _game;

        private bool _inSizeMove;
        private bool _inSuspend;
        private bool _minimized;
        private bool _fullscreen;

        public GameForm(Game game)
        {
            _game = game;

            Text = game.AppName;
            ResizeRedraw = true;
            Icon = new Icon("Icon1.ico");
            SetStyle(ControlStyles.Opaque, true);
            Size = _game.DefaultSize;
            MinimumSize = new(320, 200);

            // TODO: Uncomment the following lines for fullscreen
            //FormBorderStyle = FormBorderStyle.None;
            //WindowState = FormWindowState.Maximized;

            _game.Initialize(Handle, Width, Height, Bounds);

            Application.Idle += Application_Idle;

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_inSizeMove && _game != null)
            {
                _game.Tick();
            }
            else
            {
                base.OnPaint(e);
            }
        }

        protected override void OnMove(EventArgs e)
        {
            _game?.OnWindowMoved();

            base.OnMove(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                if (!_minimized)
                {
                    _minimized = true;
                    if (!_inSuspend)
                        _game?.OnSuspending();
                    _inSuspend = true;
                }
            }
            else if (_minimized)
            {
                _minimized = false;
                if (_inSuspend)
                    _game?.OnResuming();
                _inSuspend = false;
            }
            else if (!_inSizeMove)
            {
                _game?.OnWindowSizeChanged(Width, Height);
            }

            base.OnSizeChanged(e);
        }

        protected override void OnResizeBegin(EventArgs e)
        {
            _inSizeMove = true;

            base.OnResizeBegin(e);
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            _inSizeMove = false;
            _game?.OnWindowSizeChanged(Width, Height);

            base.OnResizeEnd(e);
        }

        protected override void OnActivated(EventArgs e)
        {
            _game?.OnActivated();

            base.OnActivated(e);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            _game?.OnDeactivated();

            base.OnDeactivate(e);
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    if (!_inSuspend)
                        _game?.OnSuspending();
                    _inSuspend = true;
                    break;
                case PowerModes.Resume:
                    if (!_minimized)
                    {
                        if (_inSuspend)
                            _game?.OnResuming();
                        _inSuspend = false;
                    }
                    break;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Alt && e.KeyCode == Keys.Return)
            {
                // Implements the class ALT+ENTER fullscreen toggle
                if (_fullscreen)
                {
                    FormBorderStyle = FormBorderStyle.Sizable;
                    WindowState = FormWindowState.Normal;
                }
                else
                {
                    WindowState = FormWindowState.Normal;
                    FormBorderStyle = FormBorderStyle.None;
                    WindowState = FormWindowState.Maximized;
                }
                _fullscreen = !_fullscreen;
            }

            base.OnKeyDown(e);
        }

        // Windows procedure
        protected override void WndProc(ref Message m)
        {
            // TOOD: Set fullscreen to true if defaulting to fullscreen

            switch (m.Msg)
            {
                case WM_DISPLAYCHANGE:
                    _game?.OnDisplayChange();
                    break;
                case WM_MENUCHAR:
                    // A menu is active and the user presses a key that does not correspond
                    // to any mnemonic or accelerator key. Ignore so we don't produce an error beep.
                    m.Result = new IntPtr(MNC_CLOSE << 16);
                    return;
            }
            base.WndProc(ref m);
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            while (PeekMessage(out _, IntPtr.Zero, 0, 0, 0) == 0)
            {
                _game.Tick();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeMessage
        {
            public IntPtr Handle;
            public uint Message;
            public IntPtr WParameter;
            public IntPtr LParameter;
            public uint Time;
            public Point Location;
        }

        [DllImport("user32.dll")]
        public static extern int PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);
    }
}
