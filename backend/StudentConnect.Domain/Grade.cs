using System.Text.Json.Serialization;

namespace StudentConnect.Domain;

// A Grade (e.g. "Grade 8") can have many Students — the "one" side of the relationship.
public class Grade
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SchoolId { get; set; }

    [JsonIgnore]
    public List<Student> Students { get; set; } = [];
}
