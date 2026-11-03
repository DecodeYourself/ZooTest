using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zoo.Content
{
    public sealed class ContentCatalog
    {
        readonly Dictionary<string, AnimalDefinition> animals = new();
        readonly Dictionary<string, ViewDefinition> views = new();
        readonly Dictionary<string, InteractionRule> rules = new();
        public string Version { get; }
        public LevelDefinition Level { get; }
        public AnimalDefinition Animal(string id) => animals[id];
        public ViewDefinition View(string id) => views[id];
        public InteractionRule Rule(string a, string b) => rules[Pair(a, b)];
        static string Pair(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a + ":" + b : b + ":" + a;
        static void Require(bool ok, string message) { if (!ok) throw new ArgumentException(message); }
        static bool Positive(float value) => value > 0 && !float.IsInfinity(value);

        public static ContentCatalog Parse(string animals, string interactions, string level) => new(
            JsonUtility.FromJson<AnimalFile>(animals), JsonUtility.FromJson<InteractionFile>(interactions),
            JsonUtility.FromJson<LevelDefinition>(level));

        public ContentCatalog(AnimalFile source, InteractionFile interactions, LevelDefinition level)
        {
            Require(source != null && interactions != null && level != null, "Missing content document.");
            Require(source.schemaVersion == 1 && interactions.schemaVersion == 1 && level.schemaVersion == 1, "Unsupported schema version.");
            Require(!string.IsNullOrWhiteSpace(source.contentVersion), "Missing content version.");
            Require(source.animals != null && source.animals.Length > 0 && source.views != null, "Missing animals or views.");
            foreach (var view in source.views)
            {
                Require(view != null && !string.IsNullOrWhiteSpace(view.id), "Missing view ID.");
                Require(!views.ContainsKey(view.id), "Duplicate view: " + view.id);
                Require(view.fallback == "sphere" || view.fallback == "capsule", "Unknown fallback: " + view.fallback);
                Require(ColorUtility.TryParseHtmlString(view.color, out _), "Invalid view color.");
                Require(view.scale != null && view.scale.Length == 3 && Positive(view.scale[0]) && Positive(view.scale[1]) && Positive(view.scale[2]), "Invalid view scale.");
                views.Add(view.id, view);
            }
            var roles = new HashSet<string>();
            foreach (var animal in source.animals)
            {
                Require(animal != null && !string.IsNullOrWhiteSpace(animal.id), "Missing animal ID.");
                Require(!animals.ContainsKey(animal.id), "Duplicate animal: " + animal.id);
                Require(animal.foodRole == "prey" || animal.foodRole == "predator", "Unknown food role.");
                Require(animal.viewId != null && views.ContainsKey(animal.viewId), "Missing animal view: " + animal.viewId);
                var m = animal.movement;
                Require(m != null && (m.kind == "linear" || m.kind == "jump"), "Unknown movement.");
                if (m.kind == "linear") Require(Positive(m.speed), "Linear speed must be positive.");
                else Require(Positive(m.distance) && Positive(m.durationSeconds) && Positive(m.intervalSeconds) && m.intervalSeconds >= m.durationSeconds, "Invalid jump timing/distance.");
                animals.Add(animal.id, animal);
                roles.Add(animal.foodRole);
            }
            Require(interactions.rules != null, "Missing interaction rules.");
            foreach (var rule in interactions.rules)
            {
                Require(rule != null && (rule.roleA == "prey" || rule.roleA == "predator") && (rule.roleB == "prey" || rule.roleB == "predator"), "Unknown rule role.");
                Require(rule.action == "bounce" || rule.action == "consume", "Unknown rule action.");
                if (rule.action == "consume")
                    Require(rule.winner == "lowerId" || (rule.winner == "predator" && rule.roleA != rule.roleB), "Invalid winner policy.");
                var key = Pair(rule.roleA, rule.roleB);
                Require(!rules.ContainsKey(key), "Duplicate interaction rule.");
                rules.Add(key, rule);
            }
            foreach (var a in roles) foreach (var b in roles) Require(rules.ContainsKey(Pair(a, b)), "Missing interaction rule: " + a + "/" + b);
            Require(Positive(level.spawnMinSeconds) && Positive(level.spawnMaxSeconds) && level.spawnMaxSeconds >= level.spawnMinSeconds, "Invalid spawn interval.");
            Require(Positive(level.cameraHalfHeight) && level.initialPopulation >= 0 && level.initialPopulation <= 1000, "Invalid level settings.");
            Require(level.spawnTable != null && level.spawnTable.Length > 0, "Empty spawn table.");
            float total = 0;
            foreach (var entry in level.spawnTable)
            {
                Require(entry != null && entry.animalId != null && animals.ContainsKey(entry.animalId) && Positive(entry.weight), "Invalid spawn entry.");
                total += entry.weight;
            }
            Require(Positive(total), "Invalid total spawn weight.");
            Version = source.contentVersion;
            Level = level;
        }
    }
}
