using System;

namespace Zoo.Content
{
    // DTOs are deserialized once, validated as a complete set, then treated as immutable.
    [Serializable] public sealed class AnimalFile
    {
        public int schemaVersion;
        public string contentVersion;
        public AnimalDefinition[] animals;
        public ViewDefinition[] views;
    }
    [Serializable] public sealed class AnimalDefinition
    {
        public string id;
        public string foodRole;
        public string viewId;
        public MovementDefinition movement;
    }
    [Serializable] public sealed class MovementDefinition
    {
        public string kind;
        public float speed;
        public float intervalSeconds;
        public float distance;
        public float durationSeconds;
    }
    [Serializable] public sealed class ViewDefinition
    {
        public string id;
        public string address;
        public string fallback;
        public string color;
        public float[] scale;
    }
    [Serializable] public sealed class InteractionFile
    {
        public int schemaVersion;
        public InteractionRule[] rules;
    }
    [Serializable] public sealed class InteractionRule
    {
        public string roleA;
        public string roleB;
        public string action;
        public string winner;
    }
    [Serializable] public sealed class LevelDefinition
    {
        public int schemaVersion;
        public float spawnMinSeconds;
        public float spawnMaxSeconds;
        public int initialPopulation;
        public float cameraHalfHeight;
        public SpawnEntry[] spawnTable;
    }
    [Serializable] public sealed class SpawnEntry
    {
        public string animalId;
        public float weight;
    }
}
