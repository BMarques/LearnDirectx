using System.Diagnostics;

namespace DX
{
    /// <summary>
    /// Helper class for animation and simulation timing.
    /// </summary>
    public class StepTimer
    {
        // Represents time using 10,000,000 ticks per second.
        public const long TicksPerSecond = 10000000;

        // Source timing uses Stopwatch units.
        private readonly long _frequency;
        private long _lastTime;
        private readonly long _maxDelta;

        // Derived timing data uses a canonical tick format.
        private long _elapsedTicks;
        private long _totalTicks;
        private long _leftOverTicks;

        // Members for tracking the framerate.
        private int _frameCount;
        private int _framesPerSecond;
        private int _framesThisSecond;
        private long _secondCounter;

        // Members for configuring fixed timestep mode
        private bool _isFixedTimeStep;
        private long _targetElapsedTicks;

        public StepTimer()
        {
            _frequency = Stopwatch.Frequency;
            _lastTime = Stopwatch.GetTimestamp();

            // Initialize max delta to 1/10 of a second
            _maxDelta = _frequency / 10;

            _targetElapsedTicks = TicksPerSecond / 60;
        }

        // Get elapsed time since the previous Update call.
        public long ElapsedTicks => _elapsedTicks;
        public double ElapsedSeconds => TicksToSeconds(_elapsedTicks);

        // Get total time since the start of the program.
        public long TotalTicks => _totalTicks;
        public double TotalSeconds => TicksToSeconds(_totalTicks);

        // Get total number of updates since start of the program.
        public int FrameCount => _frameCount;

        // Get the current framerate.
        public int FramesPerSecond => _framesPerSecond;

        // Set whether to use fixed or variable timestep mode.
        public void SetFixedTimeStep(bool isFixedTimestep) => _isFixedTimeStep = isFixedTimestep;

        // Set how often to call Update when in fixed timestep mode
        public void SetTargetElapsedTicks(long targetElapsed) => _targetElapsedTicks = targetElapsed;
        public void SetTargetElapsedSeconds(double targetElapsed) => _targetElapsedTicks = SecondsToTicks(targetElapsed);

        // After an intentional timing discontinuity (for instance a blocking IO operation)
        // call this to avoid having the fixed timestep logic attempt a set of catch-up 
        // Update calls.

        public void ResetElapsedTime()
        {
            _lastTime = Stopwatch.GetTimestamp();

            _leftOverTicks = 0;
            _framesPerSecond = 0;
            _framesThisSecond = 0;
            _secondCounter = 0;
        }

        // Update timer state, calling the specified Update function the appropriate number of times.
        public void Tick(Action<StepTimer> update)
        {
            // Query the current time.
            long currentTime = Stopwatch.GetTimestamp();

            long timeDelta = currentTime - _lastTime;

            _lastTime = currentTime;
            _secondCounter += timeDelta;

            // Clamp excessively large time deltas (e.g. after paused in the debugger).
            if (timeDelta > _maxDelta)
            {
                timeDelta = _maxDelta;
            }

            // Convert Stopwatch units into a canonical tick format. This cannot overflow due to the previous clamp.
            timeDelta *= TicksPerSecond;
            timeDelta /= _frequency;

            int lastFrameCount = _frameCount;

            if (_isFixedTimeStep)
            {
                // Fixed timestep update logic

                // If the app is running very close to the target elapsed time (within 1/4 of a millisecond) just clamp
                // the clock to exactly match the target value. This prevents tiny and irrelevant errors
                // from accumulating over time. Without this clamping, a game that requested a 60 fps
                // fixed update, running with vsync enabled on a 59.94 NTSC display, would eventually
                // accumulate enough tiny errors that it would drop a frame. It is better to just round 
                // small deviations down to zero to leave things running smoothly.

                if (Math.Abs(timeDelta - _targetElapsedTicks) < TicksPerSecond / 4000)
                {
                    timeDelta = _targetElapsedTicks;
                }

                _leftOverTicks += timeDelta;

                while (_leftOverTicks >= _targetElapsedTicks)
                {
                    _elapsedTicks = _targetElapsedTicks;
                    _totalTicks += _targetElapsedTicks;
                    _leftOverTicks -= _targetElapsedTicks;
                    _frameCount++;

                    update(this);
                }
            }
            else
            {
                // Variable timestep update logic.
                _elapsedTicks = timeDelta;
                _totalTicks += timeDelta;
                _leftOverTicks = 0;
                _frameCount++;

                update(this);
            }

            // Track the current framerate.
            if (_frameCount != lastFrameCount)
            {
                _framesThisSecond++;
            }

            if (_secondCounter >= _frequency)
            {
                _framesPerSecond = _framesThisSecond;
                _framesThisSecond = 0;
                _secondCounter %= _frequency;
            }
        }

        private static double TicksToSeconds(long ticks) => (double)ticks / TicksPerSecond;
        private static long SecondsToTicks(double seconds) => (long)(seconds * TicksPerSecond);
    }
}
