using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Zoo.Content
{
    public sealed class ContentService
    {
        public ContentCatalog Current { get; private set; }
        ContentCatalog pending;
        readonly Func<string, CancellationToken, UniTask<string>> read;
        public ContentService(Func<string, CancellationToken, UniTask<string>> read = null) { this.read = read ?? ReadAsync; }
        public bool IsLoading { get; private set; }
        public string Status { get; private set; } = "Loading content...";

        public void Stage(ContentCatalog candidate)
        {
            pending = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Status = "Validated " + candidate.Version + "; awaiting physics step";
        }

        public async UniTask ReloadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                // Each document is staged before publication. A version directory can be used remotely.
                var root = Path.Combine(Application.streamingAssetsPath, "Zoo");
                var a = await read(Path.Combine(root, "animals.json"), cancellationToken);
                var r = await read(Path.Combine(root, "interactions.json"), cancellationToken);
                var l = await read(Path.Combine(root, "level.json"), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var candidate = ContentCatalog.Parse(a, r, l);
                cancellationToken.ThrowIfCancellationRequested();
                Stage(candidate);
            }
            catch (OperationCanceledException) { Status = "Config reload cancelled"; throw; }
            catch (Exception error)
            {
                Status = "Config rejected: " + error.Message;
                Debug.LogWarning(Status);
            }
            finally { IsLoading = false; }
        }

        public bool ApplyPending()
        {
            if (pending == null) return false;
            Current = pending;
            pending = null;
            Status = "Active: " + Current.Version;
            return true;
        }

        static async UniTask<string> ReadAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // StreamingAssets is a URL on Android/WebGL, and a real file in the editor/desktop.
            if (!path.Contains("://"))
            {
                var text = File.ReadAllText(path);
                cancellationToken.ThrowIfCancellationRequested();
                return text;
            }
            using var request = UnityWebRequest.Get(path);
            request.timeout = 20;
            await request.SendWebRequest().WithCancellation(cancellationToken, cancelImmediately: true);
            if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
            return request.downloadHandler.text;
        }
    }
}
