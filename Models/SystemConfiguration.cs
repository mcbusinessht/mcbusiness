using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Models
{
    /// <summary>
    /// Configuration système dynamique pour éviter les paramètres hard-cod�s
    /// Permet la modification des paramètres métier sans redéploiement
    /// </summary>
    public class SystemConfiguration : BaseEntity
    {
        /// <summary>
        /// Clé unique de configuration (ex: MIN_WITHDRAWAL_AMOUNT)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ConfigKey { get; set; } = "";

        /// <summary>
        /// Valeur de la configuration
        /// </summary>
        [Required]
        public string ConfigValue { get; set; } = "";

        /// <summary>
        /// Type de la valeur (decimal, int, string, bool, json)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string ValueType { get; set; } = "string";

        /// <summary>
        /// Description de la configuration
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Catégorie de la configuration (Financial, System, Commerce, etc.)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = "";

        /// <summary>
        /// Configuration active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Valeur minimum pour les nombres (validation)
        /// </summary>
        public decimal? MinValue { get; set; }

        /// <summary>
        /// Valeur maximum pour les nombres (validation)
        /// </summary>
        public decimal? MaxValue { get; set; }

        /// <summary>
        /// Valeurs autorisées pour les enum (séparées par virgule)
        /// </summary>
        [MaxLength(500)]
        public string? AllowedValues { get; set; }

        /// <summary>
        /// Indique si un redémarrage est nécessaire après modification
        /// </summary>
        public bool RequiresRestart { get; set; } = false;

        /// <summary>
        /// Historique des modifications
        /// </summary>
        public virtual ICollection<SystemConfigurationHistory> History { get; set; } = new List<SystemConfigurationHistory>();
    }

    /// <summary>
    /// Historique des modifications de configuration pour audit trail
    /// </summary>
    public class SystemConfigurationHistory : BaseEntity
    {
        /// <summary>
        /// Configuration modifiée
        /// </summary>
        public int ConfigurationId { get; set; }

        /// <summary>
        /// Ancienne valeur
        /// </summary>
        public string? OldValue { get; set; }

        /// <summary>
        /// Nouvelle valeur
        /// </summary>
        public string? NewValue { get; set; }

        /// <summary>
        /// Raison du changement
        /// </summary>
        [MaxLength(500)]
        public string? Reason { get; set; }

        /// <summary>
        /// Configuration parent
        /// </summary>
        public virtual SystemConfiguration Configuration { get; set; } = null!;
    }
}
