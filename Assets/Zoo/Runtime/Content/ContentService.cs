using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Zoo.Content
{
    public sealed class ContentService
    {
        public ContentCatalog Current { get; private set; }
        ContentCatalog pending;
        readonly Func<string, Task<string>> read;
        public ContentService(Func<string, Task<string>> read = null) { this.read = read ?? ReadAsync; }
        public bool IsLoading { get; private set; }
        public string Status { get; private set; } = "Loading content...";

        public void Stage(ContentCatalog candidate)
        {
            pending = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Status = "Validated " + candidate.Version + "; awaiting physics step";
        }

        public async Task ReloadAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                // Each document is staged before publication. A version directory can be used remotely.
                var root = Path.Combine(Application.streamingAssetsPath, "Zoo");
                var a = await read(Path.Combine(root, "animals.json"));
                var r = await read(Path.Combine(root, "interactions.json"));
                var l = await read(Path.Combine(root, "level.json"));
                Stage(ContentCatalog.Parse(a, r, l));
            }
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

        static async Task<string> ReadAsync(string path)
        {
            // StreamingAssets is a URL on Android/WebGL, and a real file in the editor/desktop.
            if (!path.Contains("://")) return File.ReadAllText(path);
            using var request = UnityWebRequest.Get(path);
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
            return request.downloadHandler.text;
        }
    }
}
