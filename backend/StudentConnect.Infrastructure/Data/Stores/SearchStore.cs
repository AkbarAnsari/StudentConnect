using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

// Cross-entity full-text search using Postgres's built-in to_tsvector/plainto_tsquery —
// computed at query time (no persisted tsvector column or GIN index yet; fine at this data
// volume, and avoids the schema-migration risk of adding generated columns to five tables
// for a feature that hasn't proven it needs the extra performance).
public class SearchStore(Database db)
{
    private static SearchResultDto Map(NpgsqlDataReader reader) => new()
    {
        EntityType = reader.GetString("EntityType"),
        EntityId = reader.GetInt32("EntityId"),
        Title = reader.GetString("Title"),
        Subtitle = reader.GetString("Subtitle"),
    };

    public async Task<List<SearchResultDto>> SearchAsync(string query, int take, int schoolId) =>
        await db.QueryAsync(
            """
            SELECT "EntityType", "EntityId", "Title", "Subtitle" FROM (
                SELECT
                    'Student' AS "EntityType", s."Id" AS "EntityId",
                    s."FirstName" || ' ' || s."LastName" AS "Title",
                    s."StudentId" || ' · ' || g."Name" AS "Subtitle",
                    ts_rank(to_tsvector('english', s."FirstName" || ' ' || s."LastName" || ' ' || s."StudentId" || ' ' || s."Email"), plainto_tsquery('english', @query)) AS "Rank"
                FROM "Students" s
                JOIN "Grades" g ON g."Id" = s."GradeId"
                                WHERE s."SchoolId" = @schoolId
                                    AND to_tsvector('english', s."FirstName" || ' ' || s."LastName" || ' ' || s."StudentId" || ' ' || s."Email") @@ plainto_tsquery('english', @query)

                UNION ALL

                SELECT
                    'Teacher', t."Id",
                    t."FirstName" || ' ' || t."LastName",
                    t."TeacherId" || ' · ' || t."Email",
                    ts_rank(to_tsvector('english', t."FirstName" || ' ' || t."LastName" || ' ' || t."TeacherId" || ' ' || t."Email"), plainto_tsquery('english', @query))
                FROM "Teachers" t
                                WHERE t."SchoolId" = @schoolId
                                    AND to_tsvector('english', t."FirstName" || ' ' || t."LastName" || ' ' || t."TeacherId" || ' ' || t."Email") @@ plainto_tsquery('english', @query)

                UNION ALL

                SELECT
                    'Homework', h."Id", h."Title",
                    'Due ' || h."DueDate"::text,
                    ts_rank(to_tsvector('english', h."Title" || ' ' || h."Description"), plainto_tsquery('english', @query))
                FROM "Homework" h
                                JOIN "Grades" hg ON hg."Id" = h."GradeId"
                                WHERE hg."SchoolId" = @schoolId
                                    AND to_tsvector('english', h."Title" || ' ' || h."Description") @@ plainto_tsquery('english', @query)

                UNION ALL

                SELECT
                    'LearningMaterial', m."Id", m."Title", m."FileName",
                    ts_rank(to_tsvector('english', m."Title" || ' ' || m."Description"), plainto_tsquery('english', @query))
                FROM "LearningMaterials" m
                                JOIN "Grades" mg ON mg."Id" = m."GradeId"
                                WHERE mg."SchoolId" = @schoolId
                                    AND to_tsvector('english', m."Title" || ' ' || m."Description") @@ plainto_tsquery('english', @query)

                UNION ALL

                SELECT
                    'Activity', a."Id", a."Title",
                    a."Location" || ' · ' || a."Date"::date::text,
                    ts_rank(to_tsvector('english', a."Title" || ' ' || a."Description"), plainto_tsquery('english', @query))
                FROM "Activities" a
                                JOIN "Grades" ag ON ag."Id" = a."GradeId"
                                WHERE ag."SchoolId" = @schoolId
                                    AND to_tsvector('english', a."Title" || ' ' || a."Description") @@ plainto_tsquery('english', @query)
            ) results
            ORDER BY "Rank" DESC
            LIMIT @take
            """,
            p =>
            {
                p.AddWithValue("query", query);
                p.AddWithValue("take", take);
                p.AddWithValue("schoolId", schoolId);
            },
            Map);
}
