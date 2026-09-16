using StudentConnect.Domain;

namespace StudentConnect.Application;

public class SubjectCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class SubjectUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
