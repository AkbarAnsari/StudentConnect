namespace StudentConnect.Domain;

// A school-configurable result label threshold (e.g. "Excellent" at 90%). Replaces the
// hardcoded ResultCategory bands on a per-school basis — see GradingBandStore.Resolve.
public class GradingBand
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string Label { get; set; } = string.Empty;
    public double MinPercentage { get; set; }
}
