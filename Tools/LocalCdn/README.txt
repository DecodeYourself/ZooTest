release-2 (Windows 64-bit)

Start start_local_cdn.cmd. The server root is this cdn_server directory, port 8080.
Manifest: http://127.0.0.1:8080/release-2/release.json

In Zoo.unity enter Play Mode with Use Existing Build and click Update content.
Wait for Active: release-2. New spawns are caracal jumping predators.
Reload JSON returns to the local prototype-1 config; it is not required after updating.

JSON configs and caracal prefab are inside the AssetBundle. The manifest is published last.
The original cdn_server.py was retained. stdout/stderr logs are local diagnostics.
