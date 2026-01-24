namespace DynamicProxy;

public interface IEventRaise
{
    public Guid RaiseUuid { get; }

    void RaiseEvent(string @event, EventArgs args);
}