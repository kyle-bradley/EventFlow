using FluentValidation;

namespace ClearFlow.ValueObjects.Core;

public abstract class ValueObjectValidator<TValue> : AbstractValidator<TValue>
{
    public virtual IRuleBuilderOptions<TProp, TValue> ApplyRules<TProp>(IRuleBuilder<TProp, TValue> builder) => builder.NotNull();
}
