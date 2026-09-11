using System.Text.Json.Serialization;

namespace StudentConnect.Api.Models;

// A Subject (e.g. "Mathematics") studied by many students — many-to-many via StudentSubject.
public class Subject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    [JsonIgnore]
    public List<StudentSubject> StudentSubjects { get; set; } = [];
}
