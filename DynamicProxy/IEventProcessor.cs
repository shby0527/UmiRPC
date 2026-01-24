namespace DynamicProxy;

public interface IEventProcessor
{
    void Subscribe(IEventRaise raise, string eventName);

    void Unsubscribe(IEventRaise raise, string eventName);
}