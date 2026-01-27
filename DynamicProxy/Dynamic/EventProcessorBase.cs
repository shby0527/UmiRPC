using System.Collections.Concurrent;
using DynamicProxy;

namespace Umi.Proxy.Dynamic.Dynamic;

public abstract class EventProcessorBase : IEventProcessor
{
    // ReSharper disable once InconsistentNaming
    protected readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, WeakReference<IEventRaise>>> _events =
        new();

    public virtual void Subscribe(IEventRaise raise, string eventName)
    {
        var dictionary = _events.GetOrAdd(raise.RaiseUuid,
            _ => new ConcurrentDictionary<Guid, WeakReference<IEventRaise>>());
        dictionary.GetOrAdd(raise.ObjectUuid, _ => new WeakReference<IEventRaise>(raise));
    }

    public abstract void Unsubscribe(IEventRaise raise, string eventName);
}