# Review against Zoo world assignment

Reviewed 2026-11-03 against both pages of ../Zoo world_2026.pdf.

## Requirement coverage

- Top-down 3D camera, primitive graphics, 1-2 second randomized spawning: implemented.
- Physics contact, prey/prey separation, predator/prey consumption, one winner for predator/predator: implemented.
- Returning toward the screen after crossing its bounds: implemented with steering.
- Frog periodic jumps, snake constant-speed movement, Tasty label, uGUI death counters: implemented.
- No ECS: satisfied. Composition and data-driven species replace species subclasses.
- Git repository: https://github.com/DecodeYourself/ZooTest (source and reproducible content tooling).
- Frameworks/DI are encouraged, not named as mandatory dependencies.

The "1000 different animals" paragraph explicitly lists birds, spiders, fish, crabs and asks for clear,
extendable, understandable animal architecture. It does not specify 1000 simultaneous agents, target
hardware, frame budget or frame rate. Performance at that population is a separate measurable target.

## Findings addressed in this change

1. Async work could outlive its world/actor. UniTask and required CancellationToken parameters now
   propagate through config reads, manifest/catalog/config loads, visual preloads and attachments.
   Cancellation is not converted into a content-error warning. Candidates are checked before staging.
2. A cached Task could retain failures; a naive replacement with a cached UniTask would also violate
   multi-await semantics. The visual cache now owns Addressables handles and creates a separate
   cancellation-aware awaiter per client. Cancelling one client preserves other clients and the cache.
3. Loader shutdown now cancels waiters, releases owned handles and removes owned resource locators.
   Failed/cancelled catalog acquisition also cleans up locators registered on late completion.
4. HUD and auto-created EventSystem were scene roots independent of ZooBootstrap. They now belong
   to its hierarchy and are destroyed with the world.
5. FallbackViewPool now reuses geometry, shares materials by color and bounds inactive instances.
   Replacement/animal death returns a lease; cached prefabs bypass fallback creation altogether.
6. ZooBootstrap now only composes dependencies and forwards Unity lifecycle calls. AnimalFactory,
   SpawnService, ZooWorld, CameraArena and ZooContentController own construction, spawn policy,
   world scheduling, screen bounds and content commands respectively.

Addressables cancellation releases the owner's reference/stops waiting; it does not promise to abort
every native AssetBundle download. Shared requests intentionally continue for other consumers.
Local File.ReadAllText remains synchronous for small configs; token checks surround it, but cannot
interrupt the OS read mid-call. Network requests use cancellation-aware UnityWebRequest adapters.

## Accepted scope and remaining checks

- Accepted: sequential preload remains unchanged by the user's decision. At 1000 visual assets its
  update latency/resident-memory cost should be measured before introducing a different policy.
- Accepted: the existing speed/visual-height movement interface and random steering remain unchanged;
  they cover the PDF's requirements. Pursuit/orbit/flight mechanics are outside the current scope.
- Coverage gap — automated tests exercise interaction resolution and movement, but do not yet assert
  physical prey/prey separation or camera-bound return end to end. Add these before relying on the
  prototype for substantial physics changes.

## Dependency decisions

Do not add Zenject solely to satisfy a dependency list. Constructor/method injection and an explicit
composition root already demonstrate DI. A container becomes useful for several scene/application
scopes, interchangeable service graphs, automatic lifetime management and graph validation. If used,
resolve dependencies at creation boundaries, not once per actor update.

Do not add DOTween for the simulation. PhysX remains authoritative for the physical body and the visual
jump already has interpolation. A tween library is useful for richer HUD/popups/transitions, but it
does not solve collision rules or agent scaling. Never independently animate the same body transform
from both its motor and a tween.

## A separate 1000-simultaneous-agent target

Measure a Development Player on a named target device with 100/500/1000/2000 agents, varying contact
density and movement mix. Record CPU and GPU frame times, p95/p99, Physics.Simulate, scripts, draw calls,
GC and resident memory. Editor performance and an agent count alone are insufficient evidence.

Likely priorities: pool actors/labels, reduce unnecessary continuous
collision detection (after correctness checks), use collision layers/simple colliders, batch identical
visuals, avoid rebuilding HUD strings every frame, and budget AI decisions/pathfinding separately from
physics/presentation. Add spatial queries or Jobs/Burst for measured pure-math hot spots if needed;
retain the assignment's prohibition on ECS. UniTask improves async orchestration, not physics cost.

## Verification

Cancellation tests cover pre-cancelled reads, cancellation during a read, late cancellation before
publication, retry, independent shared-prefab waiters, disposal during pending loads, actor death
during attachment and pre-cancelled remote requests. Existing rules/movement tests remain applicable.
Full 1000-agent stress measurements and a standalone player build are not part of this review.

Results: 37 EditMode tests and 7 PlayMode tests passed. After migration, the release-2 HTTP download
smoke check also passed for the remote JSON and caracal prefab. Compilation completed without errors.

Follow-up refactor: tests additionally cover geometry/material reuse, lease cancellation, inactive-pool
limits, cached-prefab bypass, factory material sharing, and world content switches without respawning
the initial population. Actor/label pooling remains a separate optimization, not implemented here.
The follow-up suite passed 37 EditMode and 14 PlayMode tests. The real Zoo scene switched from
prototype-1 to release-2 through the UI and spawned caracals with the refactored factory/world services.
