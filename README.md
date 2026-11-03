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
Use `linear` with positive `speed`, or `jump` with positive distance/duration/interval (interval >= duration).
To add a genuinely new mechanic, implement `IMovementBehavior`, register it in `MovementFactory`,
and extend validation. JSON selects compiled capabilities; it is not executable code.

During Play Mode, edit the files and click **Reload JSON**. The entire candidate is validated before
publication at a physics boundary. Existing actors retain their species/view definitions; interaction
rules and spawn settings switch globally. Invalid content preserves the current catalog.
Desktop/editor reads from disk. StreamingAssets URL reading supports packaged platforms, but editing
StreamingAssets after installation is not a remote configuration service. A remote JSON transport and
versioned release manifest are future work; the reader can be injected into `ContentService`.

## Architecture

`ZooBootstrap` composes content, interactions, movement, UI and visual loading. Services receive
dependencies explicitly. `AnimalActor` owns physical state, lifecycle and per-instance movement.
It reports collisions to `InteractionResolver`, which deduplicates/sorts pairs and marks death immediately.
`ZooHud` observes results; `AnimalViewLoader` caches Addressables handles and releases them on shutdown.
Visual prefabs contain no gameplay colliders or behavior. Fallback geometry keeps actors visible on load failure.

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
5. Prefer new immutable addresses such as `animals/frog/v2`. Click **Update visuals**, then change
   JSON to select the new view/address and click **Reload JSON**. Old actors keep their old visual.

Unique Bundle IDs are enabled to support old and new content coexisting. Catalog updates do not replace
already-instantiated objects. Production hosting, cross-platform bundles, remote configuration deployment
and a real HTTP update run are not yet validated. The prototype has no production CDN dependency.

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

Remote Git publication is pending a destination repository. Generated caches, IDE project files,
debug captures and build outputs are excluded; required Unity `.meta` files are versioned.
