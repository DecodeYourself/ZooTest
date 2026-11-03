using UnityEngine;
using Zoo.Content;

namespace Zoo.Simulation
{
    public interface IMovementBehavior
    {
        float Speed(float deltaTime);
        float VisualHeight { get; }
    }

    public sealed class LinearMovement : IMovementBehavior
    {
        readonly float speed;
        public LinearMovement(float speed) { this.speed = speed; }
        public float Speed(float deltaTime) => speed;
        public float VisualHeight => 0;
    }

    public sealed class JumpMovement : IMovementBehavior
    {
        readonly MovementDefinition definition;
        float phase;
        public JumpMovement(MovementDefinition definition, float initialPhase = 0)
        {
            this.definition = definition;
            phase = Mathf.Repeat(initialPhase, definition.intervalSeconds);
        }
        public float Speed(float deltaTime)
        {
            if (deltaTime <= 0) return 0;
            var total = phase + deltaTime;
            var cycles = Mathf.Floor(total / definition.intervalSeconds);
            var nextPhase = total % definition.intervalSeconds;
            var activeTime = cycles * definition.durationSeconds + Mathf.Min(nextPhase, definition.durationSeconds)
                - Mathf.Min(phase, definition.durationSeconds);
            phase = nextPhase;
            return definition.distance / definition.durationSeconds * activeTime / deltaTime;
        }
        public float VisualHeight => phase < definition.durationSeconds
            ? Mathf.Sin(Mathf.PI * phase / definition.durationSeconds) * 0.65f : 0;
    }

    public static class MovementFactory
    {
        public static IMovementBehavior Create(MovementDefinition definition) => definition.kind switch
        {
            "linear" => new LinearMovement(definition.speed),
            "jump" => new JumpMovement(definition, Random.Range(0, definition.intervalSeconds)),
            _ => throw new System.ArgumentException("Unsupported movement: " + definition.kind)
        };
    }
}
