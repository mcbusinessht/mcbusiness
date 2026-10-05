using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class EmployeeTask
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required, ForeignKey("Employee")]
        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        [Required, ForeignKey("AssignedBy")]
        public string AssignedById { get; set; }
        public virtual ApplicationUser AssignedBy { get; set; }

        public TaskStatus Status { get; set; } = TaskStatus.Pending;
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        public DateTimeOffset DueDate { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? CompletedAt { get; set; }

        [Range(1, 10)]
        public int? DifficultyLevel { get; set; }

        public string Category { get; set; } // Projets, Support, Réunions, Formation, etc.

        public bool IsDeleted { get; set; } = false;
        public DateTimeOffset? UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public enum TaskStatus
    {
        Pending,
        InProgress,
        Completed,
        Cancelled,
        OnHold
    }

    public enum TaskPriority
    {
        Low,
        Medium,
        High,
        Urgent
    }
}
