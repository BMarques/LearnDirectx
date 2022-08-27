using System.Numerics;

namespace BasicGameLoop;

static class Program
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

        Game game = new()
        {
            AppName = "Test"
        };
        Application.Run(new GameForm(game));
    }
}