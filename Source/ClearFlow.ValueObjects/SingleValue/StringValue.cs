using ClearFlow.ValueObjects.Core;
using FluentValidation;

namespace ClearFlow.ValueObjects.SingleValue;

public class StringValue : ValidatedSingleValueObject<string, StringValue.Validator>
{
    public class Validator : ValueObjectValidator<string>
    {
        public override IRuleBuilderOptions<TProp, string> ApplyRules<TProp>(IRuleBuilder<TProp, string> builder) => builder
            .NotNull()
            .NotEmpty();

        public Validator() { ApplyRules(RuleFor(x => x)); }
    }

    private StringValue(string value)
            : base(value)
    { }

    public static StringValue From(string value)
    {
        return new StringValue(value);
    }
}
