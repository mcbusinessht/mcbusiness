using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class EmployeePerformance
    {
        [Key]
        public int Id { get; set; }

        [Required, ForeignKey("Employee")]
        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        [Required, ForeignKey("EvaluatedBy")]
        public string EvaluatedById { get; set; }
        public virtual ApplicationUser EvaluatedBy { get; set; }

        [Required]
        [Range(0, 5)]
        public decimal Score { get; set; } // Note sur 5

        public string Period { get; set; } // ex: "2024-01", "Q1-2024"

        public int TasksCompleted { get; set; } = 0;
        public int TasksAssigned { get; set; } = 0;

        [Range(0, 100)]
        public decimal AttendanceRate { get; set; } = 100; // Pourcentage

        public string Comments { get; set; }

        public DateTimeOffset EvaluationDate { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public bool IsDeleted { get; set; } = false;
        public DateTimeOffset? UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class EmployeeAttendance
    {
        [Key]
        public int Id { get; set; }

        [Required, ForeignKey("Employee")]
        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        [Required]
        public DateTimeOffset Date { get; set; }

        public DateTimeOffset? CheckInTime { get; set; }
        public DateTimeOffset? CheckOutTime { get; set; }

        public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

        public string Notes { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public enum AttendanceStatus
    {
        Present,
        Absent,
        Late,
        Sick,
        Vacation,
        Holiday
    }
}
