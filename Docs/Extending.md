# Extending movement and collision rules

## New species using existing mechanics

Add an AnimalDefinition, ViewDefinition and spawn entry in one versioned content release. For example,
caracal combines `foodRole: predator` with `movement.kind: jump`. Its code is shared with the frog.
No application update is needed when the required movement/actions already exist in the application.

## New movement algorithm: periodic dash

`dash` is now implemented in `Assets/Zoo/Runtime/Simulation/DashMovement.cs`, registered in
MovementFactory and accepted by ContentCatalog validation. The existing species keep their original
movement. To opt in, set a species' movement to the JSON below and reload its content set. The steps
and example implementation here also describe the pattern for adding another strategy.

The current strategy interface controls speed and visual height. AnimalActor controls direction,
steering, contacts and Rigidbody velocity. A periodic dash can use the existing MovementDefinition
fields: `speed`, `intervalSeconds`, `durationSeconds`.

1. Implement `IMovementBehavior` in Runtime/Simulation. Each actor needs its own instance
   because the strategy stores its phase. Integrate the active portion of each physics step rather
   than rounding duration to an integer number of physics steps:

```csharp
public sealed class DashMovement : IMovementBehavior
{
    readonly MovementDefinition definition;
    float phase;
    public DashMovement(MovementDefinition definition) { this.definition = definition; }
    public float VisualHeight => 0;

    public float Speed(float dt)
    {
        if (dt <= 0) return 0;
        float total = phase + dt;
        float cycles = Mathf.Floor(total / definition.intervalSeconds);
        float next = total % definition.intervalSeconds;
        float activeTime = cycles * definition.durationSeconds
            + Mathf.Min(next, definition.durationSeconds)
            - Mathf.Min(phase, definition.durationSeconds);
        phase = next;
        return definition.speed * activeTime / dt;
    }
}
```

The example belongs in `Zoo.Simulation` and uses `UnityEngine` and `Zoo.Content`.

2. Register the strategy in MovementFactory (`"dash" => new DashMovement(definition)` is already present).
3. Extend ContentCatalog validation: dash requires finite positive speed and interval/duration,
   and require `durationSeconds <= intervalSeconds`. Keep the existing jump and linear validation.
4. Test active/rest periods, distance across non-divisible physics steps and invalid timings.
   DashMovementTests covers these cases, multiple cycles in a single step and independent actor state.
5. Build an application containing this C# capability. Content releases can then select:

```json
"movement": {
  "kind": "dash",
  "speed": 6,
  "intervalSeconds": 2,
  "durationSeconds": 0.3
}
```

This yields 1.8 units of unimpeded movement per 2-second cycle. Contacts can still affect displacement.
Changing these parameters later requires only a content release. A brand-new C# strategy cannot be
delivered by AssetBundles. For movement that also chooses direction (orbit, pursuit, zigzag), extend
the interface to return a movement intent including heading; the current speed-only interface cannot
express those behaviors by itself. Keep Rigidbody writes in AnimalActor.

## Change an existing collision rule using JSON

Rules currently match symmetric pairs of food roles, not species IDs. Replace the existing
predator/predator entry in interactions.json with the following to disable predators eating each other:

```json
{"roleA":"predator", "roleB":"predator", "action":"bounce"}
```

Replace the entry, do not append a duplicate pair. The validator rejects duplicates and requires a
rule for every role pair present in the animal catalog. This example changes the assignment behavior;
it is a demonstration, not the shipped default.

The current supported actions are `bounce` and `consume`. Winner policies for consume are:
- `predator`: predator wins a mixed predator/prey contact.
- `lowerId`: the smaller positive instance ID wins; works regardless of callback argument order.

For local testing edit StreamingAssets/Zoo/interactions.json and click Reload JSON. This loads the
entire local content set, so it also switches back to the local animals/level configuration.
For remote testing create a new release with the updated interactions TextAsset and matching full
config set, publish its manifest/catalog/bundles and point ZooBootstrap's releaseManifestUrl at it.
Do not overwrite release-2; the prototype builder is currently specific to release-2 and must be
parameterized or extended for another release. The UI button does not discover new release IDs by itself.

Interaction rules switch globally at a physics boundary, including for existing animals. Existing
animals retain their species definition, including foodRole; changing a species role affects only
new instances. Physics separation, pair deduplication, immediate death flags and statistics stay in
InteractionResolver and AnimalActor.

## New outcome or species-specific food chain

To add an outcome such as stun, extend the rule DTO/validation and resolver execution. The current
Winner function returns only a winning ID or zero, so it is insufficient for nonfatal effects; use
an explicit outcome type instead of disguising a stun as consumption.

To express "snake eats frog but does not eat caracal", add optional species/tag conditions to the
rule model, a stable priority scheme (species rule before role fallback), and ambiguity validation.
Pass both animal definitions into policy evaluation. Preserve symmetric lookup and ensure swapping
contact order never changes the winner. An immune contact should resolve as bounce.

These are C# extensions requiring an application update once. After the evaluator understands the
new conditions/actions, their combinations can be changed remotely through JSON. Do not add species
name checks to OnCollisionEnter or create a MonoBehaviour subclass for each species.
