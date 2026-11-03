using System;
using Zoo.Content;

namespace Zoo.Simulation
{
    /// <summary>A ground-level burst of speed followed by a rest, repeated every interval.</summary>
    public sealed class DashMovement : IMovementBehavior
    {
        readonly double speed;
        readonly double interval;
        readonly double duration;
        double phase;

        public DashMovement(MovementDefinition definition)
        {
            speed = definition.speed;
            interval = definition.intervalSeconds;
            duration = definition.durationSeconds;
        }

        public float VisualHeight => 0;

        public float Speed(float deltaTime)
        {
            if (deltaTime <= 0) return 0;
            // Integrate every active slice, including steps crossing multiple cycle boundaries.
            // Return average speed for this physics step so distance is independent of step size.
            var total = phase + deltaTime;
            var cycles = Math.Floor(total / interval);
            var nextPhase = total % interval;
            var activeTime = cycles * duration + Math.Min(nextPhase, duration) - Math.Min(phase, duration);
            phase = nextPhase;
            return (float)(speed * Math.Max(0, activeTime) / deltaTime);
        }
    }
}
