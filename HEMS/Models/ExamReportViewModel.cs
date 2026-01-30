public class ExamReportViewModel
{
    // For Specific Report
    public string StudentName { get; set; } = string.Empty;
    public int Score { get; set; }
    public DateTime DateTaken { get; set; }

    // For General Report
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int AverageScore { get; set; }

    // Shared
    public int TotalQuestions { get; set; }
    public double Percentage => TotalQuestions > 0 ? Math.Round(((double)Score / TotalQuestions) * 100, 2) : 0;

    // Avg Percentage for General
    public double AvgPercentage => (TotalQuestions > 0 && StudentCount > 0)
        ? Math.Round(((double)AverageScore / (TotalQuestions * StudentCount)) * 100, 2) : 0;
}