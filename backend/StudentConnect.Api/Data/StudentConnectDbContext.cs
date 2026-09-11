using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Data;

// Represents our session with the database — one DbSet per table we work with.
public class StudentConnectDbContext(DbContextOptions<StudentConnectDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<StudentSubject> StudentSubjects => Set<StudentSubject>();
    public DbSet<Attendance> Attendance => Set<Attendance>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Mark> Marks => Set<Mark>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Homework> Homework => Set<Homework>();
    public DbSet<HomeworkSubmission> HomeworkSubmissions => Set<HomeworkSubmission>();
    public DbSet<LearningMaterial> LearningMaterials => Set<LearningMaterial>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityRegistration> ActivityRegistrations => Set<ActivityRegistration>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<ParentStudent> ParentStudents => Set<ParentStudent>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>()
            .HasIndex(s => s.StudentId)
            .IsUnique();

        modelBuilder.Entity<Student>()
            .HasOne(s => s.Grade)
            .WithMany(g => g.Students)
            .HasForeignKey(s => s.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StudentSubject>()
            .HasIndex(ss => new { ss.StudentId, ss.SubjectId })
            .IsUnique();

        modelBuilder.Entity<StudentSubject>()
            .HasOne(ss => ss.Student)
            .WithMany()
            .HasForeignKey(ss => ss.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StudentSubject>()
            .HasOne(ss => ss.Subject)
            .WithMany(s => s.StudentSubjects)
            .HasForeignKey(ss => ss.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // Business rule: only one attendance record per student+subject+date.
        modelBuilder.Entity<Attendance>()
            .HasIndex(a => new { a.StudentId, a.SubjectId, a.Date })
            .IsUnique();

        modelBuilder.Entity<Attendance>()
            .Property(a => a.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Attendance>()
            .HasOne(a => a.Student)
            .WithMany()
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Attendance>()
            .HasOne(a => a.Subject)
            .WithMany()
            .HasForeignKey(a => a.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Assessment>()
            .Property(a => a.AssessmentType)
            .HasConversion<string>();

        modelBuilder.Entity<Assessment>()
            .HasOne(a => a.Subject)
            .WithMany()
            .HasForeignKey(a => a.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Assessment>()
            .ToTable(t => t.HasCheckConstraint("CK_Assessment_MaximumMarks", "\"MaximumMarks\" > 0"));

        // Business rule: only one mark per student+assessment.
        modelBuilder.Entity<Mark>()
            .HasIndex(m => new { m.StudentId, m.AssessmentId })
            .IsUnique();

        modelBuilder.Entity<Mark>()
            .HasOne(m => m.Student)
            .WithMany()
            .HasForeignKey(m => m.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Mark>()
            .HasOne(m => m.Assessment)
            .WithMany()
            .HasForeignKey(m => m.AssessmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Mark>()
            .ToTable(t => t.HasCheckConstraint("CK_Mark_MarksObtained", "\"MarksObtained\" >= 0"));

        modelBuilder.Entity<Homework>()
            .HasOne(h => h.Subject)
            .WithMany()
            .HasForeignKey(h => h.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Homework>()
            .HasOne(h => h.Grade)
            .WithMany()
            .HasForeignKey(h => h.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Homework>()
            .HasOne(h => h.Teacher)
            .WithMany()
            .HasForeignKey(h => h.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        // Business rule: only one submission per student+homework.
        modelBuilder.Entity<HomeworkSubmission>()
            .HasIndex(hs => new { hs.StudentId, hs.HomeworkId })
            .IsUnique();

        // Deleting homework removes its submissions too — a submission has no meaning
        // once the assignment it answers no longer exists (chosen over blocking deletion).
        modelBuilder.Entity<HomeworkSubmission>()
            .HasOne(hs => hs.Homework)
            .WithMany()
            .HasForeignKey(hs => hs.HomeworkId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HomeworkSubmission>()
            .HasOne(hs => hs.Student)
            .WithMany()
            .HasForeignKey(hs => hs.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HomeworkSubmission>()
            .Property(hs => hs.Status)
            .HasConversion<string>();

        modelBuilder.Entity<LearningMaterial>()
            .HasIndex(m => m.StoredFileName)
            .IsUnique();

        modelBuilder.Entity<LearningMaterial>()
            .HasOne(m => m.Subject)
            .WithMany()
            .HasForeignKey(m => m.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LearningMaterial>()
            .HasOne(m => m.Grade)
            .WithMany()
            .HasForeignKey(m => m.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LearningMaterial>()
            .HasOne(m => m.Teacher)
            .WithMany()
            .HasForeignKey(m => m.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Activity>()
            .HasOne(a => a.Grade)
            .WithMany()
            .HasForeignKey(a => a.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Activity>()
            .ToTable(t => t.HasCheckConstraint("CK_Activity_Capacity", "\"Capacity\" > 0"));

        // Business rule: one registration row per student+activity — re-registering after
        // cancellation flips Status back to Registered instead of inserting a duplicate row.
        modelBuilder.Entity<ActivityRegistration>()
            .HasIndex(r => new { r.StudentId, r.ActivityId })
            .IsUnique();

        modelBuilder.Entity<ActivityRegistration>()
            .HasOne(r => r.Student)
            .WithMany()
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityRegistration>()
            .HasOne(r => r.Activity)
            .WithMany()
            .HasForeignKey(r => r.ActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityRegistration>()
            .Property(r => r.Status)
            .HasConversion<string>();

        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.Role)
            .HasConversion<string>();

        modelBuilder.Entity<ApplicationUser>()
            .HasOne(u => u.Student)
            .WithMany()
            .HasForeignKey(u => u.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ApplicationUser>()
            .HasOne(u => u.Teacher)
            .WithMany()
            .HasForeignKey(u => u.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        // Business rule: a parent can only be linked to the same child once.
        modelBuilder.Entity<ParentStudent>()
            .HasIndex(ps => new { ps.ParentUserId, ps.StudentId })
            .IsUnique();

        modelBuilder.Entity<ParentStudent>()
            .HasOne(ps => ps.ParentUser)
            .WithMany()
            .HasForeignKey(ps => ps.ParentUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ParentStudent>()
            .HasOne(ps => ps.Student)
            .WithMany()
            .HasForeignKey(ps => ps.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead });

        modelBuilder.Entity<Notification>()
            .Property(n => n.Type)
            .HasConversion<string>();

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Grade>().HasData(
            new Grade { Id = 1, Name = "Grade 8" },
            new Grade { Id = 2, Name = "Grade 9" },
            new Grade { Id = 3, Name = "Grade 10" }
        );

        modelBuilder.Entity<Student>().HasData(
            new Student { Id = 1, StudentId = "STU001", FirstName = "Ananya", LastName = "Sharma", Email = "ananya@example.com", GradeId = 1, Section = "A" },
            new Student { Id = 2, StudentId = "STU002", FirstName = "Rahul", LastName = "Verma", Email = "rahul@example.com", GradeId = 1, Section = "A" },
            new Student { Id = 3, StudentId = "STU003", FirstName = "Aisha", LastName = "Khan", Email = "aisha@example.com", GradeId = 1, Section = "B" },
            new Student { Id = 4, StudentId = "STU004", FirstName = "Arjun", LastName = "Nair", Email = "arjun@example.com", GradeId = 1, Section = "B" },
            new Student { Id = 5, StudentId = "STU005", FirstName = "Priya", LastName = "Iyer", Email = "priya@example.com", GradeId = 2, Section = "A" },
            new Student { Id = 6, StudentId = "STU006", FirstName = "Vikram", LastName = "Rao", Email = "vikram@example.com", GradeId = 2, Section = "A" },
            new Student { Id = 7, StudentId = "STU007", FirstName = "Meera", LastName = "Pillai", Email = "meera@example.com", GradeId = 2, Section = "B" },
            new Student { Id = 8, StudentId = "STU008", FirstName = "Karan", LastName = "Singh", Email = "karan@example.com", GradeId = 3, Section = "A" },
            new Student { Id = 9, StudentId = "STU009", FirstName = "Divya", LastName = "Menon", Email = "divya@example.com", GradeId = 3, Section = "A" },
            new Student { Id = 10, StudentId = "STU010", FirstName = "Rohan", LastName = "Gupta", Email = "rohan@example.com", GradeId = 3, Section = "B" }
        );

        modelBuilder.Entity<Subject>().HasData(
            new Subject { Id = 1, Name = "Mathematics", Code = "MATH" },
            new Subject { Id = 2, Name = "Science", Code = "SCI" },
            new Subject { Id = 3, Name = "English", Code = "ENG" },
            new Subject { Id = 4, Name = "Computer Science", Code = "CS" }
        );

        // Every student studies all four subjects for now — enough to demonstrate the relationship.
        var studentSubjectSeed = new List<StudentSubject>();
        var studentSubjectId = 1;
        for (var studentId = 1; studentId <= 10; studentId++)
        {
            for (var subjectId = 1; subjectId <= 4; subjectId++)
            {
                studentSubjectSeed.Add(new StudentSubject { Id = studentSubjectId++, StudentId = studentId, SubjectId = subjectId });
            }
        }
        modelBuilder.Entity<StudentSubject>().HasData(studentSubjectSeed);

        // A small, deterministic set of demo attendance records — enough to show a real percentage, not hundreds of rows.
        var attendanceSeed = new List<Attendance>();
        var attendanceId = 1;
        var startDate = new DateOnly(2026, 9, 1);

        // Mathematics attendance for the whole of Grade 8A (students 1-4).
        foreach (var studentId in new[] { 1, 2, 3, 4 })
        {
            for (var day = 0; day < 10; day++)
            {
                var status = (day + studentId) % 6 == 0 ? AttendanceStatus.Absent : AttendanceStatus.Present;
                attendanceSeed.Add(new Attendance { Id = attendanceId++, StudentId = studentId, SubjectId = 1, Date = startDate.AddDays(day), Status = status });
            }
        }

        // Ananya (student 1) also has Science/English/Computer Science attendance, matching the "My Attendance" example.
        foreach (var subjectId in new[] { 2, 3, 4 })
        {
            for (var day = 0; day < 10; day++)
            {
                var status = (day + subjectId) % 7 == 0 ? AttendanceStatus.Absent : AttendanceStatus.Present;
                attendanceSeed.Add(new Attendance { Id = attendanceId++, StudentId = 1, SubjectId = subjectId, Date = startDate.AddDays(day), Status = status });
            }
        }

        modelBuilder.Entity<Attendance>().HasData(attendanceSeed);

        modelBuilder.Entity<Assessment>().HasData(
            new Assessment { Id = 1, Title = "Unit Test 1", SubjectId = 1, Date = new DateOnly(2026, 9, 15), MaximumMarks = 100, AssessmentType = AssessmentType.UnitTest, Description = "Algebra basics" },
            new Assessment { Id = 2, Title = "Unit Test 2", SubjectId = 1, Date = new DateOnly(2026, 9, 22), MaximumMarks = 100, AssessmentType = AssessmentType.UnitTest, Description = "Geometry basics" },
            new Assessment { Id = 3, Title = "Unit Test 1", SubjectId = 2, Date = new DateOnly(2026, 9, 18), MaximumMarks = 100, AssessmentType = AssessmentType.UnitTest, Description = "Physical and chemical changes" },
            new Assessment { Id = 4, Title = "Grammar Test 1", SubjectId = 3, Date = new DateOnly(2026, 9, 20), MaximumMarks = 50, AssessmentType = AssessmentType.Quiz, Description = "Tenses and parts of speech" }
        );

        // A small demo set of marks for Grade 8A (students 1-4) — matches the Phase 6 business scenario.
        modelBuilder.Entity<Mark>().HasData(
            new Mark { Id = 1, StudentId = 1, AssessmentId = 1, MarksObtained = 87 },
            new Mark { Id = 2, StudentId = 2, AssessmentId = 1, MarksObtained = 72 },
            new Mark { Id = 3, StudentId = 3, AssessmentId = 1, MarksObtained = 94 },
            new Mark { Id = 4, StudentId = 4, AssessmentId = 1, MarksObtained = 81 },

            new Mark { Id = 5, StudentId = 1, AssessmentId = 2, MarksObtained = 91 },
            new Mark { Id = 6, StudentId = 2, AssessmentId = 2, MarksObtained = 68 },
            new Mark { Id = 7, StudentId = 3, AssessmentId = 2, MarksObtained = 89 },
            new Mark { Id = 8, StudentId = 4, AssessmentId = 2, MarksObtained = 77 },

            new Mark { Id = 9, StudentId = 1, AssessmentId = 3, MarksObtained = 84 },
            new Mark { Id = 10, StudentId = 2, AssessmentId = 3, MarksObtained = 75 },
            new Mark { Id = 11, StudentId = 3, AssessmentId = 3, MarksObtained = 90 },
            new Mark { Id = 12, StudentId = 4, AssessmentId = 3, MarksObtained = 79 },

            new Mark { Id = 13, StudentId = 1, AssessmentId = 4, MarksObtained = 46 },
            new Mark { Id = 14, StudentId = 2, AssessmentId = 4, MarksObtained = 38 },
            new Mark { Id = 15, StudentId = 3, AssessmentId = 4, MarksObtained = 48 },
            new Mark { Id = 16, StudentId = 4, AssessmentId = 4, MarksObtained = 41 }
        );

        modelBuilder.Entity<Teacher>().HasData(
            new Teacher { Id = 1, TeacherId = "T001", FirstName = "Priya", LastName = "Mehta", Email = "priya.mehta@example.com" },
            new Teacher { Id = 2, TeacherId = "T002", FirstName = "David", LastName = "Thomas", Email = "david.thomas@example.com" },
            new Teacher { Id = 3, TeacherId = "T003", FirstName = "Kavita", LastName = "Nair", Email = "kavita.nair@example.com" }
        );

        modelBuilder.Entity<Homework>().HasData(
            new Homework { Id = 1, Title = "Algebra Practice", Description = "Complete questions 1-20 from the Algebra worksheet.", SubjectId = 1, GradeId = 1, TeacherId = 1, CreatedDate = new DateOnly(2026, 9, 10), DueDate = new DateOnly(2026, 9, 25) },
            new Homework { Id = 2, Title = "States of Matter", Description = "Read chapter 3 and answer the review questions.", SubjectId = 2, GradeId = 1, TeacherId = 2, CreatedDate = new DateOnly(2026, 9, 10), DueDate = new DateOnly(2026, 9, 27) },
            new Homework { Id = 3, Title = "Grammar Exercise", Description = "Complete the grammar worksheet on tenses.", SubjectId = 3, GradeId = 1, TeacherId = 3, CreatedDate = new DateOnly(2026, 9, 10), DueDate = new DateOnly(2026, 9, 28) }
        );

        // A small demo set — Aisha (student 3) has no submission for Homework 1, demonstrating "Not Submitted" as an absence of data.
        modelBuilder.Entity<HomeworkSubmission>().HasData(
            new HomeworkSubmission { Id = 1, HomeworkId = 1, StudentId = 1, SubmittedDate = new DateTime(2026, 9, 20, 14, 30, 0, DateTimeKind.Utc), Status = HomeworkSubmissionStatus.Submitted, Comments = "Completed all 20 questions." },
            new HomeworkSubmission { Id = 2, HomeworkId = 1, StudentId = 2, SubmittedDate = new DateTime(2026, 9, 21, 9, 15, 0, DateTimeKind.Utc), Status = HomeworkSubmissionStatus.Submitted, Comments = "Done, some questions were tricky." },
            new HomeworkSubmission { Id = 3, HomeworkId = 1, StudentId = 4, SubmittedDate = new DateTime(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc), Status = HomeworkSubmissionStatus.Submitted, Comments = "Finished last night." },
            // Homework 2 is due 27 Sep — this submission on 28 Sep demonstrates the Late calculation.
            new HomeworkSubmission { Id = 4, HomeworkId = 2, StudentId = 1, SubmittedDate = new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc), Status = HomeworkSubmissionStatus.Late, Comments = "Sorry, submitted a day late." }
        );

        modelBuilder.Entity<Activity>().HasData(
            new Activity { Id = 1, Title = "Science Exhibition", Description = "Showcase science projects to the school.", Date = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc), Location = "School Auditorium", GradeId = 1, Capacity = 50, CreatedDate = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc) },
            new Activity { Id = 2, Title = "Coding Workshop", Description = "Learn the basics of programming through hands-on activities.", Date = new DateTime(2026, 10, 15, 14, 0, 0, DateTimeKind.Utc), Location = "Computer Lab", GradeId = 1, Capacity = 20, CreatedDate = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc) },
            new Activity { Id = 3, Title = "Inter-School Debate", Description = "Represent the school in a friendly debate competition.", Date = new DateTime(2026, 10, 20, 11, 0, 0, DateTimeKind.Utc), Location = "Activity Hall", GradeId = 2, Capacity = 30, CreatedDate = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc) },
            new Activity { Id = 4, Title = "Football Tournament", Description = "Inter-house football tournament.", Date = new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc), Location = "School Ground", GradeId = 1, Capacity = 40, CreatedDate = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc) },
            new Activity { Id = 5, Title = "Art Competition", Description = "Show your creativity in a school-wide art competition.", Date = new DateTime(2026, 10, 18, 13, 0, 0, DateTimeKind.Utc), Location = "Art Room", GradeId = 3, Capacity = 25, CreatedDate = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc) }
        );

        // A small demo set of registrations — includes an active Cancelled row to show it doesn't count toward capacity.
        modelBuilder.Entity<ActivityRegistration>().HasData(
            new ActivityRegistration { Id = 1, StudentId = 1, ActivityId = 1, RegistrationDate = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc), Status = ActivityRegistrationStatus.Registered },
            new ActivityRegistration { Id = 2, StudentId = 2, ActivityId = 1, RegistrationDate = new DateTime(2026, 9, 11, 10, 5, 0, DateTimeKind.Utc), Status = ActivityRegistrationStatus.Registered },
            new ActivityRegistration { Id = 3, StudentId = 3, ActivityId = 1, RegistrationDate = new DateTime(2026, 9, 11, 10, 10, 0, DateTimeKind.Utc), Status = ActivityRegistrationStatus.Registered },
            new ActivityRegistration { Id = 4, StudentId = 1, ActivityId = 2, RegistrationDate = new DateTime(2026, 9, 11, 11, 0, 0, DateTimeKind.Utc), Status = ActivityRegistrationStatus.Registered },
            new ActivityRegistration { Id = 5, StudentId = 4, ActivityId = 2, RegistrationDate = new DateTime(2026, 9, 11, 11, 5, 0, DateTimeKind.Utc), Status = ActivityRegistrationStatus.Registered },
            new ActivityRegistration { Id = 6, StudentId = 5, ActivityId = 3, RegistrationDate = new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc), Status = ActivityRegistrationStatus.Registered },
            new ActivityRegistration { Id = 7, StudentId = 2, ActivityId = 4, RegistrationDate = new DateTime(2026, 9, 11, 13, 0, 0, DateTimeKind.Utc), Status = ActivityRegistrationStatus.Registered },
            new ActivityRegistration { Id = 8, StudentId = 3, ActivityId = 4, RegistrationDate = new DateTime(2026, 9, 11, 13, 5, 0, DateTimeKind.Utc), Status = ActivityRegistrationStatus.Cancelled }
        );

        // Development-only demo accounts (see docs/phase-10.md) — passwords are the literal strings
        // "Student123!", "Teacher123!", "Admin123!", hashed once with ASP.NET Core's PasswordHasher<T>.
        // Never plaintext, never real credentials, never used outside local learning/testing.
        modelBuilder.Entity<ApplicationUser>().HasData(
            new ApplicationUser { Id = 1, Username = "student1", PasswordHash = "AQAAAAIAAYagAAAAEM6+LRrGSrEilLctUvvHkNPBPFS6xbNkllKri/okOz1huRxYZhS5pCDOutMjWMe0Lg==", Role = UserRole.Student, StudentId = 1 },
            new ApplicationUser { Id = 2, Username = "student2", PasswordHash = "AQAAAAIAAYagAAAAEM6+LRrGSrEilLctUvvHkNPBPFS6xbNkllKri/okOz1huRxYZhS5pCDOutMjWMe0Lg==", Role = UserRole.Student, StudentId = 2 },
            new ApplicationUser { Id = 3, Username = "teacher1", PasswordHash = "AQAAAAIAAYagAAAAEN3zBs3zE/9wQna3A6hG+O0aoOTolBtGKV+URht1R3XF51rb2m6HOMF20KJUMsQd1w==", Role = UserRole.Teacher, TeacherId = 1 },
            new ApplicationUser { Id = 4, Username = "admin1", PasswordHash = "AQAAAAIAAYagAAAAEMW0cZzVj7vYgZmGyQH8eOYSgADtCkf3lTIBWPwRQtsGAB5ab1k7Pna/nLZokToH8A==", Role = UserRole.Admin }
        );
    }
}
