# Zoo World

Unity 6000.3.18f1 / URP / GameObjects / PhysX / uGUI / Addressables. No ECS.

Open `Assets/Zoo/Scenes/Zoo.unity` and press Play. Frogs jump, snakes move linearly;
contacts apply the JSON food-chain rules. Death totals are in the upper right.
Visual placeholders are intentional: the assignment evaluates architecture.

Physics runs at a fixed 50 Hz. Rigidbody position interpolation and render-frame interpolation
of jump height/heading make presentation continuous between physics steps. Animals turn gradually;
frog jump phases are staggered so the initial population does not move in lockstep. Frogs retain
their configured rest intervals. Run In Background is enabled for switching between Unity and Codex.

## Content and extension

`Assets/StreamingAssets/Zoo/` contains:
- `animals.json`: stable species IDs, food roles, movement strategies, versioned view IDs and Addressable addresses.
- `interactions.json`: symmetric role pairs, `bounce` or `consume`, and winner policy.
- `level.json`: camera size, initial population, spawn interval and weighted species table.

To add a species using existing mechanics, add its definition and spawn entry. Reuse a view ID or add a visual definition.
Use `linear` with positive `speed`, `jump` with positive distance/duration/interval (interval >= duration),
or `dash` with positive speed/duration/interval (interval >= duration). Dash moves at the configured
speed during each active window and rests for the remainder of the interval; it stays on the ground.
For example `{"kind":"dash","speed":6,"intervalSeconds":2,"durationSeconds":0.3}` gives an
unimpeded 1.8-unit dash every two seconds. Existing species retain their original movement.
To add a genuinely new mechanic, implement `IMovementBehavior`, register it in `MovementFactory`,
and extend validation. JSON selects compiled capabilities; it is not executable code.

During Play Mode, edit the files and click **Reload JSON**. The entire candidate is validated before
publication at a physics boundary. Existing actors retain their species/view definitions; interaction
rules and spawn settings switch globally. Invalid content preserves the current catalog.
Desktop/editor reads from disk. StreamingAssets URL reading supports packaged platforms, but editing
StreamingAssets after installation is not a remote configuration service. **Update content** uses a
remote release manifest and loads the three JSON documents as Addressable TextAssets instead.

## Architecture

`ZooBootstrap` is the composition root and forwards Unity lifecycle callbacks. Services receive
dependencies explicitly: `ZooWorld` owns the active population and physics-step scheduling;
`SpawnService` owns spawn timing, weighted selection and placement; `AnimalFactory` constructs actors,
shares their contact material and attaches visuals. `CameraArena` supplies screen bounds, while
`ZooContentController` coordinates local/remote content commands independently of the HUD.
`AnimalActor` owns physical state, lifecycle and per-instance movement.
It reports collisions to `InteractionResolver`, which deduplicates/sorts pairs and marks death immediately.
`ZooHud` observes results; `AnimalViewLoader` caches Addressables handles and releases them on shutdown.
Visual prefabs contain no gameplay colliders or behavior. Fallback geometry keeps actors visible on load failure.
`FallbackViewPool` reuses sphere/capsule instances, shares materials by color, and returns leases on
visual replacement or animal death. Scale and pose are reset on every rent. At most 32 inactive
instances per shape/color are retained; active animals always have separate objects. Cached prefabs
instantiate directly without creating a fallback. The world-owned loader disposes the pool/materials.

There is no species inheritance hierarchy. Adding 1000 species means extending data, not writing 1000 classes.
An actor limit, pooling and accelerated simulation are deliberately deferred until measurement shows a need.

## Addressables and remote content

The default **Animal Visuals** group is local and works offline. Initial local content is built.
Editor Play Mode normally uses Asset Database; use **Use Existing Build** to verify built bundles.

For a local remote-content demonstration:
1. Choose **Zoo > Content > Use remote localhost profile**. This selects `RemoteLocalhost`, enables
   Build Remote Catalog, and switches the visuals group to remote paths.
