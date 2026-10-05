using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MCBusiness.Attributes
{
    public class MaxFileSizeAttribute : ValidationAttribute
    {
        private readonly int _maxFileSizeInBytes;

        public MaxFileSizeAttribute(int maxFileSizeInBytes)
        {
            _maxFileSizeInBytes = maxFileSizeInBytes;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var file = value as IFormFile;
            if (file != null)
            {
                if (file.Length > _maxFileSizeInBytes)
                {
                    return new ValidationResult($"La taille du fichier ne doit pas dépasser {_maxFileSizeInBytes / 1024 / 1024} Mo.");
                }
            }
            return ValidationResult.Success;
        }
    }
}
