using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace LTEK_ULed.Validators
{
    public sealed class NumberValidationAttribute() : ValidationAttribute(DefaultErrorMessage)
    {
        private static readonly string DefaultErrorMessage = "Please enter a valid number";

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value!.ToString() is not string number)
                return new ValidationResult(ErrorMessage);

            if (string.IsNullOrWhiteSpace(number))
                return new ValidationResult(ErrorMessage);

            float result = 0;

            try
            {
                if (!float.TryParse(number, out result))
                    return new ValidationResult(ErrorMessage);

            }
            catch
            {
                return new ValidationResult(ErrorMessage);

            }


            if (result%1!=0)
            {
                return new ValidationResult("The number must be an integer");
            }

            return ValidationResult.Success;
        }
    }
}
