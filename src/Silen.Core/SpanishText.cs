using System.Globalization;

namespace Silen.Core;

public static class SpanishText
{
    public static string Form(long count, string singular, string plural) => count == 1 ? singular : plural;
    public static string Count(long count, string singular, string plural) =>
        count.ToString(CultureInfo.GetCultureInfo("es-CL")) + " " + Form(count, singular, plural);
}
