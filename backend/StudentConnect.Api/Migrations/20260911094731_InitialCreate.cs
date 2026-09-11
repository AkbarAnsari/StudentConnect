using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StudentConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Grades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeacherId = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    GradeId = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.CheckConstraint("CK_Activity_Capacity", "\"Capacity\" > 0");
                    table.ForeignKey(
                        name: "FK_Activities_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Section = table.Column<string>(type: "text", nullable: false),
                    GradeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MaximumMarks = table.Column<int>(type: "integer", nullable: false),
                    AssessmentType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                    table.CheckConstraint("CK_Assessment_MaximumMarks", "\"MaximumMarks\" > 0");
                    table.ForeignKey(
                        name: "FK_Assessments_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Homework",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    GradeId = table.Column<int>(type: "integer", nullable: false),
                    TeacherId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Homework", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Homework_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Homework_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Homework_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningMaterials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    GradeId = table.Column<int>(type: "integer", nullable: false),
                    TeacherId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    StoredFileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningMaterials_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningMaterials_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningMaterials_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityRegistrations_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityRegistrations_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attendance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendance_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attendance_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentSubjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentSubjects_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentSubjects_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: true),
                    TeacherId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Marks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    AssessmentId = table.Column<int>(type: "integer", nullable: false),
                    MarksObtained = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marks", x => x.Id);
                    table.CheckConstraint("CK_Mark_MarksObtained", "\"MarksObtained\" >= 0");
                    table.ForeignKey(
                        name: "FK_Marks_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Marks_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HomeworkSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HomeworkId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Comments = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkSubmissions_Homework_HomeworkId",
                        column: x => x.HomeworkId,
                        principalTable: "Homework",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeworkSubmissions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Grades",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Grade 8" },
                    { 2, "Grade 9" },
                    { 3, "Grade 10" }
                });

            migrationBuilder.InsertData(
                table: "Subjects",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "MATH", "Mathematics" },
                    { 2, "SCI", "Science" },
                    { 3, "ENG", "English" },
                    { 4, "CS", "Computer Science" }
                });

            migrationBuilder.InsertData(
                table: "Teachers",
                columns: new[] { "Id", "Email", "FirstName", "LastName", "TeacherId" },
                values: new object[,]
                {
                    { 1, "priya.mehta@example.com", "Priya", "Mehta", "T001" },
                    { 2, "david.thomas@example.com", "David", "Thomas", "T002" },
                    { 3, "kavita.nair@example.com", "Kavita", "Nair", "T003" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "PasswordHash", "Role", "StudentId", "TeacherId", "Username" },
                values: new object[] { 4, "AQAAAAIAAYagAAAAEMW0cZzVj7vYgZmGyQH8eOYSgADtCkf3lTIBWPwRQtsGAB5ab1k7Pna/nLZokToH8A==", "Admin", null, null, "admin1" });

            migrationBuilder.InsertData(
                table: "Activities",
                columns: new[] { "Id", "Capacity", "CreatedDate", "Date", "Description", "GradeId", "Location", "Title" },
                values: new object[,]
                {
                    { 1, 50, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 5, 10, 0, 0, 0, DateTimeKind.Utc), "Showcase science projects to the school.", 1, "School Auditorium", "Science Exhibition" },
                    { 2, 20, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 15, 14, 0, 0, 0, DateTimeKind.Utc), "Learn the basics of programming through hands-on activities.", 1, "Computer Lab", "Coding Workshop" },
                    { 3, 30, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 20, 11, 0, 0, 0, DateTimeKind.Utc), "Represent the school in a friendly debate competition.", 2, "Activity Hall", "Inter-School Debate" },
                    { 4, 40, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 25, 9, 0, 0, 0, DateTimeKind.Utc), "Inter-house football tournament.", 1, "School Ground", "Football Tournament" },
                    { 5, 25, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 18, 13, 0, 0, 0, DateTimeKind.Utc), "Show your creativity in a school-wide art competition.", 3, "Art Room", "Art Competition" }
                });

            migrationBuilder.InsertData(
                table: "Assessments",
                columns: new[] { "Id", "AssessmentType", "Date", "Description", "MaximumMarks", "SubjectId", "Title" },
                values: new object[,]
                {
                    { 1, "UnitTest", new DateOnly(2026, 9, 15), "Algebra basics", 100, 1, "Unit Test 1" },
                    { 2, "UnitTest", new DateOnly(2026, 9, 22), "Geometry basics", 100, 1, "Unit Test 2" },
                    { 3, "UnitTest", new DateOnly(2026, 9, 18), "Physical and chemical changes", 100, 2, "Unit Test 1" },
                    { 4, "Quiz", new DateOnly(2026, 9, 20), "Tenses and parts of speech", 50, 3, "Grammar Test 1" }
                });

            migrationBuilder.InsertData(
                table: "Homework",
                columns: new[] { "Id", "CreatedDate", "Description", "DueDate", "GradeId", "SubjectId", "TeacherId", "Title" },
                values: new object[,]
                {
                    { 1, new DateOnly(2026, 9, 10), "Complete questions 1-20 from the Algebra worksheet.", new DateOnly(2026, 9, 25), 1, 1, 1, "Algebra Practice" },
                    { 2, new DateOnly(2026, 9, 10), "Read chapter 3 and answer the review questions.", new DateOnly(2026, 9, 27), 1, 2, 2, "States of Matter" },
                    { 3, new DateOnly(2026, 9, 10), "Complete the grammar worksheet on tenses.", new DateOnly(2026, 9, 28), 1, 3, 3, "Grammar Exercise" }
                });

            migrationBuilder.InsertData(
                table: "Students",
                columns: new[] { "Id", "Email", "FirstName", "GradeId", "LastName", "Section", "StudentId" },
                values: new object[,]
                {
                    { 1, "ananya@example.com", "Ananya", 1, "Sharma", "A", "STU001" },
                    { 2, "rahul@example.com", "Rahul", 1, "Verma", "A", "STU002" },
                    { 3, "aisha@example.com", "Aisha", 1, "Khan", "B", "STU003" },
                    { 4, "arjun@example.com", "Arjun", 1, "Nair", "B", "STU004" },
                    { 5, "priya@example.com", "Priya", 2, "Iyer", "A", "STU005" },
                    { 6, "vikram@example.com", "Vikram", 2, "Rao", "A", "STU006" },
                    { 7, "meera@example.com", "Meera", 2, "Pillai", "B", "STU007" },
                    { 8, "karan@example.com", "Karan", 3, "Singh", "A", "STU008" },
                    { 9, "divya@example.com", "Divya", 3, "Menon", "A", "STU009" },
                    { 10, "rohan@example.com", "Rohan", 3, "Gupta", "B", "STU010" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "PasswordHash", "Role", "StudentId", "TeacherId", "Username" },
                values: new object[] { 3, "AQAAAAIAAYagAAAAEN3zBs3zE/9wQna3A6hG+O0aoOTolBtGKV+URht1R3XF51rb2m6HOMF20KJUMsQd1w==", "Teacher", null, 1, "teacher1" });

            migrationBuilder.InsertData(
                table: "ActivityRegistrations",
                columns: new[] { "Id", "ActivityId", "RegistrationDate", "Status", "StudentId" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 9, 11, 10, 0, 0, 0, DateTimeKind.Utc), "Registered", 1 },
                    { 2, 1, new DateTime(2026, 9, 11, 10, 5, 0, 0, DateTimeKind.Utc), "Registered", 2 },
                    { 3, 1, new DateTime(2026, 9, 11, 10, 10, 0, 0, DateTimeKind.Utc), "Registered", 3 },
                    { 4, 2, new DateTime(2026, 9, 11, 11, 0, 0, 0, DateTimeKind.Utc), "Registered", 1 },
                    { 5, 2, new DateTime(2026, 9, 11, 11, 5, 0, 0, DateTimeKind.Utc), "Registered", 4 },
                    { 6, 3, new DateTime(2026, 9, 11, 12, 0, 0, 0, DateTimeKind.Utc), "Registered", 5 },
                    { 7, 4, new DateTime(2026, 9, 11, 13, 0, 0, 0, DateTimeKind.Utc), "Registered", 2 },
                    { 8, 4, new DateTime(2026, 9, 11, 13, 5, 0, 0, DateTimeKind.Utc), "Cancelled", 3 }
                });

            migrationBuilder.InsertData(
                table: "Attendance",
                columns: new[] { "Id", "Date", "Status", "StudentId", "SubjectId" },
                values: new object[,]
                {
                    { 1, new DateOnly(2026, 9, 1), "Present", 1, 1 },
                    { 2, new DateOnly(2026, 9, 2), "Present", 1, 1 },
                    { 3, new DateOnly(2026, 9, 3), "Present", 1, 1 },
                    { 4, new DateOnly(2026, 9, 4), "Present", 1, 1 },
                    { 5, new DateOnly(2026, 9, 5), "Present", 1, 1 },
                    { 6, new DateOnly(2026, 9, 6), "Absent", 1, 1 },
                    { 7, new DateOnly(2026, 9, 7), "Present", 1, 1 },
                    { 8, new DateOnly(2026, 9, 8), "Present", 1, 1 },
                    { 9, new DateOnly(2026, 9, 9), "Present", 1, 1 },
                    { 10, new DateOnly(2026, 9, 10), "Present", 1, 1 },
                    { 11, new DateOnly(2026, 9, 1), "Present", 2, 1 },
                    { 12, new DateOnly(2026, 9, 2), "Present", 2, 1 },
                    { 13, new DateOnly(2026, 9, 3), "Present", 2, 1 },
                    { 14, new DateOnly(2026, 9, 4), "Present", 2, 1 },
                    { 15, new DateOnly(2026, 9, 5), "Absent", 2, 1 },
                    { 16, new DateOnly(2026, 9, 6), "Present", 2, 1 },
                    { 17, new DateOnly(2026, 9, 7), "Present", 2, 1 },
                    { 18, new DateOnly(2026, 9, 8), "Present", 2, 1 },
                    { 19, new DateOnly(2026, 9, 9), "Present", 2, 1 },
                    { 20, new DateOnly(2026, 9, 10), "Present", 2, 1 },
                    { 21, new DateOnly(2026, 9, 1), "Present", 3, 1 },
                    { 22, new DateOnly(2026, 9, 2), "Present", 3, 1 },
                    { 23, new DateOnly(2026, 9, 3), "Present", 3, 1 },
                    { 24, new DateOnly(2026, 9, 4), "Absent", 3, 1 },
                    { 25, new DateOnly(2026, 9, 5), "Present", 3, 1 },
                    { 26, new DateOnly(2026, 9, 6), "Present", 3, 1 },
                    { 27, new DateOnly(2026, 9, 7), "Present", 3, 1 },
                    { 28, new DateOnly(2026, 9, 8), "Present", 3, 1 },
                    { 29, new DateOnly(2026, 9, 9), "Present", 3, 1 },
                    { 30, new DateOnly(2026, 9, 10), "Absent", 3, 1 },
                    { 31, new DateOnly(2026, 9, 1), "Present", 4, 1 },
                    { 32, new DateOnly(2026, 9, 2), "Present", 4, 1 },
                    { 33, new DateOnly(2026, 9, 3), "Absent", 4, 1 },
                    { 34, new DateOnly(2026, 9, 4), "Present", 4, 1 },
                    { 35, new DateOnly(2026, 9, 5), "Present", 4, 1 },
                    { 36, new DateOnly(2026, 9, 6), "Present", 4, 1 },
                    { 37, new DateOnly(2026, 9, 7), "Present", 4, 1 },
                    { 38, new DateOnly(2026, 9, 8), "Present", 4, 1 },
                    { 39, new DateOnly(2026, 9, 9), "Absent", 4, 1 },
                    { 40, new DateOnly(2026, 9, 10), "Present", 4, 1 },
                    { 41, new DateOnly(2026, 9, 1), "Present", 1, 2 },
                    { 42, new DateOnly(2026, 9, 2), "Present", 1, 2 },
                    { 43, new DateOnly(2026, 9, 3), "Present", 1, 2 },
                    { 44, new DateOnly(2026, 9, 4), "Present", 1, 2 },
                    { 45, new DateOnly(2026, 9, 5), "Present", 1, 2 },
                    { 46, new DateOnly(2026, 9, 6), "Absent", 1, 2 },
                    { 47, new DateOnly(2026, 9, 7), "Present", 1, 2 },
                    { 48, new DateOnly(2026, 9, 8), "Present", 1, 2 },
                    { 49, new DateOnly(2026, 9, 9), "Present", 1, 2 },
                    { 50, new DateOnly(2026, 9, 10), "Present", 1, 2 },
                    { 51, new DateOnly(2026, 9, 1), "Present", 1, 3 },
                    { 52, new DateOnly(2026, 9, 2), "Present", 1, 3 },
                    { 53, new DateOnly(2026, 9, 3), "Present", 1, 3 },
                    { 54, new DateOnly(2026, 9, 4), "Present", 1, 3 },
                    { 55, new DateOnly(2026, 9, 5), "Absent", 1, 3 },
                    { 56, new DateOnly(2026, 9, 6), "Present", 1, 3 },
                    { 57, new DateOnly(2026, 9, 7), "Present", 1, 3 },
                    { 58, new DateOnly(2026, 9, 8), "Present", 1, 3 },
                    { 59, new DateOnly(2026, 9, 9), "Present", 1, 3 },
                    { 60, new DateOnly(2026, 9, 10), "Present", 1, 3 },
                    { 61, new DateOnly(2026, 9, 1), "Present", 1, 4 },
                    { 62, new DateOnly(2026, 9, 2), "Present", 1, 4 },
                    { 63, new DateOnly(2026, 9, 3), "Present", 1, 4 },
                    { 64, new DateOnly(2026, 9, 4), "Absent", 1, 4 },
                    { 65, new DateOnly(2026, 9, 5), "Present", 1, 4 },
                    { 66, new DateOnly(2026, 9, 6), "Present", 1, 4 },
                    { 67, new DateOnly(2026, 9, 7), "Present", 1, 4 },
                    { 68, new DateOnly(2026, 9, 8), "Present", 1, 4 },
                    { 69, new DateOnly(2026, 9, 9), "Present", 1, 4 },
                    { 70, new DateOnly(2026, 9, 10), "Present", 1, 4 }
                });

            migrationBuilder.InsertData(
                table: "HomeworkSubmissions",
                columns: new[] { "Id", "Comments", "HomeworkId", "Status", "StudentId", "SubmittedDate" },
                values: new object[,]
                {
                    { 1, "Completed all 20 questions.", 1, "Submitted", 1, new DateTime(2026, 9, 20, 14, 30, 0, 0, DateTimeKind.Utc) },
                    { 2, "Done, some questions were tricky.", 1, "Submitted", 2, new DateTime(2026, 9, 21, 9, 15, 0, 0, DateTimeKind.Utc) },
                    { 3, "Finished last night.", 1, "Submitted", 4, new DateTime(2026, 9, 24, 18, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "Sorry, submitted a day late.", 2, "Late", 1, new DateTime(2026, 9, 28, 10, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Marks",
                columns: new[] { "Id", "AssessmentId", "MarksObtained", "StudentId" },
                values: new object[,]
                {
                    { 1, 1, 87, 1 },
                    { 2, 1, 72, 2 },
                    { 3, 1, 94, 3 },
                    { 4, 1, 81, 4 },
                    { 5, 2, 91, 1 },
                    { 6, 2, 68, 2 },
                    { 7, 2, 89, 3 },
                    { 8, 2, 77, 4 },
                    { 9, 3, 84, 1 },
                    { 10, 3, 75, 2 },
                    { 11, 3, 90, 3 },
                    { 12, 3, 79, 4 },
                    { 13, 4, 46, 1 },
                    { 14, 4, 38, 2 },
                    { 15, 4, 48, 3 },
                    { 16, 4, 41, 4 }
                });

            migrationBuilder.InsertData(
                table: "StudentSubjects",
                columns: new[] { "Id", "StudentId", "SubjectId" },
                values: new object[,]
                {
                    { 1, 1, 1 },
                    { 2, 1, 2 },
                    { 3, 1, 3 },
                    { 4, 1, 4 },
                    { 5, 2, 1 },
                    { 6, 2, 2 },
                    { 7, 2, 3 },
                    { 8, 2, 4 },
                    { 9, 3, 1 },
                    { 10, 3, 2 },
                    { 11, 3, 3 },
                    { 12, 3, 4 },
                    { 13, 4, 1 },
                    { 14, 4, 2 },
                    { 15, 4, 3 },
                    { 16, 4, 4 },
                    { 17, 5, 1 },
                    { 18, 5, 2 },
                    { 19, 5, 3 },
                    { 20, 5, 4 },
                    { 21, 6, 1 },
                    { 22, 6, 2 },
                    { 23, 6, 3 },
                    { 24, 6, 4 },
                    { 25, 7, 1 },
                    { 26, 7, 2 },
                    { 27, 7, 3 },
                    { 28, 7, 4 },
                    { 29, 8, 1 },
                    { 30, 8, 2 },
                    { 31, 8, 3 },
                    { 32, 8, 4 },
                    { 33, 9, 1 },
                    { 34, 9, 2 },
                    { 35, 9, 3 },
                    { 36, 9, 4 },
                    { 37, 10, 1 },
                    { 38, 10, 2 },
                    { 39, 10, 3 },
                    { 40, 10, 4 }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "PasswordHash", "Role", "StudentId", "TeacherId", "Username" },
                values: new object[,]
                {
                    { 1, "AQAAAAIAAYagAAAAEM6+LRrGSrEilLctUvvHkNPBPFS6xbNkllKri/okOz1huRxYZhS5pCDOutMjWMe0Lg==", "Student", 1, null, "student1" },
                    { 2, "AQAAAAIAAYagAAAAEM6+LRrGSrEilLctUvvHkNPBPFS6xbNkllKri/okOz1huRxYZhS5pCDOutMjWMe0Lg==", "Student", 2, null, "student2" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_GradeId",
                table: "Activities",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRegistrations_ActivityId",
                table: "ActivityRegistrations",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRegistrations_StudentId_ActivityId",
                table: "ActivityRegistrations",
                columns: new[] { "StudentId", "ActivityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_SubjectId",
                table: "Assessments",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_StudentId_SubjectId_Date",
                table: "Attendance",
                columns: new[] { "StudentId", "SubjectId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_SubjectId",
                table: "Attendance",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Homework_GradeId",
                table: "Homework",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_Homework_SubjectId",
                table: "Homework",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Homework_TeacherId",
                table: "Homework",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSubmissions_HomeworkId",
                table: "HomeworkSubmissions",
                column: "HomeworkId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSubmissions_StudentId_HomeworkId",
                table: "HomeworkSubmissions",
                columns: new[] { "StudentId", "HomeworkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningMaterials_GradeId",
                table: "LearningMaterials",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningMaterials_StoredFileName",
                table: "LearningMaterials",
                column: "StoredFileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningMaterials_SubjectId",
                table: "LearningMaterials",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningMaterials_TeacherId",
                table: "LearningMaterials",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Marks_AssessmentId",
                table: "Marks",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Marks_StudentId_AssessmentId",
                table: "Marks",
                columns: new[] { "StudentId", "AssessmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_GradeId",
                table: "Students",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentId",
                table: "Students",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubjects_StudentId_SubjectId",
                table: "StudentSubjects",
                columns: new[] { "StudentId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubjects_SubjectId",
                table: "StudentSubjects",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_StudentId",
                table: "Users",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TeacherId",
                table: "Users",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityRegistrations");

            migrationBuilder.DropTable(
                name: "Attendance");

            migrationBuilder.DropTable(
                name: "HomeworkSubmissions");

            migrationBuilder.DropTable(
                name: "LearningMaterials");

            migrationBuilder.DropTable(
                name: "Marks");

            migrationBuilder.DropTable(
                name: "StudentSubjects");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "Homework");

            migrationBuilder.DropTable(
                name: "Assessments");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "Teachers");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DropTable(
                name: "Grades");
        }
    }
}
