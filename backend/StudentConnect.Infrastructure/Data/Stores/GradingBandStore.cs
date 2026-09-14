using Npgsql;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class GradingBandStore(Database db)
{
    // Matches the previous hardcoded StudentConnect.Domain.ResultCategory thresholds exactly —
    // used both as the fallback below and as the starting point an Admin edits from.
    public static readonly (string Label, double MinPercentage)[] DefaultBands =
    [
        ("Excellent", 90), ("Very Good", 75), ("Good", 60), ("Needs Improvement", 40), ("Needs Support", 0),
    ];

    private static GradingBand Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        SchoolId = reader.GetInt32("SchoolId"),
        Label = reader.GetString("Label"),
        MinPercentage = reader.GetDouble("MinPercentage"),
    };

    // Falls back to the built-in defaults (as transient, unsaved rows) if the school hasn't
    // customized its bands yet — so the admin UI always has something sensible to edit from.
    public async Task<List<GradingBand>> GetForSchoolAsync(int schoolId)
    {
        var bands = await db.QueryAsync(
            """SELECT "Id", "SchoolId", "Label", "MinPercentage" FROM "GradingBands" WHERE "SchoolId" = @schoolId ORDER BY "MinPercentage" DESC""",
            p => p.AddWithValue("schoolId", schoolId),
            Map);

        if (bands.Count > 0)
        {
            return bands;
        }

        return DefaultBands
            .Select(b => new GradingBand { Id = 0, SchoolId = schoolId, Label = b.Label, MinPercentage = b.MinPercentage })
            .OrderByDescending(b => b.MinPercentage)
            .ToList();
    }

    public async Task ReplaceAllAsync(int schoolId, IReadOnlyList<(string Label, double MinPercentage)> bands)
    {
        await db.ExecuteAsync("""DELETE FROM "GradingBands" WHERE "SchoolId" = @schoolId""", p => p.AddWithValue("schoolId", schoolId));

        foreach (var (label, min) in bands)
        {
            await db.ExecuteAsync(
                """INSERT INTO "GradingBands" ("SchoolId", "Label", "MinPercentage") VALUES (@schoolId, @label, @min)""",
                p =>
                {
                    p.AddWithValue("schoolId", schoolId);
                    p.AddWithValue("label", label);
                    p.AddWithValue("min", min);
                });
        }
    }

    // Highest band whose MinPercentage the percentage still clears — bands must be ordered
    // MinPercentage DESC (as GetForSchoolAsync returns them) for the first match to be correct.
    public static string Resolve(IReadOnlyList<GradingBand> bands, double percentage) =>
        bands.FirstOrDefault(b => percentage >= b.MinPercentage)?.Label
            ?? DefaultBands.First(b => percentage >= b.MinPercentage).Label;
}
