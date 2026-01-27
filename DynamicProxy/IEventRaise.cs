namespace DynamicProxy;

public interface IEventRaise
{
    public Guid RaiseUuid { get; }
    
    public Guid ObjectUuid { get; }

    void RaiseEvent(string @event, EventArgs args);
}