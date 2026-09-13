using System.Text.Json.Serialization;

namespace StudentConnect.Domain;

// A Subject (e.g. "Mathematics") studied by many students — many-to-many via StudentSubject.
public class Subject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int SchoolId { get; set; }

    [JsonIgnore]
    public List<StudentSubject> StudentSubjects { get; set; } = [];
}
