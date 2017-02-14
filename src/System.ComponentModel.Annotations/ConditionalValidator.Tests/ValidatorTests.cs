using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ConditionalValidator.Tests
{
    public class ValidatorTests
    {
        class Person
        {
            public Person(string name, string title)
            {
                Name = name;
                Title = title;
            }

            [Required]
            public string Name { get; }

            [Required]
            public string Title { get; }
        }

        private static readonly Person invalid = new Person("Valid Name", null);
        private static readonly Person valid = new Person("Valid Name", "Valid Title");
        private static bool Validate(Person input) => Validator.TryValidateObject(input, new ValidationContext(input), null, true);

        [Fact]
        public static void try_validate_object_should_return_false_result_when_disable_service_is_missing_and_input_is_invalid()
            => Validate(invalid).Should().BeFalse();

        [Fact]
        public static void try_validate_object_should_return_true_result_when_disable_service_is_missing_and_input_is_valid()
            => Validate(valid).Should().BeTrue();

        [Theory()]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public static void try_validate_object_should_return_expected_result_when_disable_service_is_provided(bool isDisabled, bool expected)
        {
            // arrange
            var disable = Substitute.For<IDisableValidation>();
            disable.IsDisabled(Arg.Any<object>(), Arg.Any<ValidationContext>(), Arg.Any<ValidationAttribute>()).Returns(isDisabled);

            var ctx = new ValidationContext(invalid);
            ctx.ServiceContainer.AddService(typeof(IDisableValidation), disable);

            // act
            var result = Validator.TryValidateObject(invalid, ctx, null, true);

            // assert
            result.Should().Be(expected);
        }
    }
}
