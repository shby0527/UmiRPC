using System.Collections.Concurrent;
using DynamicProxy;

namespace Umi.Proxy.Dynamic.Dynamic;

public abstract class EventProcessorBase : IEventProcessor
{
    protected readonly IDictionary<Guid, WeakReference<IEventRaise>> _events;

    protected EventProcessorBase()
    {
        _events = new ConcurrentDictionary<Guid, WeakReference<IEventRaise>>();
    }

    public virtual void Subscribe(IEventRaise raise, string eventName)
    {
        _events.GetOrDefault(raise.RaiseUuid, () => new WeakReference<IEventRaise>(raise));
    }

    public abstract void Unsubscribe(IEventRaise raise, string eventName);
}