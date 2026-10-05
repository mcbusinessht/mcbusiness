using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Models
{
    /// <summary>
    /// Entité de base avec versioning et audit trail pour toutes les tables principales
    /// Implémente les améliorations recommandées pour la traçabilité et la concurrence optimiste
    /// </summary>
    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Version pour la concurrence optimiste - évite les conflits de mise à jour
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// ID de l'utilisateur qui a créé l'entité
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// ID de l'utilisateur qui a modifié l'entité en dernier
        /// </summary>
        public string? ModifiedBy { get; set; }

        /// <summary>
        /// Date de création de l'entité
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Date de dernière modification
        /// </summary>
        public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Suppression logique - évite la perte de données
        /// </summary>
        public bool IsDeleted { get; set; } = false;
    }

    /// <summary>
    /// Entité de base avec ID string (pour ApplicationUser)
    /// </summary>
    public abstract class BaseEntityWithStringId
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Version pour la concurrence optimiste
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// ID de l'utilisateur qui a créé l'entité
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// ID de l'utilisateur qui a modifié l'entité en dernier
        /// </summary>
        public string? ModifiedBy { get; set; }

        /// <summary>
        /// Date de création de l'entité
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Date de dernière modification
        /// </summary>
        public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Suppression logique - évite la perte de données
        /// </summary>
        public bool IsDeleted { get; set; } = false;
    }
}
