using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class HomeworkSubmissionStore(Database db)
{
    private const string SelectWithNames = """
        SELECT s."Id", s."HomeworkId", s."StudentId", s."SubmittedDate", s."Status", s."Comments",
               s."FileName", s."StoredFileName", s."ContentType", s."FileSize", s."TeacherFeedback", s."FeedbackAtUtc",
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
        FileName = reader.GetNullableString("FileName"),
        StoredFileName = reader.GetNullableString("StoredFileName"),
        ContentType = reader.GetNullableString("ContentType"),
        FileSize = reader.GetNullableInt64("FileSize"),
        TeacherFeedback = reader.GetNullableString("TeacherFeedback"),
        FeedbackAtUtc = reader.GetNullableDateTimeUtc("FeedbackAtUtc"),
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
            """
            SELECT "Id", "StudentId", "SubmittedDate", "Status", "Comments", "StoredFileName", "TeacherFeedback"
            FROM "HomeworkSubmissions" WHERE "HomeworkId" = @homeworkId
            """,
            p => p.AddWithValue("homeworkId", homeworkId),
            reader => new HomeworkSubmission
            {
                Id = reader.GetInt32("Id"),
                StudentId = reader.GetInt32("StudentId"),
                SubmittedDate = reader.GetDateTimeUtc("SubmittedDate"),
                Status = reader.GetEnum<HomeworkSubmissionStatus>("Status"),
                Comments = reader.GetString("Comments"),
                StoredFileName = reader.GetNullableString("StoredFileName"),
                TeacherFeedback = reader.GetNullableString("TeacherFeedback"),
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
            INSERT INTO "HomeworkSubmissions"
                ("HomeworkId", "StudentId", "SubmittedDate", "Status", "Comments", "FileName", "StoredFileName", "ContentType", "FileSize")
            VALUES (@homeworkId, @studentId, @submittedDate, @status, @comments, @fileName, @storedFileName, @contentType, @fileSize)
            RETURNING "Id"
            """,
            p =>
            {
                Bind(p, submission);
                p.AddWithValue("fileName", (object?)submission.FileName ?? DBNull.Value);
                p.AddWithValue("storedFileName", (object?)submission.StoredFileName ?? DBNull.Value);
                p.AddWithValue("contentType", (object?)submission.ContentType ?? DBNull.Value);
                p.AddWithValue("fileSize", (object?)submission.FileSize ?? DBNull.Value);
            });

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

    // Resubmission with a replacement file — the controller deletes the previous stored
    // file from IFileStorage after this succeeds.
    public async Task UpdateWithFileAsync(int id, DateTime submittedDate, HomeworkSubmissionStatus status, string comments, string fileName, string storedFileName, string contentType, long fileSize) =>
        await db.ExecuteAsync(
            """
            UPDATE "HomeworkSubmissions"
            SET "SubmittedDate" = @submittedDate, "Status" = @status, "Comments" = @comments,
                "FileName" = @fileName, "StoredFileName" = @storedFileName, "ContentType" = @contentType, "FileSize" = @fileSize
            WHERE "Id" = @id
            """,
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("submittedDate", submittedDate);
                p.AddWithValue("status", status.ToString());
                p.AddWithValue("comments", comments);
                p.AddWithValue("fileName", fileName);
                p.AddWithValue("storedFileName", storedFileName);
                p.AddWithValue("contentType", contentType);
                p.AddWithValue("fileSize", fileSize);
            });

    public async Task UpdateFeedbackAsync(int id, string teacherFeedback) =>
        await db.ExecuteAsync(
            """UPDATE "HomeworkSubmissions" SET "TeacherFeedback" = @feedback, "FeedbackAtUtc" = now() WHERE "Id" = @id""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("feedback", teacherFeedback);
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
