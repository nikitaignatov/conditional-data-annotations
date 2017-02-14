namespace ConditionalValidator
{
    using System.ComponentModel.DataAnnotations;

    public interface IDisableValidation
    {
        bool IsDisabled(object value, ValidationContext validationContext, ValidationAttribute attribute);
    }
}
