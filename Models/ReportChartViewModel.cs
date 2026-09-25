namespace FoodSupply.Models;

public sealed class ReportChartViewModel
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Currency { get; set; }
    public string Type { get; set; } = "Bar";
    public List<ReportChartPoint> Points { get; set; } = [];
}

public sealed class ReportChartPoint
{
    public string Label { get; set; } = "";
    public decimal Value { get; set; }
}
