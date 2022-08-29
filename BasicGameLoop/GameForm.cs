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
    internal class GameForm : Form
    {
        private const int WM_DISPLAYCHANGE = 0x007E;
        private const int WM_MENUCHAR = 0x0120;
        private const int WM_MOVE = 0x0003;
        private const int MNC_CLOSE = 1;

        private readonly Game _game;

        private bool _inSizeMove;
        private bool _inSuspend;
        private bool _minimized;
        private bool _fullscreen; // TODO: Set _fullscreen to true if defaulting to fullscreen.

        public GameForm(Game game)
        {
            _game = game;

            ResizeRedraw = true;
            Text = game.Title;
            Icon = new Icon("Icon1.ico");
            SetStyle(ControlStyles.Opaque, true);
            Size = _game.DefaultSize;
            MinimumSize = new(320, 200);

            // TODO: Uncomment the following lines for fullscreen
            //FormBorderStyle = FormBorderStyle.None;
            //WindowState = FormWindowState.Maximized;

            _game.Initialize(Handle, Width, Height, Bounds);

            Application.Idle += Application_Idle;
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

            // Exit helper
            _game.ExitGame = () => Application.Exit();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_inSizeMove)
            {
                _game?.Tick();
            }
            else
            {
                base.OnPaint(e);
            }
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
                case WM_MOVE:
                    _game?.OnWindowMoved();
                    break;

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

        private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    if (!_inSuspend)
                        _game?.OnSuspending();
                    _inSuspend = true;
                    return;
                case PowerModes.Resume:
                    if (!_minimized)
                    {
                        if (_inSuspend)
                            _game?.OnResuming();
                        _inSuspend = false;
                    }
                    return;
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
