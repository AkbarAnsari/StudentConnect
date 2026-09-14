namespace StudentConnect.Application;

public class GradingBandDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public double MinPercentage { get; set; }
}

public class GradingBandItemDto
{
    public string Label { get; set; } = string.Empty;
    public double MinPercentage { get; set; }
}

public class GradingBandReplaceDto
{
    public List<GradingBandItemDto> Bands { get; set; } = [];
}
