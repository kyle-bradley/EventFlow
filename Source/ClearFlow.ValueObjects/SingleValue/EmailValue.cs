using ClearFlow.ValueObjects.Core;
using EmailValidation;
using FluentValidation;

namespace ClearFlow.ValueObjects.SingleValue;

public class EmailValue : ValidatedSingleValueObject<string, EmailValue.Validator>
{
    public class Validator : ValueObjectValidator<string>
    {
        public override IRuleBuilderOptions<TProp, string> ApplyRules<TProp>(IRuleBuilder<TProp, string> builder) =>
            ApplyComplexRule(builder);

        public Validator() { ApplyRules(RuleFor(x => x)); }

        protected IRuleBuilderOptions<TProp, string> ApplyComplexRule<TProp>(IRuleBuilder<TProp, string> builder)
        {
            var error = EmailValidationError.None;
            return builder
                .Must(value => { return EmailValidator.TryValidate(value, false, false, out error); })
                    .WithMessage(value => $"Provide email address is not a valid. With code {error}");
        }
    }


    public EmailValue(string value) : base(value)
    { }
}