2. Choose **Zoo > Content > Build Addressables**. Build a player for the same target.
3. Serve `ServerData` on port 8080 (for example `python -m http.server 8080 --directory ServerData`).
   `Remote.LoadPath` is `http://127.0.0.1:8080/[BuildTarget]`; this is for desktop localhost tests only.
4. For an update, preserve the original content-state file. Use Addressables **Update a Previous Build**
   and publish the resulting bundles, catalog and hash. Never delete bundles required by older clients.
5. Prefer new immutable addresses such as `animals/frog/v2`. Publish a release manifest identifying
   the catalog and versioned config addresses. **Update content** downloads the configs and preloads
   their visuals before staging the candidate. Old actors keep their old visual.

Unique Bundle IDs are enabled to support old and new content coexisting. Catalog updates do not replace
already-instantiated objects. Production hosting and cross-platform bundles are not yet validated.

### Prepared release-2 demo

`Assets/Zoo/Releases/release-2` contains the authored JSON sources. The release adds caracal as a
jumping predator and intentionally sets all future spawns to caracal for a clear demonstration.
Startup still reads `prototype-1` from StreamingAssets.

**Zoo > Content > Build CDN release-2** publishes a full, separately loadable catalog plus the remote
config/prefab bundle to `../cdn_server/release-2/StandaloneWindows64`, then writes `release.json` last.
It selects **Use Existing Build** for Play Mode. This is a new catalog release, not a patch generated
against the earlier local build's content-state file. Do not overwrite immutable published releases
for production; use a new release directory and addresses.

Start `../cdn_server/start_local_cdn.cmd`, enter Play Mode and click **Update content**.
The default manifest URL is `http://127.0.0.1:8080/release-2/release.json` (serialized on ZooBootstrap).
When status becomes `Active: release-2`, the next spawn uses caracal. Do not click **Reload JSON**
afterwards unless intentionally returning to the local prototype-1 configuration.

Actual HTTP download of catalog, config TextAssets and caracal prefab was verified in Unity Play Mode.
The server log contains their successful GET requests. Downloads may subsequently use Unity's bundle cache.
The current release targets Windows 64-bit only. **Zoo > Content > Verify CDN release-2 download**
performs a download/validation smoke check during Play Mode without applying it to the world.

**Zoo > Set up prototype scene and visuals** creates missing initial assets. It does not overwrite an
existing Zoo scene. It restores local group paths; select the Default profile as well when returning
from the remote demonstration. Addressables remote settings are project assets and should be reviewed in Git.

## Verification

EditMode tests cover shipped data, invalid references/movement/rule matrices, callback-order-independent
winner selection, transactional reload and jump distance. PlayMode tests cover duplicate/multi-contact
death accounting. Run from **Window > General > Test Runner** or Unity Pipeline `test.run`.
PlayMode also verifies that presentation changes on render frames between deliberately coarse physics steps.

CLI and MCP were both verified against the running editor. On this machine the CLI is at
`C:\Users\decode_yourself\AppData\Local\Unity\bin\unity.exe`; it may require the full path and local-server
access outside the Codex sandbox. The MCP connection is pinned to this project.

Repository: [DecodeYourself/ZooTest](https://github.com/DecodeYourself/ZooTest).
Generated caches, IDE project files,
debug captures and build outputs are excluded; required Unity `.meta` files are versioned.

Server source and launcher are versioned under `Tools/LocalCdn`. The release builder copies them to
the sibling `cdn_server` folder. Generated catalogs/bundles and local server logs stay outside Git.
See [extension examples](Docs/Extending.md) for adding movement and collision rules.

Async loading uses UniTask 2.5.11. Public async operations require a CancellationToken. World teardown
and actor death cancel their waits; the visual cache owns shared Addressables handles until disposal.
See [assignment review and scaling notes](Docs/CodeReview.md) for remaining limitations.
