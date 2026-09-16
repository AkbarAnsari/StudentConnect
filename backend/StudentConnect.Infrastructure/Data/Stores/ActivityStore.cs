using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class ActivityStore(Database db)
{
    private const string SelectWithGrade = """
         SELECT a."Id", a."Title", a."Description", a."Date", a."Location", a."GradeId", a."SectionId", a."Capacity", a."CreatedDate",
             a."OwnerUserId", a."OrganizerName", a."ContactEmail", a."RegistrationOpensAtUtc", a."RegistrationClosesAtUtc", a."ConsentRequired", a."FeeAmount",
             a."ModerationStatus", a."ModerationReason", a."ModeratedByUserId", a."ModeratedAtUtc", a."Status", a."CancelledAtUtc", a."CompletedAtUtc",
             g."Name" AS "GradeName", sec."Name" AS "SectionName"
        FROM "Activities" a
        JOIN "Grades" g ON g."Id" = a."GradeId"
         LEFT JOIN "Sections" sec ON sec."Id" = a."SectionId"
        """;

    private static Activity Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Title = reader.GetString("Title"),
        Description = reader.GetString("Description"),
        Date = reader.GetDateTimeUtc("Date"),
        Location = reader.GetString("Location"),
        GradeId = reader.GetInt32("GradeId"),
        Grade = new Grade { Name = reader.GetString("GradeName") },
        SectionId = reader.GetNullableInt32("SectionId"),
        Section = reader.GetNullableInt32("SectionId") is { } sectionId ? new Section { Id = sectionId, Name = reader.GetString("SectionName") } : null,
        Capacity = reader.GetInt32("Capacity"),
        CreatedDate = reader.GetDateTimeUtc("CreatedDate"),
        OwnerUserId = reader.GetNullableInt32("OwnerUserId"),
        OrganizerName = reader.GetString("OrganizerName"),
        ContactEmail = reader.GetNullableString("ContactEmail"),
        RegistrationOpensAtUtc = reader.GetNullableDateTimeUtc("RegistrationOpensAtUtc"),
        RegistrationClosesAtUtc = reader.GetNullableDateTimeUtc("RegistrationClosesAtUtc"),
        ConsentRequired = reader.GetBoolean("ConsentRequired"),
        FeeAmount = reader.GetFieldValue<decimal?>(reader.GetOrdinal("FeeAmount")),
        ModerationStatus = reader.GetString("ModerationStatus"),
        ModerationReason = reader.GetNullableString("ModerationReason"),
        ModeratedByUserId = reader.GetNullableInt32("ModeratedByUserId"),
        ModeratedAtUtc = reader.GetNullableDateTimeUtc("ModeratedAtUtc"),
        Status = reader.GetEnum<ActivityStatus>("Status"),
        CancelledAtUtc = reader.GetNullableDateTimeUtc("CancelledAtUtc"),
        CompletedAtUtc = reader.GetNullableDateTimeUtc("CompletedAtUtc"),
    };

    public async Task<List<Activity>> GetAllAsync() =>
        await db.QueryAsync($"""{SelectWithGrade} ORDER BY a."Date" """, bind: null, map: Map);

    public async Task<List<Activity>> GetAllAsync(int schoolId) =>
        await db.QueryAsync(
            $"""{SelectWithGrade} WHERE g."SchoolId" = @schoolId ORDER BY a."Date" """,
            p => p.AddWithValue("schoolId", schoolId),
            Map);

    public async Task<Activity?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{SelectWithGrade} WHERE a."Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task<Activity?> GetByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            $"""{SelectWithGrade} WHERE a."Id" = @id AND g."SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            },
            Map);

    public async Task<List<Activity>> GetByGradeAsync(int gradeId) =>
        await db.QueryAsync($"""{SelectWithGrade} WHERE a."GradeId" = @gradeId ORDER BY a."Date" """, p => p.AddWithValue("gradeId", gradeId), Map);

    public async Task<List<Activity>> GetByGradeAsync(int gradeId, int schoolId) =>
        await db.QueryAsync(
            $"""{SelectWithGrade} WHERE a."GradeId" = @gradeId AND g."SchoolId" = @schoolId AND a."Status" = 'Open' ORDER BY a."Date" """,
            p =>
            {
                p.AddWithValue("gradeId", gradeId);
                p.AddWithValue("schoolId", schoolId);
            },
            Map);

    public async Task<int> CreateAsync(Activity activity) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Activities" ("Title", "Description", "Date", "Location", "GradeId", "SectionId", "Capacity", "CreatedDate", "OwnerUserId", "OrganizerName", "ContactEmail", "RegistrationOpensAtUtc", "RegistrationClosesAtUtc", "ConsentRequired", "FeeAmount", "ModerationStatus", "Status")
            VALUES (@title, @description, @date, @location, @gradeId, @sectionId, @capacity, @createdDate, @ownerUserId, @organizerName, @contactEmail, @registrationOpensAtUtc, @registrationClosesAtUtc, @consentRequired, @feeAmount, @moderationStatus, @status)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("title", activity.Title);
                p.AddWithValue("description", activity.Description);
                p.AddWithValue("date", activity.Date);
                p.AddWithValue("location", activity.Location);
                p.AddWithValue("gradeId", activity.GradeId);
                p.AddWithValue("sectionId", (object?)activity.SectionId ?? DBNull.Value);
                p.AddWithValue("capacity", activity.Capacity);
                p.AddWithValue("createdDate", activity.CreatedDate);
                p.AddWithValue("ownerUserId", (object?)activity.OwnerUserId ?? DBNull.Value);
                p.AddWithValue("organizerName", activity.OrganizerName);
                p.AddWithValue("contactEmail", (object?)activity.ContactEmail ?? DBNull.Value);
                p.AddWithValue("registrationOpensAtUtc", (object?)activity.RegistrationOpensAtUtc ?? DBNull.Value);
                p.AddWithValue("registrationClosesAtUtc", (object?)activity.RegistrationClosesAtUtc ?? DBNull.Value);
                p.AddWithValue("consentRequired", activity.ConsentRequired);
                p.AddWithValue("feeAmount", (object?)activity.FeeAmount ?? DBNull.Value);
                p.AddWithValue("moderationStatus", activity.ModerationStatus);
                p.AddWithValue("status", activity.Status.ToString());
            });

    public async Task UpdateAsync(int id, string title, string description, DateTime date, string location, int capacity, int? sectionId, string organizerName, string? contactEmail, DateTime? registrationOpensAtUtc, DateTime? registrationClosesAtUtc, bool consentRequired, decimal? feeAmount) =>
        await db.ExecuteAsync(
            """
            UPDATE "Activities"
            SET "Title" = @title, "Description" = @description, "Date" = @date, "Location" = @location, "Capacity" = @capacity,
                "SectionId" = @sectionId, "OrganizerName" = @organizerName, "ContactEmail" = @contactEmail,
                "RegistrationOpensAtUtc" = @registrationOpensAtUtc, "RegistrationClosesAtUtc" = @registrationClosesAtUtc,
                "ConsentRequired" = @consentRequired, "FeeAmount" = @feeAmount,
                "ModerationStatus" = CASE WHEN "ModerationStatus" = 'Approved' THEN 'Pending' ELSE "ModerationStatus" END
            WHERE "Id" = @id
            """,
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("title", title);
                p.AddWithValue("description", description);
                p.AddWithValue("date", date);
                p.AddWithValue("location", location);
                p.AddWithValue("capacity", capacity);
                p.AddWithValue("sectionId", (object?)sectionId ?? DBNull.Value);
                p.AddWithValue("organizerName", organizerName);
                p.AddWithValue("contactEmail", (object?)contactEmail ?? DBNull.Value);
                p.AddWithValue("registrationOpensAtUtc", (object?)registrationOpensAtUtc ?? DBNull.Value);
                p.AddWithValue("registrationClosesAtUtc", (object?)registrationClosesAtUtc ?? DBNull.Value);
                p.AddWithValue("consentRequired", consentRequired);
                p.AddWithValue("feeAmount", (object?)feeAmount ?? DBNull.Value);
            });

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Activities" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task SetStatusAsync(int id, ActivityStatus status) =>
        await db.ExecuteAsync(
            """UPDATE "Activities" SET "Status" = @status, "CancelledAtUtc" = CASE WHEN @status = 'Cancelled' THEN now() ELSE "CancelledAtUtc" END, "CompletedAtUtc" = CASE WHEN @status = 'Completed' THEN now() ELSE "CompletedAtUtc" END WHERE "Id" = @id""",
            p => { p.AddWithValue("id", id); p.AddWithValue("status", status.ToString()); });

    public async Task SetModerationAsync(int id, string status, string? reason, int userId) =>
        await db.WithTransactionAsync(async (connection, transaction) =>
        {
            await using var update = new NpgsqlCommand(
                """UPDATE "Activities" SET "ModerationStatus" = @status, "ModerationReason" = @reason, "ModeratedByUserId" = @userId, "ModeratedAtUtc" = now(), "Status" = CASE WHEN @status = 'Approved' THEN "Status" ELSE 'Draft' END WHERE "Id" = @id""",
                connection, transaction);
            update.Parameters.AddWithValue("id", id); update.Parameters.AddWithValue("status", status);
            update.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value); update.Parameters.AddWithValue("userId", userId);
            await update.ExecuteNonQueryAsync();
            await using var history = new NpgsqlCommand(
                """INSERT INTO "ActivityModerationHistory" ("ActivityId", "Action", "Reason", "ChangedByUserId") VALUES (@id, @status, @reason, @userId)""",
                connection, transaction);
            history.Parameters.AddWithValue("id", id); history.Parameters.AddWithValue("status", status);
            history.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value); history.Parameters.AddWithValue("userId", userId);
            await history.ExecuteNonQueryAsync();
            return 0;
        });
}
