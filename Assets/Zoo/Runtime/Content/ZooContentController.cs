using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zoo.Presentation;

namespace Zoo.Content
{
    /// <summary>Coordinates local/remote content commands and their presentation status.</summary>
    public sealed class ZooContentController
    {
        readonly ContentService content;
        readonly RemoteReleaseLoader remote;
        readonly AnimalViewLoader views;
        readonly string manifestUrl;
        bool updating;
        string remoteStatus = "";
        public string Status => content.Status + (string.IsNullOrEmpty(remoteStatus) ? "" : "\n" + remoteStatus);
        public ZooContentController(ContentService content, RemoteReleaseLoader remote, AnimalViewLoader views, string manifestUrl)
        { this.content = content; this.remote = remote; this.views = views; this.manifestUrl = manifestUrl; }
        public async UniTask ReloadLocalAsync(CancellationToken cancellationToken)
        {
            try { await content.ReloadAsync(cancellationToken); }
            catch (OperationCanceledException) { }
            catch (Exception error) { Debug.LogException(error); }
        }
        public async UniTask UpdateRemoteAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (updating) return;
            updating = true;
            remoteStatus = "Downloading remote release...";
            try
            {
                var candidate = await remote.LoadAsync(manifestUrl, views, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                content.Stage(candidate);
                remoteStatus = "Remote " + candidate.Version + " ready; visuals downloaded";
            }
            catch (OperationCanceledException) { remoteStatus = "Remote update cancelled"; }
            catch (Exception error) { remoteStatus = "Remote update failed: " + error.Message; Debug.LogWarning(remoteStatus); }
            finally { updating = false; }
        }
    }
}
