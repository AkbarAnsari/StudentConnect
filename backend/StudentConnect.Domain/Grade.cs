using System.Text.Json.Serialization;

namespace StudentConnect.Domain;

// A Grade (e.g. "Grade 8") can have many Students — the "one" side of the relationship.
public class Grade
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SchoolId { get; set; }

    // Determines "the next grade" for academic-year rollover; null falls back to Id order.
    public int? SequenceOrder { get; set; }

    [JsonIgnore]
    public List<Student> Students { get; set; } = [];
}
