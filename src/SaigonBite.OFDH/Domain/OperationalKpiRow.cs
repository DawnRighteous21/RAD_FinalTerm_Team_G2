namespace SaigonBite.OFDH.Domain;

public class OperationalKpiRow
{
    public string EntityLabel { get; set; } = string.Empty;
    public string PeakOrCompletedText { get; set; } = string.Empty;
    public string RerouteOrFailedText { get; set; } = string.Empty;
    public string CollectedCodText { get; set; } = "—";
    public bool HasWarning { get; set; }
}
