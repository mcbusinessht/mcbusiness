using System.Security.Claims;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    /// <summary>
    /// Service centralisé pour la gestion de l'audit et du versioning
    /// Assure le respect des normes BaseEntity sur toutes les opérations
    /// </summary>
    public interface IAuditService
    {
        void SetAuditFields<T>(T entity, string? userId = null) where T : BaseEntity;
        void SetAuditFieldsForUpdate<T>(T entity, string? userId = null) where T : BaseEntity;
        string? GetCurrentUserId(ClaimsPrincipal user);
        void ValidateVersioning<T>(T entity, int expectedVersion) where T : BaseEntity;
    }

    public class AuditService : IAuditService
    {
        /// <summary>
        /// Configure les champs d'audit pour une nouvelle entité
        /// </summary>
        public void SetAuditFields<T>(T entity, string? userId = null) where T : BaseEntity
        {
            var now = DateTimeOffset.UtcNow;
            
            entity.Version = 1;
            entity.CreatedBy = userId;
            entity.ModifiedBy = userId;
            entity.CreatedAt = now;
            entity.ModifiedAt = now;
            entity.IsDeleted = false;
        }

        /// <summary>
        /// Configure les champs d'audit pour une mise à jour d'entité
        /// </summary>
        public void SetAuditFieldsForUpdate<T>(T entity, string? userId = null) where T : BaseEntity
        {
            entity.Version++; // Incrémenter la version pour concurrence optimiste
            entity.ModifiedBy = userId;
            entity.ModifiedAt = DateTimeOffset.UtcNow;
            // Ne pas modifier CreatedBy et CreatedAt
        }

        /// <summary>
        /// Récupère l'ID de l'utilisateur connecté
        /// </summary>
        public string? GetCurrentUserId(ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        /// <summary>
        /// Valide le versioning pour éviter les conflits de concurrence
        /// </summary>
        public void ValidateVersioning<T>(T entity, int expectedVersion) where T : BaseEntity
        {
            if (entity.Version != expectedVersion)
            {
                throw new InvalidOperationException(
                    $"Conflit de concurrence détecté. Version attendue: {expectedVersion}, Version actuelle: {entity.Version}");
            }
        }
    }

    /// <summary>
    /// Extension methods pour faciliter l'utilisation de l'audit
    /// </summary>
    public static class AuditServiceExtensions
    {
        /// <summary>
        /// Configure automatiquement l'audit pour une nouvelle entité
        /// </summary>
        public static T WithAudit<T>(this T entity, IAuditService auditService, ClaimsPrincipal user) where T : BaseEntity
        {
            var userId = auditService.GetCurrentUserId(user);
            auditService.SetAuditFields(entity, userId);
            return entity;
        }

        /// <summary>
        /// Configure automatiquement l'audit pour une mise à jour d'entité
        /// </summary>
        public static T WithUpdateAudit<T>(this T entity, IAuditService auditService, ClaimsPrincipal user) where T : BaseEntity
        {
            var userId = auditService.GetCurrentUserId(user);
            auditService.SetAuditFieldsForUpdate(entity, userId);
            return entity;
        }
    }
}
