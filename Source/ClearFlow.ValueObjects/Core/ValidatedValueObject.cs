using FluentValidation;
using FluentValidation.Results;
using System;
using System.Collections.Generic;

namespace ClearFlow.ValueObjects.Core;

public abstract class ValidatedValueObject<TDto, TValidator> : ValueObject<TDto>
    where TDto : class
    where TValidator : AbstractValidator<TDto>, new()
{
    private static TValidator validator = new TValidator();
    public ValidatedValueObject(TDto value) : base(value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        validator.ValidateAndThrow(value);
    }

    public static IReadOnlyList<ValidationFailure> Validate(TDto value)
    {
        return validator.Validate(value).Errors;
    }

    public static IRuleBuilderOptions<TProp, TDto> Validate<TProp>(IRuleBuilder<TProp, TDto> builder)
    {
        return builder.SetValidator(validator);
    }
}
