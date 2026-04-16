namespace Tyresoles.Web.GraphQL;

/// <summary>Input for <see cref="Mutation.SaveVehicle"/> — NAV transporter / vehicle master.</summary>
public sealed class VehicleSaveInput
{
    public string No { get; set; } = "";
    public string? Name { get; set; }
    public string? MobileNo { get; set; }
    public string? GstNo { get; set; }
    public int LineNo { get; set; }
    public string? ResponsibilityCenter { get; set; }
    public int Status { get; set; }
}
