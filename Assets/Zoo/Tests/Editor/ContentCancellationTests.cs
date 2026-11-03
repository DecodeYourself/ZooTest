using System;
using System.Collections;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zoo.Content;

namespace Zoo.Tests
{
    public sealed class ContentCancellationTests
    {
        [UnityTest] public IEnumerator PreCancelledReloadDoesNotRead() => UniTask.ToCoroutine(async () =>
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            int reads = 0;
            var service = new ContentService((path, token) => { reads++; return UniTask.FromResult(""); });
            bool cancelled = await service.ReloadAsync(cancellation.Token).SuppressCancellationThrow();
            Assert.That(cancelled, Is.True);
            Assert.That(reads, Is.Zero);
            Assert.That(service.IsLoading, Is.False);
            Assert.That(service.ApplyPending(), Is.False);
        });

        [UnityTest] public IEnumerator CancellationDuringReadPreservesCurrentAndAllowsRetry() => UniTask.ToCoroutine(async () =>
        {
            using var cancellation = new CancellationTokenSource();
            var gate = new UniTaskCompletionSource<string>();
            bool block = false;
            CancellationToken received = default;
            var service = new ContentService((path, token) =>
            {
                received = token;
                return block ? gate.Task.AttachExternalCancellation(token) : UniTask.FromResult(File.ReadAllText(path));
            });
            await service.ReloadAsync(CancellationToken.None);
            service.ApplyPending();
            var current = service.Current;
            block = true;
            var reload = service.ReloadAsync(cancellation.Token).SuppressCancellationThrow();
            Assert.That(service.IsLoading, Is.True);
            Assert.That(received, Is.EqualTo(cancellation.Token));
            cancellation.Cancel();
            Assert.That(await reload, Is.True);
            gate.TrySetResult("ignored late result");
            Assert.That(service.IsLoading, Is.False);
            Assert.That(service.ApplyPending(), Is.False);
            Assert.That(service.Current, Is.SameAs(current));
            block = false;
            await service.ReloadAsync(CancellationToken.None);
            Assert.That(service.ApplyPending(), Is.True);
        });

        [UnityTest] public IEnumerator CancelledAfterLastReadCannotPublish() => UniTask.ToCoroutine(async () =>
        {
            using var cancellation = new CancellationTokenSource();
            var service = new ContentService((path, token) =>
            {
                var text = File.ReadAllText(path);
                if (path.EndsWith("level.json")) cancellation.Cancel();
                return UniTask.FromResult(text);
            });
            Assert.That(await service.ReloadAsync(cancellation.Token).SuppressCancellationThrow(), Is.True);
            Assert.That(service.ApplyPending(), Is.False);
        });
    }
}
