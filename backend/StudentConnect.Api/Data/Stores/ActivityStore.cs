using Npgsql;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Data.Stores;

public class ActivityStore(Database db)
{
    private const string SelectWithGrade = """
        SELECT a."Id", a."Title", a."Description", a."Date", a."Location", a."GradeId", a."Capacity", a."CreatedDate", g."Name" AS "GradeName"
        FROM "Activities" a
        JOIN "Grades" g ON g."Id" = a."GradeId"
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
        Capacity = reader.GetInt32("Capacity"),
        CreatedDate = reader.GetDateTimeUtc("CreatedDate"),
    };

    public async Task<List<Activity>> GetAllAsync() =>
        await db.QueryAsync($"""{SelectWithGrade} ORDER BY a."Date" """, bind: null, map: Map);

    public async Task<Activity?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{SelectWithGrade} WHERE a."Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task<List<Activity>> GetByGradeAsync(int gradeId) =>
        await db.QueryAsync($"""{SelectWithGrade} WHERE a."GradeId" = @gradeId ORDER BY a."Date" """, p => p.AddWithValue("gradeId", gradeId), Map);

    public async Task<int> CreateAsync(Activity activity) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Activities" ("Title", "Description", "Date", "Location", "GradeId", "Capacity", "CreatedDate")
            VALUES (@title, @description, @date, @location, @gradeId, @capacity, @createdDate)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("title", activity.Title);
                p.AddWithValue("description", activity.Description);
                p.AddWithValue("date", activity.Date);
                p.AddWithValue("location", activity.Location);
                p.AddWithValue("gradeId", activity.GradeId);
                p.AddWithValue("capacity", activity.Capacity);
                p.AddWithValue("createdDate", activity.CreatedDate);
            });

    public async Task UpdateAsync(int id, string title, string description, DateTime date, string location, int capacity) =>
        await db.ExecuteAsync(
            """
            UPDATE "Activities"
            SET "Title" = @title, "Description" = @description, "Date" = @date, "Location" = @location, "Capacity" = @capacity
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
            });

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Activities" WHERE "Id" = @id""", p => p.AddWithValue("id", id));
}
