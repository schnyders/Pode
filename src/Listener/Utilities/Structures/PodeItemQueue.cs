using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pode.Utilities.Structures
{
    public class PodeItemQueue<T> : IDisposable
    {
        // the queued items, and a semaphore counting how many are available - waiting on the semaphore
        // asynchronously means that idle callers of GetAsync don't each block a thread pool thread
        private ConcurrentQueue<T> Items = default;
        private SemaphoreSlim ItemsAvailable = default;
        private CancellationTokenSource DisposeTokenSource = default;
        private List<T> ProcessingItems = default;
        private readonly bool TrackProcessing = true;
        private bool IsDisposed = false;

        public int Count { get => Items.Count + ProcessingItems.Count; }
        public int QueuedCount { get => Items.Count; }
        public int ProcessingCount { get => ProcessingItems.Count; }

        public PodeItemQueue(bool trackProcessing = true)
        {
            Items = new ConcurrentQueue<T>();
            ItemsAvailable = new SemaphoreSlim(0);
            DisposeTokenSource = new CancellationTokenSource();
            TrackProcessing = trackProcessing;
            ProcessingItems = new List<T>();
        }

        public T Get(CancellationToken cancellationToken = default)
        {
            if (IsDisposed)
            {
                return default;
            }

            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposeTokenSource.Token))
            {
                while (true)
                {
                    ItemsAvailable.Wait(linked.Token);
                    if (Items.TryDequeue(out var item))
                    {
                        AddProcessing(item);
                        return item;
                    }
                }
            }
        }

        public async Task<T> GetAsync(CancellationToken cancellationToken = default)
        {
            if (IsDisposed)
            {
                return default;
            }

            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposeTokenSource.Token))
            {
                while (true)
                {
                    await ItemsAvailable.WaitAsync(linked.Token).ConfigureAwait(false);
                    if (Items.TryDequeue(out var item))
                    {
                        AddProcessing(item);
                        return item;
                    }
                }
            }
        }

        public void Add(T item)
        {
            if (IsDisposed)
            {
                return;
            }

            Items.Enqueue(item);
            ItemsAvailable.Release();
        }

        public void AddProcessing(T item)
        {
            if (!TrackProcessing || IsDisposed)
            {
                return;
            }

            lock (ProcessingItems)
            {
                ProcessingItems.Add(item);
            }
        }

        public void RemoveProcessing(T item)
        {
            if (!TrackProcessing || IsDisposed)
            {
                return;
            }

            lock (ProcessingItems)
            {
                ProcessingItems.Remove(item);
            }
        }

        public T[] ToArray()
        {
            if (IsDisposed)
            {
                return Array.Empty<T>();
            }

            return Items.ToArray();
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;

            // cancel any callers still waiting for an item, and dispose the queue
            DisposeTokenSource.Cancel();
            DisposeTokenSource.Dispose();
            DisposeTokenSource = null;

            ItemsAvailable.Dispose();
            ItemsAvailable = null;
            Items = null;

            // clear the processing items
            ProcessingItems.Clear();
            ProcessingItems = null;

            GC.SuppressFinalize(this);
        }
    }
}