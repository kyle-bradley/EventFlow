using ClearFlow.ValueObjects.Core;
using FluentValidation;
using System.Linq;

namespace ClearFlow.ValueObjects.SingleValue;

public class PersonNameValue : ValidatedSingleValueObject<string, PersonNameValue.Validator>
{
    public class Validator : ValueObjectValidator<string>
    {
        private int MinNameLength = 2;
        private const int MaxNameLength = 60;
        private const string IllegalCharacterString = "0123456789@#$%!^&*()_+=~><?/.,\";:][{}|\\/";
        private static readonly char[] IllegalCharacters = IllegalCharacterString.ToCharArray();
        private const char IllegalStartOrEndCharacter = '-';
        private const int MaxNumberOfNames = 5;

        public override IRuleBuilderOptions<TProp, string> ApplyRules<TProp>(IRuleBuilder<TProp, string> builder) => builder
            .NotNull()
            .NotEmpty()
            .MinimumLength(MinNameLength)
            .MaximumLength(MaxNameLength)
            .Must(name => name.IndexOfAny(IllegalCharacters) == -1)
                .WithMessage($"A person's name cannot contain the following characters {string.Join(",", IllegalCharacters)}")
            .Must(name => !NameStartsOrEndsWithHyphen(name))
                .WithMessage($"A person's name cannot start or end with a {IllegalStartOrEndCharacter}")
            .Must(name => !ContainsToManyNames(name))
                .WithMessage($"A person's name cannot have more than {MaxNumberOfNames} names");

        public Validator() { ApplyRules(RuleFor(x => x)); }

        private static bool NameStartsOrEndsWithHyphen(string name)
        {
            var firstCharacter = name.FirstOrDefault();
            var lastCharacter = name.LastOrDefault();
            var invalidStartAndEndCharacters = firstCharacter == IllegalStartOrEndCharacter || lastCharacter == IllegalStartOrEndCharacter;
            return invalidStartAndEndCharacters;
        }

        private static bool ContainsToManyNames(string name)
        {
            var adjustForNoSpacedName = 1;
            var numberOfSpaces = name.Count(space => space == ' ') + adjustForNoSpacedName;
            var namesMoreThanMax = numberOfSpaces > MaxNumberOfNames;
            return namesMoreThanMax;
        }
    }

    public PersonNameValue(string value) : base(value)
    {
    }
}
