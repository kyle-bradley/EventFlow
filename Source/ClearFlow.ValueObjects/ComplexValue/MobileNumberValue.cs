using ClearFlow.ValueObjects.Core;
using ClearFlow.ValueObjects.Dtos;
using FluentValidation;
using PhoneNumbers;

namespace ClearFlow.ValueObjects.ComplexValue;

public class MobileNumberValue : ValidatedValueObject<MobileNumberDto, MobileNumberValue.Validator>
{
    public class Validator : AbstractValidator<MobileNumberDto>
    {
        protected int MinNationalNumberLength = 8;
        protected int MaxNationalNumberLength = 15;
        protected int CountryCodeLength = 2;

        public Validator()
        {
            RuleFor(x => x.CountryCode).NotNull().Length(CountryCodeLength);
            RuleFor(x => x.MobileNumber).NotNull().MinimumLength(MinNationalNumberLength).MaximumLength(MaxNationalNumberLength);

            RuleFor(m => PhoneNumberUtil.GetInstance().Parse(m.MobileNumber, m.CountryCode))
                .Must(m => m.HasCountryCode)
                .WithMessage(m => $"Country code {m.CountryCode} is not valid")
                .Must(m => m.HasNationalNumber)
                .WithMessage(m => $"National number {m.MobileNumber} is not valid")
                .Must(m => PhoneNumberUtil.GetInstance().IsValidNumber(m))
                .WithMessage(m => $"Full phone number {m.MobileNumber} is not valid");
        }
    }

    private MobileNumberValue(MobileNumberDto value) : base(value)
    { }

    public static MobileNumberValue From(MobileNumberDto dto)
    {
        return new MobileNumberValue(dto);
    }
}