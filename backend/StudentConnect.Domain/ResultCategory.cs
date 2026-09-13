namespace StudentConnect.Domain;

// Demonstration-only result labels — not an official academic grading system.
// Thresholds live in exactly one place instead of being scattered as magic numbers.
public static class ResultCategory
{
    public static string From(double percentage) => percentage switch
    {
        >= 90 => "Excellent",
        >= 75 => "Very Good",
        >= 60 => "Good",
        >= 40 => "Needs Improvement",
        _ => "Needs Support",
    };
}
