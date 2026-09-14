using Npgsql;

namespace StudentConnect.Infrastructure.Data;

// Column-name-based reads on top of NpgsqlDataReader's ordinal-based API — every Store's
// mapping function reads rows through these instead of magic-number column indexes.
public static class DataReaderExtensions
{
    public static int GetInt32(this NpgsqlDataReader reader, string column) => reader.GetInt32(reader.GetOrdinal(column));

    public static int? GetNullableInt32(this NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    public static long GetInt64(this NpgsqlDataReader reader, string column) => reader.GetInt64(reader.GetOrdinal(column));

    public static long? GetNullableInt64(this NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    public static double GetDouble(this NpgsqlDataReader reader, string column) => reader.GetDouble(reader.GetOrdinal(column));

    public static string GetString(this NpgsqlDataReader reader, string column) => reader.GetString(reader.GetOrdinal(column));

    public static string? GetNullableString(this NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static bool GetBoolean(this NpgsqlDataReader reader, string column) => reader.GetBoolean(reader.GetOrdinal(column));

    public static DateOnly GetDateOnly(this NpgsqlDataReader reader, string column) => reader.GetFieldValue<DateOnly>(reader.GetOrdinal(column));

    public static DateOnly? GetNullableDateOnly(this NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateOnly>(ordinal);
    }

    public static DateTime GetDateTimeUtc(this NpgsqlDataReader reader, string column) => reader.GetFieldValue<DateTime>(reader.GetOrdinal(column));

    public static DateTime? GetNullableDateTimeUtc(this NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTime>(ordinal);
    }

    public static T GetEnum<T>(this NpgsqlDataReader reader, string column) where T : struct, Enum =>
        Enum.Parse<T>(reader.GetString(column));
}
