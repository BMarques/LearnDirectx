using System.Numerics;

namespace BasicGameLoop
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
#if !DEBUG
            if (!Vector.IsHardwareAccelerated)
                return;
#endif

            using var game = new Game("Direct3D 11 Game");
            Application.Run(new GameForm(game));
        }
    }
}