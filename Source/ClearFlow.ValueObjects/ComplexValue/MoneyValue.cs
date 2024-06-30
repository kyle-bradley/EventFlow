using ClearFlow.ValueObjects.Core;
using ClearFlow.ValueObjects.Dtos;
using FluentValidation;
using NMoneys;

namespace ClearFlow.ValueObjects.ComplexValue;

public class MoneyValue : ValidatedValueObject<MoneyDto, MoneyValue.Validator>
{
    public class Validator : AbstractValidator<MoneyDto>
    {
        protected decimal MinMonetaryValue = 0;
        protected decimal MaxMonetaryValue = 1000000000000;

        public Validator()
        {
            RuleFor(x => x.Currency)
                .NotNull()
                .Must(currency => Currency.TryGet(currency, out Currency? itMightNotBe))
                    .WithMessage(currency => $"Curreny with provided ISO is not valid.");

            RuleFor(x => x.Amount)
                .NotNull()
                .Must(value => value >= MinMonetaryValue)
                    .WithMessage(amount => $"Negative amounts are not allowed.")
                .Must(value => value <= MaxMonetaryValue)
                    .WithMessage(amount => $"Exceeded max allowed value of {MaxMonetaryValue}.");
        }
    }

    private MoneyValue(MoneyDto value) : base(value)
    { }

    public static MoneyValue From(MoneyDto dto)
    {
        return new MoneyValue(dto);
    }
}
