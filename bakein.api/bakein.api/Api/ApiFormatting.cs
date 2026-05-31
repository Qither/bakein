namespace Bakein.Api.Api;

internal static class ApiFormatting
{
    public static string Money(int cents) => cents == 0 ? "¥0" : $"¥{cents / 100m:0.##}";

    public static string CoursePrice(int cents, bool memberFree) => memberFree ? "会员免费" : Money(cents);

    public static string Duration(int minutes) => $"{minutes}min";

    public static string StepTime(int seconds)
    {
        var minutes = seconds / 60;
        var remainingSeconds = seconds % 60;
        return $"{minutes:00}:{remainingSeconds:00}";
    }

    public static string StudentLabel(int count) => count >= 1000 ? $"{count / 1000m:0.#}k" : count.ToString();
}
