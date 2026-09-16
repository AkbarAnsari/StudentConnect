using Npgsql;
using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Data.Stores;

public class ReportTemplateStore(Database db)
{
    public async Task<List<ReportTemplateDto>> GetAllAsync(int schoolId) =>
        await db.QueryAsync(
            """SELECT "Id", "Name", "HeaderText", "FooterText", "PrimarySignatureLabel", "SecondarySignatureLabel", "IsDefault" FROM "ReportTemplates" WHERE "SchoolId" = @schoolId ORDER BY "IsDefault" DESC, "Name" """,
            p => p.AddWithValue("schoolId", schoolId), Map);

    public async Task<ReportTemplateDto?> GetByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            """SELECT "Id", "Name", "HeaderText", "FooterText", "PrimarySignatureLabel", "SecondarySignatureLabel", "IsDefault" FROM "ReportTemplates" WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            p => { p.AddWithValue("id", id); p.AddWithValue("schoolId", schoolId); }, Map);

    public async Task<int> SaveAsync(ReportTemplateSaveDto dto, int schoolId) =>
        await db.WithTransactionAsync(async (connection, transaction) =>
        {
            if (dto.IsDefault)
            {
                await using var clear = new NpgsqlCommand("""UPDATE "ReportTemplates" SET "IsDefault" = false WHERE "SchoolId" = @schoolId""", connection, transaction);
                clear.Parameters.AddWithValue("schoolId", schoolId); await clear.ExecuteNonQueryAsync();
            }
            await using var command = new NpgsqlCommand(dto.Id > 0
                ? """UPDATE "ReportTemplates" SET "Name"=@name,"HeaderText"=@header,"FooterText"=@footer,"PrimarySignatureLabel"=@primary,"SecondarySignatureLabel"=@secondary,"IsDefault"=@isDefault,"UpdatedAtUtc"=now() WHERE "Id"=@id AND "SchoolId"=@schoolId RETURNING "Id" """
                : """INSERT INTO "ReportTemplates" ("SchoolId","Name","HeaderText","FooterText","PrimarySignatureLabel","SecondarySignatureLabel","IsDefault") VALUES (@schoolId,@name,@header,@footer,@primary,@secondary,@isDefault) RETURNING "Id" """, connection, transaction);
            command.Parameters.AddWithValue("id", dto.Id); command.Parameters.AddWithValue("schoolId", schoolId); command.Parameters.AddWithValue("name", dto.Name.Trim());
            command.Parameters.AddWithValue("header", dto.HeaderText.Trim()); command.Parameters.AddWithValue("footer", dto.FooterText?.Trim() ?? string.Empty);
            command.Parameters.AddWithValue("primary", dto.PrimarySignatureLabel.Trim()); command.Parameters.AddWithValue("secondary", (object?)dto.SecondarySignatureLabel?.Trim() ?? DBNull.Value); command.Parameters.AddWithValue("isDefault", dto.IsDefault);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        });

    private static ReportTemplateDto Map(NpgsqlDataReader reader) => new()
    {
        Id=reader.GetInt32("Id"),Name=reader.GetString("Name"),HeaderText=reader.GetString("HeaderText"),FooterText=reader.GetString("FooterText"),
        PrimarySignatureLabel=reader.GetString("PrimarySignatureLabel"),SecondarySignatureLabel=reader.GetNullableString("SecondarySignatureLabel"),IsDefault=reader.GetBoolean("IsDefault")
    };
}