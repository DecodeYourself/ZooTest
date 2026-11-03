# Zoo World implementation plan

## Accepted decisions
- Unity 6, GameObjects and PhysX; no ECS/DOTS. No Burst until profiling identifies a need.
- Composition: species data selects movement strategy and food role. No species subclass hierarchy.
- Explicit constructor injection for services, composition root in ZooBootstrap; no service locator.
- Versioned JSON in StreamingAssets/Zoo: animals (including visual addresses), interactions, level.
- Validate an entire candidate before applying it at a physics-step boundary. Invalid reload preserves the current catalog.
- Existing animals pin their species definition and visual version; new spawns use the new catalog. Interaction rules and spawning settings switch together globally.
- Addressables loads visual-only prefabs. AnimalActor owns state, Rigidbody and collider. Local primitives are the fallback.
- Remote catalog/bundles can supply visual updates, not C# implementations. Live visual replacement of existing animals is deferred.
- Remote publishing requires a chosen hosting URL; initially support a local HTTP demo profile.

## Milestones
1. Verify CLI/MCP; establish architecture, JSON schemas, validation and core tests.
2. Playable Zoo scene: camera bounds, 1-2 second spawn, frog jumps, snake linear movement, collisions, exactly-once death, Tasty, uGUI counters.
3. Addressables local content and remote profile; safe manual catalog/config reload, versioned addresses and fallback.
4. EditMode tests for rules/validation, PlayMode smoke check and visual inspection. Document extension and content update workflow.

## Acceptance
- Prey/prey contacts separate physically. Predator/prey kills prey. Predator/predator has exactly one survivor for a pair.
- Dead actors cannot eat or be counted twice, including multi-contact situations.
- Outside-camera animals turn inward; movement remains on the ground plane.
- A third species using existing movement requires only JSON and an optional visual asset.
- Invalid reload leaves the running simulation intact; valid reload affects subsequent spawns.
- Remote address failure preserves a usable fallback visual.
- No remote deployment or git publication until hosting/repository destination is known.

## Progress
- CLI and MCP verified against the running project.
- Milestones 1 and 2 implemented; Zoo scene visually inspected in Play Mode.
- Milestone 3: local Addressables built; remote localhost profile and manual catalog update implemented.
  Remote JSON TextAssets and caracal prefab in release-2 were subsequently downloaded and validated
  from the local HTTP server. Update content stages the release after all visual dependencies load.
- Milestone 4: EditMode and PlayMode suites pass; standalone player build/performance profiling remain outstanding.
- Continuous presentation: interpolated jump height/heading, smooth steering, staggered jump phases,
  Run In Background enabled. PlayMode verifies movement between physics ticks.
