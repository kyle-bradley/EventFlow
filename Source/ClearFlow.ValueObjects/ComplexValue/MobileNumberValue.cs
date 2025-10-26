using ClearFlow.ValueObjects.Core;
using ClearFlow.ValueObjects.Dtos;
using FluentValidation;
using PhoneNumbers;

namespace ClearFlow.ValueObjects.ComplexValue;

public class MobileNumberValue : ValidatedValueObject<MobileNumberDto, MobileNumberValue.Validator>
{
    protected static PhoneNumberUtil Util => PhoneNumberUtil.GetInstance();
    protected PhoneNumber MobileValue => Util.Parse(Value.MobileNumber, Value.CountryCode);
    public class Validator : AbstractValidator<MobileNumberDto>
    {
        protected int MinNationalNumberLength = 8;
        protected int MaxNationalNumberLength = 15;
        protected int CountryCodeLength = 2;
        

        public Validator()
        {
            RuleFor(x => x.CountryCode).NotNull().Length(CountryCodeLength);
            RuleFor(x => x.MobileNumber).NotNull().MinimumLength(MinNationalNumberLength).MaximumLength(MaxNationalNumberLength);

            RuleFor(m => Util.Parse(m.MobileNumber, m.CountryCode))
                .Must(m => m.HasCountryCode)
                .WithMessage(m => $"Country code {m.CountryCode} is not valid")
                .Must(m => m.HasNationalNumber)
                .WithMessage(m => $"National number {m.MobileNumber} is not valid")
                .Must(m => Util.IsValidNumber(m))
                .WithMessage(m => $"Full phone number {m.MobileNumber} is not valid");
        }
    }

    private MobileNumberValue(MobileNumberDto value) : base(value)
    { }

    public string ToNationalString()
    {
        var formattedPhoneNumberNational = Util.Format(MobileValue, PhoneNumberFormat.NATIONAL);
        return formattedPhoneNumberNational;
    }
    public override string ToString()
    {
        var formattedPhoneNumberNational = Util.Format(MobileValue, PhoneNumberFormat.INTERNATIONAL);
        return formattedPhoneNumberNational;
    }

    public bool Matches(MobileNumberValue value)
    {
        var otherValue = value.Value;
        var otherNumber = Util.Parse(otherValue.MobileNumber, otherValue.CountryCode);
        return this.MobileValue.Equals(otherNumber);
    }


    public static MobileNumberValue From(MobileNumberDto dto, bool format = false)
    {
        if (format)
        {
            var parsed = Util.Parse(dto.MobileNumber, dto.CountryCode);
            var formatted = Util.Format(parsed, PhoneNumberFormat.E164);
            return new MobileNumberValue(new MobileNumberDto(dto.CountryCode, formatted));
        }

        return new MobileNumberValue(dto);
    }
}