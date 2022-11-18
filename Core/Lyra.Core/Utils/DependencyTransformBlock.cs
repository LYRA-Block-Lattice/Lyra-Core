using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Lyra.Core.Utils
{
    // https://stackoverflow.com/questions/64380946/tpl-dataflow-queue-with-postponement
    public class DependencyTransformBlock<TInput, TKey, TOutput> :
        ITargetBlock<TInput>, ISourceBlock<TOutput>
    {
        private readonly ITargetBlock<TInput> _inputBlock;
        private readonly IPropagatorBlock<Item, TOutput> _transformBlock;

        private readonly object _locker = new object();
        private readonly Dictionary<TKey, Item> _items;

        private int _pendingCount = 1;
        // The initial 1 represents the completion of the _inputBlock

        private class Item
        {
            public TKey Key;
            public TInput Input;
            public bool HasInput;
            public bool IsCompleted;
            public HashSet<Item> Dependencies;
            public HashSet<Item> Dependents;

            public Item(TKey key) => Key = key;
        }

        public DependencyTransformBlock(
            Func<TInput, Task<TOutput>> transform,
            Func<TInput, TKey> keySelector,
            Func<TInput, IReadOnlyCollection<TKey>> dependenciesSelector,
            ExecutionDataflowBlockOptions dataflowBlockOptions = null,
            IEqualityComparer<TKey> keyComparer = null)
        {
            if (transform == null)
                throw new ArgumentNullException(nameof(transform));
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));
            if (dependenciesSelector == null)
                throw new ArgumentNullException(nameof(dependenciesSelector));

            dataflowBlockOptions =
                dataflowBlockOptions ?? new ExecutionDataflowBlockOptions();
            keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;

            _items = new Dictionary<TKey, Item>(keyComparer);

            _inputBlock = new ActionBlock<TInput>(async input =>
            {
                var key = keySelector(input);
                var dependencyKeys = dependenciesSelector(input);
                bool isReadyForProcessing = true;
                Item item;
                lock (_locker)
                {
                    if (!_items.TryGetValue(key, out item))
                    {
                        item = new Item(key);
                        _items.Add(key, item);
                    }
                    if (item.HasInput)
                        throw new InvalidOperationException($"Duplicate key ({key}).");
                    item.Input = input;
                    item.HasInput = true;

                    if (dependencyKeys != null && dependencyKeys.Count > 0)
                    {
                        item.Dependencies = new HashSet<Item>();
                        foreach (var dependencyKey in dependencyKeys)
                        {
                            if (!_items.TryGetValue(dependencyKey, out var dependency))
                            {
                                dependency = new Item(dependencyKey);
                                _items.Add(dependencyKey, dependency);
                            }
                            if (!dependency.IsCompleted)
                            {
                                item.Dependencies.Add(dependency);
                                if (dependency.Dependents == null)
                                    dependency.Dependents = new HashSet<Item>();
                                dependency.Dependents.Add(item);
                            }
                        }
                        isReadyForProcessing = item.Dependencies.Count == 0;
                    }
                    if (isReadyForProcessing) _pendingCount++;
                }
                if (isReadyForProcessing)
                {
                    await _transformBlock.SendAsync(item);
                }
            }, new ExecutionDataflowBlockOptions()
            {
                CancellationToken = dataflowBlockOptions.CancellationToken,
                BoundedCapacity = 1
            });

            var middleBuffer = new BufferBlock<Item>(new DataflowBlockOptions()
            {
                CancellationToken = dataflowBlockOptions.CancellationToken,
                BoundedCapacity = DataflowBlockOptions.Unbounded
            });

            _transformBlock = new TransformBlock<Item, TOutput>(async item =>
            {
                try
                {
                    TInput input;
                    lock (_locker)
                    {
                        Debug.Assert(item.HasInput && !item.IsCompleted);
                        input = item.Input;
                    }
                    var result = await transform(input).ConfigureAwait(false);
                    lock (_locker)
                    {
                        item.IsCompleted = true;
                        if (item.Dependents != null)
                        {
                            foreach (var dependent in item.Dependents)
                            {
                                Debug.Assert(dependent.Dependencies != null);
                                var removed = dependent.Dependencies.Remove(item);
                                Debug.Assert(removed);
                                if (dependent.HasInput
                                    && dependent.Dependencies.Count == 0)
                                {
                                    middleBuffer.Post(dependent);
                                    _pendingCount++;
                                }
                            }
                        }
                        item.Input = default; // Cleanup
                        item.Dependencies = null;
                        item.Dependents = null;
                    }
                    return result;
                }
                finally
                {
                    lock (_locker)
                    {
                        _pendingCount--;
                        if (_pendingCount == 0) middleBuffer.Complete();
                    }
                }
            }, dataflowBlockOptions);

            middleBuffer.LinkTo(_transformBlock);

            PropagateCompletion(_inputBlock, middleBuffer,
                condition: () => { lock (_locker) return --_pendingCount == 0; });
            PropagateCompletion(middleBuffer, _transformBlock);
            PropagateFailure(_transformBlock, middleBuffer);
            PropagateFailure(_transformBlock, _inputBlock);
        }

        // Constructor with synchronous lambda
        public DependencyTransformBlock(
            Func<TInput, TOutput> transform,
            Func<TInput, TKey> keySelector,
            Func<TInput, IReadOnlyCollection<TKey>> dependenciesSelector,
            ExecutionDataflowBlockOptions dataflowBlockOptions = null,
            IEqualityComparer<TKey> keyComparer = null) : this(
                input => Task.FromResult(transform(input)),
                keySelector, dependenciesSelector, dataflowBlockOptions, keyComparer)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
        }

        public TInput[] Unprocessed
        {
            get
            {
                lock (_locker) return _items.Values
                    .Where(item => item.HasInput && !item.IsCompleted)
                    .Select(item => item.Input)
                    .ToArray();
            }
        }

        public Task Completion => _transformBlock.Completion;
        public void Complete() => _inputBlock.Complete();
        void IDataflowBlock.Fault(Exception ex) => _inputBlock.Fault(ex);

        DataflowMessageStatus ITargetBlock<TInput>.OfferMessage(
            DataflowMessageHeader header, TInput value, ISourceBlock<TInput> source,
            bool consumeToAccept)
        {
            return _inputBlock.OfferMessage(header, value, source, consumeToAccept);
        }

        TOutput ISourceBlock<TOutput>.ConsumeMessage(DataflowMessageHeader header,
            ITargetBlock<TOutput> target, out bool messageConsumed)
        {
            return _transformBlock.ConsumeMessage(header, target, out messageConsumed);
        }

        bool ISourceBlock<TOutput>.ReserveMessage(DataflowMessageHeader header,
            ITargetBlock<TOutput> target)
        {
            return _transformBlock.ReserveMessage(header, target);
        }

        void ISourceBlock<TOutput>.ReleaseReservation(DataflowMessageHeader header,
            ITargetBlock<TOutput> target)
        {
            _transformBlock.ReleaseReservation(header, target);
        }

        public IDisposable LinkTo(ITargetBlock<TOutput> target,
            DataflowLinkOptions linkOptions)
        {
            return _transformBlock.LinkTo(target, linkOptions);
        }

        private async void PropagateCompletion(IDataflowBlock source,
            IDataflowBlock target, Func<bool> condition = null)
        {
            try { await source.Completion.ConfigureAwait(false); } catch { }
            if (source.Completion.IsFaulted)
                target.Fault(source.Completion.Exception.InnerException);
            else
                if (condition == null || condition()) target.Complete();
        }

        private async void PropagateFailure(IDataflowBlock source,
            IDataflowBlock target)
        {
            try { await source.Completion.ConfigureAwait(false); } catch { }
            if (source.Completion.IsFaulted)
                target.Fault(source.Completion.Exception.InnerException);
        }
    }

    /*
     var block = new DependencyTransformBlock<Item, string, Item>(item =>
        {
            DoWork(item);
            return item;
        },
        keySelector: item => item.Name,
        dependenciesSelector: item => item.DependsOn,
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        },
        keyComparer: StringComparer.OrdinalIgnoreCase);

        //...

        block.LinkTo(DataflowBlock.NullTarget<Item>());

    */
}
