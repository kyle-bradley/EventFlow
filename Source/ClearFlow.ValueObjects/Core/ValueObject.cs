using EventFlow.ValueObjects;

namespace ClearFlow.ValueObjects.Core;

public abstract class ValueObject<T>: ValueObject
        where T : class
{
    public ValueObject(T value) 
    { 
        Value = value;
    }

    public T Value { get; }

    public T ToDto()
    {
        return Value;
    }
}
