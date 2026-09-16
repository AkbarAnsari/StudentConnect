using Npgsql;
using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Data.Stores;

public sealed record OperationalSettingsRow(int SchoolId, OperationalSettingsDto Settings);

public class OperationalSettingsStore(Database db)
{
    public async Task<OperationalSettingsDto> GetAsync(int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            """SELECT "AlertEmail","AlertWebhookUrl","FailedEmailThreshold","AuditRetentionDays","NotificationRetentionDays","RedactAuditDetails","RunbookUrl" FROM "OperationalSettings" WHERE "SchoolId"=@schoolId""",
            p=>p.AddWithValue("schoolId",schoolId),Map)??new OperationalSettingsDto();

    public async Task SaveAsync(int schoolId,OperationalSettingsDto dto)=>
        await db.ExecuteAsync("""INSERT INTO "OperationalSettings" ("SchoolId","AlertEmail","AlertWebhookUrl","FailedEmailThreshold","AuditRetentionDays","NotificationRetentionDays","RedactAuditDetails","RunbookUrl") VALUES (@schoolId,@email,@webhook,@threshold,@auditDays,@notificationDays,@redact,@runbook) ON CONFLICT ("SchoolId") DO UPDATE SET "AlertEmail"=EXCLUDED."AlertEmail","AlertWebhookUrl"=EXCLUDED."AlertWebhookUrl","FailedEmailThreshold"=EXCLUDED."FailedEmailThreshold","AuditRetentionDays"=EXCLUDED."AuditRetentionDays","NotificationRetentionDays"=EXCLUDED."NotificationRetentionDays","RedactAuditDetails"=EXCLUDED."RedactAuditDetails","RunbookUrl"=EXCLUDED."RunbookUrl","UpdatedAtUtc"=now()""",
            p=>Bind(p,schoolId,dto));

    public async Task<List<OperationalSettingsRow>> GetAllAsync()=>await db.QueryAsync("""SELECT "SchoolId","AlertEmail","AlertWebhookUrl","FailedEmailThreshold","AuditRetentionDays","NotificationRetentionDays","RedactAuditDetails","RunbookUrl" FROM "OperationalSettings" """,null,r=>new OperationalSettingsRow(r.GetInt32("SchoolId"),Map(r)));

    public async Task<(int AuditRows,int NotificationRows)> ApplyRetentionAsync(int schoolId,OperationalSettingsDto settings)=>
        await db.WithTransactionAsync(async(connection,transaction)=>
        {
            await using var audit=new NpgsqlCommand(settings.RedactAuditDetails
                ?"""UPDATE "AuditLogs" a SET "Details"='[REDACTED BY RETENTION POLICY]' WHERE a."TimestampUtc"<now()-make_interval(days=>@days) AND a."Details" IS DISTINCT FROM '[REDACTED BY RETENTION POLICY]' AND EXISTS(SELECT 1 FROM "Users" u WHERE u."Id"=a."UserId" AND u."SchoolId"=@schoolId)"""
                :"""DELETE FROM "AuditLogs" a WHERE a."TimestampUtc"<now()-make_interval(days=>@days) AND EXISTS(SELECT 1 FROM "Users" u WHERE u."Id"=a."UserId" AND u."SchoolId"=@schoolId)""",connection,transaction);
            audit.Parameters.AddWithValue("days",settings.AuditRetentionDays);audit.Parameters.AddWithValue("schoolId",schoolId);var auditRows=await audit.ExecuteNonQueryAsync();
            await using var notifications=new NpgsqlCommand("""DELETE FROM "Notifications" n WHERE n."CreatedAtUtc"<now()-make_interval(days=>@days) AND EXISTS(SELECT 1 FROM "Users" u WHERE u."Id"=n."UserId" AND u."SchoolId"=@schoolId)""",connection,transaction);
            notifications.Parameters.AddWithValue("days",settings.NotificationRetentionDays);notifications.Parameters.AddWithValue("schoolId",schoolId);var notificationRows=await notifications.ExecuteNonQueryAsync();
            return(auditRows,notificationRows);
        });

    public async Task<bool> ShouldAlertAsync(int schoolId)=>!await db.ExistsAsync("""SELECT 1 FROM "OperationalAlertLog" WHERE "SchoolId"=@schoolId AND "CreatedAtUtc">now()-interval '1 hour'""",p=>p.AddWithValue("schoolId",schoolId));
    public async Task LogAlertAsync(int schoolId,string destination,string message,bool succeeded,string? error)=>await db.ExecuteAsync("""INSERT INTO "OperationalAlertLog" ("SchoolId","Destination","Message","Succeeded","Error") VALUES (@schoolId,@destination,@message,@succeeded,@error)""",p=>{p.AddWithValue("schoolId",schoolId);p.AddWithValue("destination",destination);p.AddWithValue("message",message);p.AddWithValue("succeeded",succeeded);p.AddWithValue("error",(object?)error??DBNull.Value);});
    private static OperationalSettingsDto Map(NpgsqlDataReader r)=>new(){AlertEmail=r.GetNullableString("AlertEmail"),AlertWebhookUrl=r.GetNullableString("AlertWebhookUrl"),FailedEmailThreshold=r.GetInt32("FailedEmailThreshold"),AuditRetentionDays=r.GetInt32("AuditRetentionDays"),NotificationRetentionDays=r.GetInt32("NotificationRetentionDays"),RedactAuditDetails=r.GetBoolean("RedactAuditDetails"),RunbookUrl=r.GetNullableString("RunbookUrl")};
    private static void Bind(NpgsqlParameterCollection p,int schoolId,OperationalSettingsDto d){p.AddWithValue("schoolId",schoolId);p.AddWithValue("email",(object?)d.AlertEmail??DBNull.Value);p.AddWithValue("webhook",(object?)d.AlertWebhookUrl??DBNull.Value);p.AddWithValue("threshold",d.FailedEmailThreshold);p.AddWithValue("auditDays",d.AuditRetentionDays);p.AddWithValue("notificationDays",d.NotificationRetentionDays);p.AddWithValue("redact",d.RedactAuditDetails);p.AddWithValue("runbook",(object?)d.RunbookUrl??DBNull.Value);}
}