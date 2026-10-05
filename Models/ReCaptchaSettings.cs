using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Models
{
    /// <summary>
    /// Configuration settings for Google reCAPTCHA
    /// </summary>
    public class ReCaptchaSettings
    {
        [Required]
        public string SiteKey { get; set; }
        
        [Required]
        public string SecretKey { get; set; }
        
        public string VerifyUrl { get; set; } = "https://www.google.com/recaptcha/api/siteverify";
        
        public double MinScore { get; set; } = 0.5;
        
        public bool IsEnabled { get; set; } = true;
    }
}
