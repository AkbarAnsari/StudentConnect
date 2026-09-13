using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class HomeworkSubmissionStore(Database db)
{
    private const string SelectWithNames = """
        SELECT s."Id", s."HomeworkId", s."StudentId", s."SubmittedDate", s."Status", s."Comments",
               h."Title" AS "HomeworkTitle", h."DueDate" AS "HomeworkDueDate",
               st."FirstName" AS "StudentFirstName", st."LastName" AS "StudentLastName"
        FROM "HomeworkSubmissions" s
        JOIN "Homework" h ON h."Id" = s."HomeworkId"
        JOIN "Students" st ON st."Id" = s."StudentId"
        """;

    private static HomeworkSubmission Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        HomeworkId = reader.GetInt32("HomeworkId"),
        Homework = new Homework { Title = reader.GetString("HomeworkTitle"), DueDate = reader.GetDateOnly("HomeworkDueDate") },
        StudentId = reader.GetInt32("StudentId"),
        Student = new Student { FirstName = reader.GetString("StudentFirstName"), LastName = reader.GetString("StudentLastName") },
        SubmittedDate = reader.GetDateTimeUtc("SubmittedDate"),
        Status = reader.GetEnum<HomeworkSubmissionStatus>("Status"),
        Comments = reader.GetString("Comments"),
    };

    public async Task<HomeworkSubmission?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{SelectWithNames} WHERE s."Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task<List<HomeworkSubmission>> GetByStudentAsync(int studentId) =>
        await db.QueryAsync(
            $"""{SelectWithNames} WHERE s."StudentId" = @studentId ORDER BY s."SubmittedDate" DESC""",
            p => p.AddWithValue("studentId", studentId),
            Map);

    // Used by the student homework list to derive per-homework status without a full join.
    public async Task<List<HomeworkSubmission>> GetByStudentForHomeworkIdsAsync(int studentId, IReadOnlyCollection<int> homeworkIds)
    {
        if (homeworkIds.Count == 0)
        {
            return [];
        }

        return await db.QueryAsync(
            """SELECT "Id", "HomeworkId", "StudentId", "SubmittedDate", "Status", "Comments" FROM "HomeworkSubmissions" WHERE "StudentId" = @studentId AND "HomeworkId" = ANY(@homeworkIds)""",
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("homeworkIds", homeworkIds.ToArray());
            },
            reader => new HomeworkSubmission
            {
                Id = reader.GetInt32("Id"),
                HomeworkId = reader.GetInt32("HomeworkId"),
                StudentId = reader.GetInt32("StudentId"),
                SubmittedDate = reader.GetDateTimeUtc("SubmittedDate"),
                Status = reader.GetEnum<HomeworkSubmissionStatus>("Status"),
                Comments = reader.GetString("Comments"),
            });
    }

    // Raw rows (no joins) for the teacher "who submitted" overview — the controller merges
    // this with the class roster itself, exactly like the original EF version.
    public async Task<List<HomeworkSubmission>> GetByHomeworkIdRawAsync(int homeworkId) =>
        await db.QueryAsync(
            """SELECT "Id", "StudentId", "SubmittedDate", "Status", "Comments" FROM "HomeworkSubmissions" WHERE "HomeworkId" = @homeworkId""",
            p => p.AddWithValue("homeworkId", homeworkId),
            reader => new HomeworkSubmission
            {
                Id = reader.GetInt32("Id"),
                StudentId = reader.GetInt32("StudentId"),
                SubmittedDate = reader.GetDateTimeUtc("SubmittedDate"),
                Status = reader.GetEnum<HomeworkSubmissionStatus>("Status"),
                Comments = reader.GetString("Comments"),
            });

    public async Task<bool> ExistsAsync(int studentId, int homeworkId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "HomeworkSubmissions" WHERE "StudentId" = @studentId AND "HomeworkId" = @homeworkId""",
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("homeworkId", homeworkId);
            });

    public async Task<int> CreateAsync(HomeworkSubmission submission) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "HomeworkSubmissions" ("HomeworkId", "StudentId", "SubmittedDate", "Status", "Comments")
            VALUES (@homeworkId, @studentId, @submittedDate, @status, @comments)
            RETURNING "Id"
            """,
            p => Bind(p, submission));

    public async Task UpdateAsync(int id, DateTime submittedDate, HomeworkSubmissionStatus status, string comments) =>
        await db.ExecuteAsync(
            """UPDATE "HomeworkSubmissions" SET "SubmittedDate" = @submittedDate, "Status" = @status, "Comments" = @comments WHERE "Id" = @id""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("submittedDate", submittedDate);
                p.AddWithValue("status", status.ToString());
                p.AddWithValue("comments", comments);
            });

    private static void Bind(NpgsqlParameterCollection p, HomeworkSubmission submission)
    {
        p.AddWithValue("homeworkId", submission.HomeworkId);
        p.AddWithValue("studentId", submission.StudentId);
        p.AddWithValue("submittedDate", submission.SubmittedDate);
        p.AddWithValue("status", submission.Status.ToString());
        p.AddWithValue("comments", submission.Comments);
    }
}
