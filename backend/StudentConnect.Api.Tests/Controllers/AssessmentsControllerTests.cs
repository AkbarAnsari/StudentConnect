using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Controllers;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using Xunit;

namespace StudentConnect.Api.Tests.Controllers;

// Runs against a real Postgres test database (see DatabaseFixture).
[Collection("Database")]
public class AssessmentsControllerTests(DatabaseFixture fixture)
{
    private AssessmentsController CreateController() =>
        new(
            new AssessmentStore(fixture.Database),
            new ReferenceDataStore(fixture.Database),
            new TermStore(fixture.Database),
            new FakeCurrentTenantService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AssessmentsController>.Instance);

    private async Task<int> SeedSubjectAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return await fixture.Database.ExecuteScalarAsync<int>(
            """INSERT INTO "Subjects" ("Name", "Code", "SchoolId") VALUES (@name, @code, 1) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", $"Subject-{suffix}");
                p.AddWithValue("code", suffix);
            });
    }

    private async Task<int> SeedLockedTermAsync()
    {
        var db = fixture.Database;
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var academicYearId = await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "AcademicYears" ("Name", "StartDate", "EndDate", "SchoolId")
            VALUES (@name, @start, @end, 1) RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("name", $"Year-{suffix}");
                p.AddWithValue("start", new DateOnly(2026, 1, 1));
                p.AddWithValue("end", new DateOnly(2026, 12, 31));
            });

        var termId = await new TermStore(db).CreateAsync(new Term
        {
            AcademicYearId = academicYearId,
            Name = $"Term-{suffix}",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 6, 30),
        });

        await new TermStore(db).LockAsync(termId);
        return termId;
    }

    [Fact]
    public async Task CreateAssessment_ZeroOrNegativeWeight_DefaultsToOne()
    {
        var subjectId = await SeedSubjectAsync();
        var controller = CreateController();

        var result = await controller.CreateAssessment(new StudentConnect.Application.AssessmentCreateDto
        {
            Title = "Quiz",
            SubjectId = subjectId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            MaximumMarks = 50,
            AssessmentType = "Quiz",
            Description = "",
            Weight = 0,
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<StudentConnect.Application.AssessmentDto>(created.Value);
        Assert.Equal(1.0m, dto.Weight);
    }

    [Fact]
    public async Task CreateAssessment_PositiveWeight_IsPreserved()
    {
        var subjectId = await SeedSubjectAsync();
        var controller = CreateController();

        var result = await controller.CreateAssessment(new StudentConnect.Application.AssessmentCreateDto
        {
            Title = "Final Exam",
            SubjectId = subjectId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            MaximumMarks = 100,
            AssessmentType = "FinalExam",
            Description = "",
            Weight = 3.5m,
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<StudentConnect.Application.AssessmentDto>(created.Value);
        Assert.Equal(3.5m, dto.Weight);
    }

    [Fact]
    public async Task UpdateAssessment_InLockedTerm_ReturnsConflict()
    {
        var subjectId = await SeedSubjectAsync();
        var lockedTermId = await SeedLockedTermAsync();
        var controller = CreateController();

        var createResult = await controller.CreateAssessment(new StudentConnect.Application.AssessmentCreateDto
        {
            Title = "Pre-lock Quiz",
            SubjectId = subjectId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            MaximumMarks = 50,
            AssessmentType = "Quiz",
            Description = "",
            TermId = null,
        });
        var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
        var createdDto = Assert.IsType<StudentConnect.Application.AssessmentDto>(created.Value);

        // Attach the assessment to the now-locked term directly (bypassing controller validation,
        // which would itself reject TermId on create/update once locked) to set up the update test.
        await fixture.Database.ExecuteAsync(
            """UPDATE "Assessments" SET "TermId" = @termId WHERE "Id" = @id""",
            p =>
            {
                p.AddWithValue("termId", lockedTermId);
                p.AddWithValue("id", createdDto.Id);
            });

        var updateResult = await controller.UpdateAssessment(createdDto.Id, new StudentConnect.Application.AssessmentUpdateDto
        {
            Title = "Edited Quiz",
            SubjectId = subjectId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            MaximumMarks = 50,
            AssessmentType = "Quiz",
            Description = "",
            TermId = lockedTermId,
        });

        Assert.IsType<ConflictObjectResult>(updateResult.Result);
    }

    [Fact]
    public async Task CreateAssessment_LockedTerm_RejectedUpfront()
    {
        var subjectId = await SeedSubjectAsync();
        var lockedTermId = await SeedLockedTermAsync();
        var controller = CreateController();

        var result = await controller.CreateAssessment(new StudentConnect.Application.AssessmentCreateDto
        {
            Title = "New Assessment",
            SubjectId = subjectId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            MaximumMarks = 50,
            AssessmentType = "Quiz",
            Description = "",
            TermId = lockedTermId,
        });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }
}
