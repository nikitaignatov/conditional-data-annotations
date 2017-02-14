namespace ConditionalValidator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.ComponentModel.DataAnnotations;
    using System.Globalization;
    using System.ComponentModel;

    public static class Validator
    {
        private static ValidationAttributeStore _store = ValidationAttributeStore.Instance;

        public static bool TryValidateProperty(object value, ValidationContext validationContext, ICollection<ValidationResult> validationResults)
        {
            // Throw if value cannot be assigned to this property.  That is not a validation exception.
            Type propertyType = _store.GetPropertyType(validationContext);
            string propertyName = validationContext.MemberName;
            EnsureValidPropertyType(propertyName, propertyType, value);

            bool result = true;
            bool breakOnFirstError = (validationResults == null);

            IEnumerable<ValidationAttribute> attributes = _store.GetPropertyValidationAttributes(validationContext);

            foreach (ValidationError err in GetValidationErrors(value, validationContext, attributes, breakOnFirstError))
            {
                result = false;

                if (validationResults != null)
                {
                    validationResults.Add(err.ValidationResult);
                }
            }

            return result;
        }

        public static bool TryValidateObject(object instance, ValidationContext validationContext, ICollection<ValidationResult> validationResults)
        {
            return TryValidateObject(instance, validationContext, validationResults, false /*validateAllProperties*/);
        }

        public static bool TryValidateObject(object instance, ValidationContext validationContext, ICollection<ValidationResult> validationResults, bool validateAllProperties)
        {
            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }

            if (validationContext != null && instance != validationContext.ObjectInstance)
            {
                throw new ArgumentException(DataAnnotationsResources.Validator_InstanceMustMatchValidationContextInstance, "instance");
            }

            bool result = true;
            bool breakOnFirstError = (validationResults == null);

            foreach (ValidationError err in GetObjectValidationErrors(instance, validationContext, validateAllProperties, breakOnFirstError))
            {
                result = false;

                if (validationResults != null)
                {
                    validationResults.Add(err.ValidationResult);
                }
            }

            return result;
        }

        public static bool TryValidateValue(object value, ValidationContext validationContext, ICollection<ValidationResult> validationResults, IEnumerable<ValidationAttribute> validationAttributes)
        {
            bool result = true;
            bool breakOnFirstError = validationResults == null;

            foreach (ValidationError err in GetValidationErrors(value, validationContext, validationAttributes, breakOnFirstError))
            {
                result = false;

                if (validationResults != null)
                {
                    validationResults.Add(err.ValidationResult);
                }
            }

            return result;
        }

        public static void ValidateProperty(object value, ValidationContext validationContext)
        {
            // Throw if value cannot be assigned to this property.  That is not a validation exception.
            Type propertyType = _store.GetPropertyType(validationContext);
            EnsureValidPropertyType(validationContext.MemberName, propertyType, value);

            IEnumerable<ValidationAttribute> attributes = _store.GetPropertyValidationAttributes(validationContext);

            ValidationError err = GetValidationErrors(value, validationContext, attributes, false).FirstOrDefault();
            if (err != null)
            {
                err.ThrowValidationException();
            }
        }

        public static void ValidateObject(object instance, ValidationContext validationContext)
        {
            ValidateObject(instance, validationContext, false /*validateAllProperties*/);
        }

        public static void ValidateObject(object instance, ValidationContext validationContext, bool validateAllProperties)
        {
            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }
            if (validationContext == null)
            {
                throw new ArgumentNullException("validationContext");
            }
            if (instance != validationContext.ObjectInstance)
            {
                throw new ArgumentException(DataAnnotationsResources.Validator_InstanceMustMatchValidationContextInstance, "instance");
            }

            ValidationError err = GetObjectValidationErrors(instance, validationContext, validateAllProperties, false).FirstOrDefault();
            if (err != null)
            {
                err.ThrowValidationException();
            }
        }

        public static void ValidateValue(object value, ValidationContext validationContext, IEnumerable<ValidationAttribute> validationAttributes)
        {
            if (validationContext == null)
            {
                throw new ArgumentNullException("validationContext");
            }

            ValidationError err = GetValidationErrors(value, validationContext, validationAttributes, false).FirstOrDefault();
            if (err != null)
            {
                err.ThrowValidationException();
            }
        }

        internal static ValidationContext CreateValidationContext(object instance, ValidationContext validationContext)
        {
            if (validationContext == null)
            {
                throw new ArgumentNullException("validationContext");
            }

            // Create a new context using the existing ValidationContext that acts as an IServiceProvider and contains our existing items.
            ValidationContext context = new ValidationContext(instance, validationContext, validationContext.Items);
            return context;
        }

        private static bool CanBeAssigned(Type destinationType, object value)
        {
            if (destinationType == null)
            {
                throw new ArgumentNullException("destinationType");
            }

            if (value == null)
            {
                // Null can be assigned only to reference types or Nullable or Nullable<>
                return !destinationType.IsValueType ||
                        (destinationType.IsGenericType && destinationType.GetGenericTypeDefinition() == typeof(Nullable<>));
            }

            // Not null -- be sure it can be cast to the right type
            return destinationType.IsAssignableFrom(value.GetType());
        }

        private static void EnsureValidPropertyType(string propertyName, Type propertyType, object value)
        {
            if (!CanBeAssigned(propertyType, value))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, DataAnnotationsResources.Validator_Property_Value_Wrong_Type, propertyName, propertyType), "value");
            }
        }

        private static IEnumerable<ValidationError> GetObjectValidationErrors(object instance, ValidationContext validationContext, bool validateAllProperties, bool breakOnFirstError)
        {
            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }

            if (validationContext == null)
            {
                throw new ArgumentNullException("validationContext");
            }

            // Step 1: Validate the object properties' validation attributes
            List<ValidationError> errors = new List<ValidationError>();
            errors.AddRange(GetObjectPropertyValidationErrors(instance, validationContext, validateAllProperties, breakOnFirstError));

            // We only proceed to Step 2 if there are no errors
            if (errors.Any())
            {
                return errors;
            }

            // Step 2: Validate the object's validation attributes
            IEnumerable<ValidationAttribute> attributes = _store.GetTypeValidationAttributes(validationContext);
            errors.AddRange(GetValidationErrors(instance, validationContext, attributes, breakOnFirstError));

            return errors;
        }

        private static IEnumerable<ValidationError> GetObjectPropertyValidationErrors(object instance, ValidationContext validationContext, bool validateAllProperties, bool breakOnFirstError)
        {
            ICollection<KeyValuePair<ValidationContext, object>> properties = GetPropertyValues(instance, validationContext);
            List<ValidationError> errors = new List<ValidationError>();

            foreach (KeyValuePair<ValidationContext, object> property in properties)
            {
                // get list of all validation attributes for this property
                IEnumerable<ValidationAttribute> attributes = _store.GetPropertyValidationAttributes(property.Key);

                if (validateAllProperties)
                {
                    // validate all validation attributes on this property
                    errors.AddRange(GetValidationErrors(property.Value, property.Key, attributes, breakOnFirstError));
                }
                else
                {
                    // only validate the Required attributes
                    RequiredAttribute reqAttr = attributes.FirstOrDefault(a => a is RequiredAttribute) as RequiredAttribute;
                    if (reqAttr != null)
                    {
                        // Note: we let the [Required] attribute do its own null testing,
                        // since the user may have subclassed it and have a deeper meaning to what 'required' means
                        ValidationResult validationResult = reqAttr.GetValidationResult(property.Value, property.Key);
                        if (validationResult != ValidationResult.Success)
                        {
                            errors.Add(new ValidationError(reqAttr, property.Value, validationResult));
                        }
                    }
                }

                if (breakOnFirstError && errors.Any())
                {
                    break;
                }
            }

            return errors;
        }

        private static ICollection<KeyValuePair<ValidationContext, object>> GetPropertyValues(object instance, ValidationContext validationContext)
        {

            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(instance);
            List<KeyValuePair<ValidationContext, object>> items = new List<KeyValuePair<ValidationContext, object>>(properties.Count);
            foreach (PropertyDescriptor property in properties)
            {
                ValidationContext context = CreateValidationContext(instance, validationContext);
                context.MemberName = property.Name;

                if (_store.GetPropertyValidationAttributes(context).Any())
                {
                    items.Add(new KeyValuePair<ValidationContext, object>(context, property.GetValue(instance)));
                }
            }
            return items;
        }

        private static IEnumerable<ValidationError> GetValidationErrors(object value, ValidationContext validationContext, IEnumerable<ValidationAttribute> attributes, bool breakOnFirstError)
        {
            if (validationContext == null)
            {
                throw new ArgumentNullException("validationContext");
            }

            List<ValidationError> errors = new List<ValidationError>();
            ValidationError validationError;

            // Get the required validator if there is one and test it first, aborting on failure
            RequiredAttribute required = attributes.FirstOrDefault(a => a is RequiredAttribute) as RequiredAttribute;
            if (required != null)
            {
                if (!TryValidate(value, validationContext, required, out validationError))
                {
                    errors.Add(validationError);
                    return errors;
                }
            }

            // Iterate through the rest of the validators, skipping the required validator
            foreach (ValidationAttribute attr in attributes)
            {
                if (attr != required)
                {
                    if (!TryValidate(value, validationContext, attr, out validationError))
                    {
                        errors.Add(validationError);

                        if (breakOnFirstError)
                        {
                            break;
                        }
                    }
                }
            }

            return errors;
        }

        private static bool TryValidate(object value, ValidationContext validationContext, ValidationAttribute attribute, out ValidationError validationError)
        {
            if (validationContext == null)
            {
                throw new ArgumentNullException("validationContext");
            }

            var disable = (IDisableValidation)validationContext.GetService(typeof(IDisableValidation));
            if (disable != null && disable.IsDisabled(value, validationContext, attribute))
            {
                validationError = null;
                return true;
            }

            ValidationResult validationResult = attribute.GetValidationResult(value, validationContext);
            if (validationResult != ValidationResult.Success)
            {
                validationError = new ValidationError(attribute, value, validationResult);
                return false;
            }

            validationError = null;
            return true;
        }

        private class ValidationError
        {
            internal ValidationError(ValidationAttribute validationAttribute, object value, ValidationResult validationResult)
            {
                this.ValidationAttribute = validationAttribute;
                this.ValidationResult = validationResult;
                this.Value = value;
            }

            internal object Value { get; set; }

            internal ValidationAttribute ValidationAttribute { get; set; }

            internal ValidationResult ValidationResult { get; set; }

            internal void ThrowValidationException()
            {
                throw new ValidationException(this.ValidationResult, this.ValidationAttribute, this.Value);
            }
        }
    }
}
