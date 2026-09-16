using Npgsql;
using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Data.Stores;

public sealed record ReportIssuanceRecord(ReportIssuanceSummaryDto Summary, string SnapshotJson);

public class ReportIssuanceStore(Database db)
{
    public async Task<List<ReportPeriodDto>> GetPeriodsAsync(int schoolId) =>
        await db.QueryAsync(
            """
            SELECT t."Id", t."AcademicYearId", ay."Name" AS "AcademicYearName", t."Name", t."StartDate", t."EndDate", t."IsLocked", ay."IsCurrent"
            FROM "Terms" t
            JOIN "AcademicYears" ay ON ay."Id" = t."AcademicYearId"
            WHERE ay."SchoolId" = @schoolId AND NOT ay."IsArchived"
            ORDER BY ay."StartDate" DESC, t."StartDate" DESC
            """,
            p => p.AddWithValue("schoolId", schoolId),
            MapPeriod);

    public async Task<ReportPeriodDto?> GetPeriodAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT t."Id", t."AcademicYearId", ay."Name" AS "AcademicYearName", t."Name", t."StartDate", t."EndDate", t."IsLocked", ay."IsCurrent"
            FROM "Terms" t JOIN "AcademicYears" ay ON ay."Id" = t."AcademicYearId"
            WHERE t."Id" = @id AND ay."SchoolId" = @schoolId
            """,
            p => { p.AddWithValue("id", id); p.AddWithValue("schoolId", schoolId); },
            MapPeriod);

    public async Task<ReportPeriodDto?> GetCurrentPeriodAsync(int schoolId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await db.QuerySingleOrDefaultAsync(
            """
            SELECT t."Id", t."AcademicYearId", ay."Name" AS "AcademicYearName", t."Name", t."StartDate", t."EndDate", t."IsLocked", ay."IsCurrent"
            FROM "Terms" t JOIN "AcademicYears" ay ON ay."Id" = t."AcademicYearId"
            WHERE ay."SchoolId" = @schoolId AND NOT ay."IsArchived" AND @today BETWEEN t."StartDate" AND t."EndDate"
            ORDER BY ay."IsCurrent" DESC, t."StartDate" DESC LIMIT 1
            """,
            p => { p.AddWithValue("schoolId", schoolId); p.AddWithValue("today", today); },
            MapPeriod);
    }

    public async Task<int> CreateAsync(int schoolId, int studentId, ReportPeriodDto period, string snapshotJson, string? comments, int issuedByUserId, int? templateId = null) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "ReportIssuances" ("SchoolId", "StudentId", "TermId", "PeriodLabel", "PeriodStartDate", "PeriodEndDate", "SnapshotJson", "Comments", "IssuedByUserId", "TemplateId")
            VALUES (@schoolId, @studentId, @termId, @periodLabel, @periodStartDate, @periodEndDate, CAST(@snapshotJson AS jsonb), @comments, @issuedByUserId, @templateId)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("schoolId", schoolId);
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("termId", period.Id);
                p.AddWithValue("periodLabel", period.Label);
                p.AddWithValue("periodStartDate", period.StartDate);
                p.AddWithValue("periodEndDate", period.EndDate);
                p.AddWithValue("snapshotJson", snapshotJson);
                p.AddWithValue("comments", (object?)comments ?? DBNull.Value);
                p.AddWithValue("issuedByUserId", issuedByUserId);
                p.AddWithValue("templateId", (object?)templateId ?? DBNull.Value);
            });

    public async Task<ReportIssuanceRecord?> GetLatestAsync(int studentId, int? termId, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT ri."Id", ri."StudentId", ri."TermId", ri."PeriodLabel", ri."PeriodStartDate", ri."PeriodEndDate", ri."SnapshotJson"::text AS "SnapshotJson",
                     ri."Comments", ri."IssuedByUserId", u."Username" AS "IssuedByName", ri."IssuedAtUtc", rt."Name" AS "TemplateName",
                     (SELECT COUNT(*) FROM "ReportSignatures" rs WHERE rs."ReportIssuanceId"=ri."Id") AS "SignatureCount",
                     (SELECT MAX("DownloadedAtUtc") FROM "ReportDownloadReceipts" rr WHERE rr."ReportIssuanceId"=ri."Id") AS "LastDownloadedAtUtc"
                 FROM "ReportIssuances" ri JOIN "Users" u ON u."Id" = ri."IssuedByUserId" LEFT JOIN "ReportTemplates" rt ON rt."Id"=ri."TemplateId"
            WHERE ri."StudentId" = @studentId AND ri."SchoolId" = @schoolId AND (@termId IS NULL OR ri."TermId" = @termId)
            ORDER BY ri."IssuedAtUtc" DESC LIMIT 1
            """,
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("schoolId", schoolId);
                p.Add("termId", NpgsqlTypes.NpgsqlDbType.Integer).Value = termId is { } id ? id : DBNull.Value;
            },
            MapRecord);

    public async Task<ReportIssuanceRecord?> GetByIdAsync(int id, int studentId, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT ri."Id", ri."StudentId", ri."TermId", ri."PeriodLabel", ri."PeriodStartDate", ri."PeriodEndDate", ri."SnapshotJson"::text AS "SnapshotJson",
                     ri."Comments", ri."IssuedByUserId", u."Username" AS "IssuedByName", ri."IssuedAtUtc", rt."Name" AS "TemplateName",
                     (SELECT COUNT(*) FROM "ReportSignatures" rs WHERE rs."ReportIssuanceId"=ri."Id") AS "SignatureCount",
                     (SELECT MAX("DownloadedAtUtc") FROM "ReportDownloadReceipts" rr WHERE rr."ReportIssuanceId"=ri."Id") AS "LastDownloadedAtUtc"
                 FROM "ReportIssuances" ri JOIN "Users" u ON u."Id" = ri."IssuedByUserId" LEFT JOIN "ReportTemplates" rt ON rt."Id"=ri."TemplateId"
            WHERE ri."Id" = @id AND ri."StudentId" = @studentId AND ri."SchoolId" = @schoolId
            """,
            p => { p.AddWithValue("id", id); p.AddWithValue("studentId", studentId); p.AddWithValue("schoolId", schoolId); },
            MapRecord);

    public async Task<List<ReportIssuanceSummaryDto>> GetHistoryAsync(int studentId, int schoolId) =>
        await db.QueryAsync(
            """
            SELECT ri."Id", ri."StudentId", ri."TermId", ri."PeriodLabel", ri."PeriodStartDate", ri."PeriodEndDate", ri."SnapshotJson"::text AS "SnapshotJson",
                     ri."Comments", ri."IssuedByUserId", u."Username" AS "IssuedByName", ri."IssuedAtUtc", rt."Name" AS "TemplateName",
                     (SELECT COUNT(*) FROM "ReportSignatures" rs WHERE rs."ReportIssuanceId"=ri."Id") AS "SignatureCount",
                     (SELECT MAX("DownloadedAtUtc") FROM "ReportDownloadReceipts" rr WHERE rr."ReportIssuanceId"=ri."Id") AS "LastDownloadedAtUtc"
                 FROM "ReportIssuances" ri JOIN "Users" u ON u."Id" = ri."IssuedByUserId" LEFT JOIN "ReportTemplates" rt ON rt."Id"=ri."TemplateId"
            WHERE ri."StudentId" = @studentId AND ri."SchoolId" = @schoolId
            ORDER BY ri."IssuedAtUtc" DESC
            """,
            p => { p.AddWithValue("studentId", studentId); p.AddWithValue("schoolId", schoolId); },
            reader => MapRecord(reader).Summary);

    private static ReportPeriodDto MapPeriod(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        AcademicYearId = reader.GetInt32("AcademicYearId"),
        AcademicYearName = reader.GetString("AcademicYearName"),
        Name = reader.GetString("Name"),
        StartDate = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("StartDate")),
        EndDate = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("EndDate")),
        IsLocked = reader.GetBoolean("IsLocked"),
        IsCurrent = reader.GetBoolean("IsCurrent"),
    };

    private static ReportIssuanceRecord MapRecord(NpgsqlDataReader reader) => new(
        new ReportIssuanceSummaryDto
        {
            Id = reader.GetInt32("Id"),
            StudentId = reader.GetInt32("StudentId"),
            TermId = reader.GetInt32("TermId"),
            PeriodLabel = reader.GetString("PeriodLabel"),
            PeriodStartDate = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("PeriodStartDate")),
            PeriodEndDate = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("PeriodEndDate")),
            Comments = reader.GetNullableString("Comments"),
            IssuedByUserId = reader.GetInt32("IssuedByUserId"),
            IssuedByName = reader.GetString("IssuedByName"),
            IssuedAtUtc = reader.GetDateTimeUtc("IssuedAtUtc"),
            TemplateName = reader.GetNullableString("TemplateName"),
            SignatureCount = (int)reader.GetInt64("SignatureCount"),
            LastDownloadedAtUtc = reader.GetNullableDateTimeUtc("LastDownloadedAtUtc"),
        },
        reader.GetString("SnapshotJson"));
}