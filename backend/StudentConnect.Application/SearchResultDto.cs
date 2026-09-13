using StudentConnect.Domain;

namespace StudentConnect.Application;

// One row of a cross-entity search result — EntityType determines how the frontend
// icon/link is rendered (e.g. "Student" links to /students/{EntityId}).
public class SearchResultDto
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
}
