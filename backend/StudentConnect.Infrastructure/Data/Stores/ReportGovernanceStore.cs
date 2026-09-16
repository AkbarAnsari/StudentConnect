using Npgsql;
using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Data.Stores;

public class ReportGovernanceStore(Database db)
{
    public async Task AddSignatureAsync(int issuanceId, int schoolId, string label, string signedName, int userId) =>
        await db.ExecuteAsync(
            """INSERT INTO "ReportSignatures" ("ReportIssuanceId","Label","SignedName","SignedByUserId") SELECT ri."Id",@label,@signedName,@userId FROM "ReportIssuances" ri WHERE ri."Id"=@issuanceId AND ri."SchoolId"=@schoolId ON CONFLICT ("ReportIssuanceId","Label") DO UPDATE SET "SignedName"=EXCLUDED."SignedName","SignedByUserId"=EXCLUDED."SignedByUserId","SignedAtUtc"=now()""",
            p=>{p.AddWithValue("issuanceId",issuanceId);p.AddWithValue("schoolId",schoolId);p.AddWithValue("label",label);p.AddWithValue("signedName",signedName);p.AddWithValue("userId",userId);});

    public async Task RecordDownloadAsync(int issuanceId, int schoolId, int userId) =>
        await db.ExecuteAsync("""INSERT INTO "ReportDownloadReceipts" ("ReportIssuanceId","DownloadedByUserId") SELECT "Id",@userId FROM "ReportIssuances" WHERE "Id"=@issuanceId AND "SchoolId"=@schoolId""",
            p=>{p.AddWithValue("issuanceId",issuanceId);p.AddWithValue("schoolId",schoolId);p.AddWithValue("userId",userId);});

    public async Task<int> CreateTranscriptAsync(int schoolId,int studentId,string snapshotJson,int userId)=>
        await db.ExecuteScalarAsync<int>("""INSERT INTO "TranscriptIssuances" ("SchoolId","StudentId","SnapshotJson","IssuedByUserId") VALUES (@schoolId,@studentId,CAST(@snapshot AS jsonb),@userId) RETURNING "Id" """,
            p=>{p.AddWithValue("schoolId",schoolId);p.AddWithValue("studentId",studentId);p.AddWithValue("snapshot",snapshotJson);p.AddWithValue("userId",userId);});

    public async Task<List<TranscriptIssuanceSummaryDto>> GetTranscriptHistoryAsync(int studentId,int schoolId)=>
        await db.QueryAsync("""SELECT ti."Id",ti."StudentId",ti."IssuedAtUtc",u."Username" AS "IssuedByName" FROM "TranscriptIssuances" ti JOIN "Users" u ON u."Id"=ti."IssuedByUserId" WHERE ti."StudentId"=@studentId AND ti."SchoolId"=@schoolId ORDER BY ti."IssuedAtUtc" DESC""",
            p=>{p.AddWithValue("studentId",studentId);p.AddWithValue("schoolId",schoolId);},reader=>new TranscriptIssuanceSummaryDto{Id=reader.GetInt32("Id"),StudentId=reader.GetInt32("StudentId"),IssuedAtUtc=reader.GetDateTimeUtc("IssuedAtUtc"),IssuedByName=reader.GetString("IssuedByName")});

    public async Task<string?> GetTranscriptSnapshotAsync(int id,int studentId,int schoolId)=>
        await db.ExecuteScalarAsync<string>("""SELECT "SnapshotJson"::text FROM "TranscriptIssuances" WHERE "Id"=@id AND "StudentId"=@studentId AND "SchoolId"=@schoolId""",
            p=>{p.AddWithValue("id",id);p.AddWithValue("studentId",studentId);p.AddWithValue("schoolId",schoolId);});
}