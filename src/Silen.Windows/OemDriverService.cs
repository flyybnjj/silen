using System.Globalization;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Silen.Core;

namespace Silen.Windows;

public sealed class OemDriverService
{
    private const string LenovoBase = "https://pcsupport.lenovo.com/us/en/";
    private static readonly HttpClient SharedClient = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
        MaxResponseContentBufferSize = 4 * 1024 * 1024
    };
    private readonly HttpClient client;
    private readonly Func<CancellationToken, Task<string?>> serialReader;
    private readonly TimeSpan timeout;

    public OemDriverService() : this(SharedClient) { }

    internal OemDriverService(HttpClient client, Func<CancellationToken, Task<string?>>? serialReader = null, TimeSpan? timeout = null)
    {
        this.client = client;
        this.serialReader = serialReader ?? ReadBiosSerialAsync;
        this.timeout = timeout ?? TimeSpan.FromSeconds(20);
    }

    public async Task<OemSupportResult> FindAsync(MachineInfo machine, bool useSerial = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(machine);
        cancellationToken.ThrowIfCancellationRequested();
        var manufacturer = machine.Manufacturer?.Trim() ?? "";
        var model = machine.Model?.Trim() ?? "";
        var name = IsUsableIdentity(machine.ProductName) ? machine.ProductName.Trim() : model;
        var portal = WindowsActions.ManufacturerUrl(manufacturer) ?? "";
        var result = new OemSupportResult(portal.Length > 0, manufacturer, name, portal, [],
            "Portal oficial: busca el modelo o SKU detectado y selecciona tu sistema operativo. No hay un catálogo programático verificado para este fabricante.", false);
        if (portal.Length == 0)
            return result with { Success = false, Message = "No se identificó un portal oficial para este fabricante. Verifica el nombre y modelo del equipo." };
        if (!manufacturer.Equals("LENOVO", StringComparison.OrdinalIgnoreCase))
        {
            if ((manufacturer.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) || manufacturer.Equals("Microsoft Corporation", StringComparison.OrdinalIgnoreCase)) &&
                (name.StartsWith("Surface", StringComparison.OrdinalIgnoreCase) || model.StartsWith("Surface", StringComparison.OrdinalIgnoreCase)))
                result = result with { Provider = "Microsoft Surface", SupportUrl = "https://support.microsoft.com/surface" };
            return useSerial ? result with { Success = false, Message = "La búsqueda por serie dentro de la app está disponible para Lenovo. Usa el portal oficial para identificar este equipo." } : result;
        }

        var machineType = LenovoMachineType(machine);
        result = result with { Provider = "Lenovo", SupportUrl = machineType is null ? LenovoBase : ModelPortal(machineType) };
        if (!useSerial && machineType is null)
            return result with { Message = "No se obtuvo un tipo Lenovo válido del modelo o SKU. Usa el portal oficial o la búsqueda explícita por serie; no se confirmó una coincidencia de modelo." };

        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(timeout);
        string? serial = null;
        try
        {
            if (useSerial)
            {
                serial = NormalizeSerial(await serialReader(requestTimeout.Token).ConfigureAwait(false));
                if (serial is null)
                    return result with { Success = false, Message = "Windows no entregó una serie BIOS válida. No se envió ninguna serie; usa el modelo o el portal oficial." };
                result = result with { SerialDisplay = MaskSerial(serial) };
            }
            var identifier = serial ?? machineType!;
            var html = await GetLenovoTextAsync(new Uri(LenovoBase + "products/" + Uri.EscapeDataString(identifier) + "/downloads/"), requestTimeout.Token).ConfigureAwait(false);
            var product = ParseLenovoProduct(html);
            if (product is null || serial is null && !product.Value.MachineType.Equals(machineType, StringComparison.OrdinalIgnoreCase) ||
                serial is not null && product.Value.Serial.Length > 0 && !product.Value.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase))
                return result with { Success = false, Message = "Lenovo no confirmó la identidad solicitada. Usa el portal oficial para verificar el equipo." };

            machineType = product.Value.MachineType;
            result = result with
            {
                ModelMatched = true,
                DeviceName = WithoutSerial(product.Value.Name, serial),
                SupportUrl = ModelPortal(machineType)
            };
            var json = await GetLenovoTextAsync(new Uri(LenovoBase + "api/v4/downloads/drivers?productId=" + Uri.EscapeDataString(machineType)), requestTimeout.Token).ConfigureAwait(false);
            var packages = ParseLenovoCatalog(json);
            if (serial is not null)
                packages = packages.Select(package => package with
                {
                    Name = WithoutSerial(package.Name, serial), Version = WithoutSerial(package.Version, serial),
                    Category = WithoutSerial(package.Category, serial), ReleaseDate = WithoutSerial(package.ReleaseDate, serial),
                    OperatingSystem = WithoutSerial(package.OperatingSystem, serial),
                    DownloadUrl = ContainsSerial(package.DownloadUrl, serial) ? null : package.DownloadUrl,
                    DetailUrl = ContainsSerial(package.DetailUrl, serial) ? null : package.DetailUrl
                }).ToList();
            return result with
            {
                Success = true, Packages = packages,
                Message = packages.Count == 0
                    ? "Lenovo confirmó el modelo, pero su catálogo no entregó paquetes. Revisa el portal oficial."
                    : "Catálogo oficial del modelo Lenovo. Estos paquetes no son actualizaciones confirmadas de los drivers instalados: verifica hardware, versión y sistema compatible en Lenovo antes de instalarlos. Los enlaces se abren en el navegador."
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return result with { Success = false, Message = "La consulta superó el tiempo disponible. Puedes reintentar o abrir el portal oficial." };
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return result with { Success = false, Message = "No se encontró el modelo o catálogo solicitado en Lenovo. Verifica el modelo en el portal oficial." };
        }
        catch (HttpRequestException)
        {
            return result with { Success = false, Message = "No se pudo consultar Lenovo: la red o el servicio oficial no están disponibles. Puedes reintentar o abrir el portal oficial." };
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or FormatException or ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return result with { Success = false, Message = "No se pudo verificar la respuesta OEM o leer la identidad solicitada. No se muestran paquetes sin verificar; usa el portal oficial." };
        }
    }

    internal static string? LenovoMachineType(MachineInfo machine)
    {
        var model = machine.Model?.Trim().ToUpperInvariant() ?? "";
        if (Regex.IsMatch(model, "^[0-9][A-Z0-9]{3}([A-Z0-9]{6})?$", RegexOptions.CultureInvariant)) return model[..4];
        var sku = machine.SystemSku?.Trim().ToUpperInvariant() ?? "";
        var structured = Regex.Match(sku, @"(?:^|_)MT_([0-9][A-Z0-9]{3})(?:_|$)", RegexOptions.CultureInvariant);
        if (structured.Success) return structured.Groups[1].Value;
        return Regex.IsMatch(sku, "^[0-9][A-Z0-9]{3}([A-Z0-9]{6})?$", RegexOptions.CultureInvariant) ? sku[..4] : null;
    }

    internal static bool IsUsableIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = Regex.Replace(value, "[^a-zA-Z0-9]", "").ToLowerInvariant();
        return normalized is not ("defaultstring" or "tobefilledbyoem" or "systemserialnumber" or "serialnumber" or "unknown" or "none" or "na" or "notapplicable" or "notavailable" or "notset" or "invalid" or "0123456789" or "123456789") &&
            normalized.Length > 0 && !(normalized.Length > 1 && normalized.All(character => character == normalized[0]));
    }

    private static string? NormalizeSerial(string? value)
    {
        var serial = value?.Trim().ToUpperInvariant();
        return IsUsableIdentity(serial) && Regex.IsMatch(serial!, "^[A-Z0-9]{4,32}$", RegexOptions.CultureInvariant) ? serial : null;
    }

    private static string MaskSerial(string serial) => serial.Length > 4 ? new string('•', serial.Length - 4) + serial[^4..] : "••••";
    private static string WithoutSerial(string value, string? serial) => serial is null ? value : value.Replace(serial, MaskSerial(serial), StringComparison.OrdinalIgnoreCase);
    private static bool ContainsSerial(string? value, string serial) => value?.Contains(serial, StringComparison.OrdinalIgnoreCase) == true;
    private static string ModelPortal(string type) => LenovoBase + "products/" + type.ToLowerInvariant() + "/downloads/";

    private async Task<string> GetLenovoTextAsync(Uri uri, CancellationToken token)
    {
        for (var redirect = 0; redirect < 4; redirect++)
        {
            if (!OfficialUrl(uri.AbsoluteUri, "pcsupport.lenovo.com")) throw new InvalidDataException("Unverified Lenovo endpoint.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Referrer = new Uri("https://pcsupport.lenovo.com/");
            request.Headers.UserAgent.ParseAdd("Silen/1.0");
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoStore = true, NoCache = true };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308 && response.Headers.Location is { } location)
            {
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                continue;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        }
        throw new InvalidDataException("Too many Lenovo redirects.");
    }

    private static (string MachineType, string Name, string Serial)? ParseLenovoProduct(string html)
    {
        var match = Regex.Match(html, @"\bvar\s+ds_productinfo\s*=\s*window\.ds_productinfo\s*\|\|\s*(\{[^\r\n]*\})\s*;", RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        using var document = JsonDocument.Parse(match.Groups[1].Value);
        var type = Text(document.RootElement, "MachineType").ToUpperInvariant();
        var name = Text(document.RootElement, "ProductName");
        return Regex.IsMatch(type, "^[0-9][A-Z0-9]{3}$", RegexOptions.CultureInvariant) && IsUsableIdentity(name) ? (type, name, Text(document.RootElement, "Serial")) : null;
    }

    internal static List<OemDriverPackage> ParseLenovoCatalog(string json)
    {
        using var document = JsonDocument.Parse(json);
        var body = Property(document.RootElement, "body");
        var downloads = Property(body, "DownloadItems");
        if (!Text(document.RootElement, "message").Equals("succeed", StringComparison.OrdinalIgnoreCase) || downloads.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Unrecognized Lenovo catalog.");
        var packages = new List<OemDriverPackage>();
        foreach (var item in downloads.EnumerateArray())
        {
            var title = WebUtility.HtmlDecode(Text(item, "Title"));
            if (string.IsNullOrWhiteSpace(title)) continue;
            var files = Property(item, "Files");
            var installer = files.ValueKind == JsonValueKind.Array ? files.EnumerateArray().FirstOrDefault(file =>
                Text(file, "TypeString").Equals("exe", StringComparison.OrdinalIgnoreCase) || Text(file, "TypeString").Equals("zip", StringComparison.OrdinalIgnoreCase) || Text(file, "TypeString").Equals("msi", StringComparison.OrdinalIgnoreCase)) : default;
            var download = Text(installer, "URL");
            var docId = Text(item, "DocId");
            var detail = Regex.IsMatch(docId, "^DS[0-9]+$", RegexOptions.CultureInvariant) ? LenovoBase + "downloads/" + docId : null;
            var unix = Property(Property(installer, "Date"), "Unix");
            if (unix.ValueKind == JsonValueKind.Undefined) unix = Property(Property(item, "Date"), "Unix");
            var date = "";
            if (long.TryParse(unix.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
            {
                try { date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
                catch (ArgumentOutOfRangeException) { }
            }
            var systems = Property(item, "OperatingSystemKeys");
            var operatingSystem = systems.ValueKind == JsonValueKind.Array ? string.Join(", ", systems.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()).Where(value => !string.IsNullOrWhiteSpace(value))) : "";
            packages.Add(new OemDriverPackage(title, Text(Property(item, "SummaryInfo"), "Version"), Text(Property(item, "Category"), "Name"), date, operatingSystem,
                OfficialUrl(download, "download.lenovo.com") ? download : null, detail));
        }
        return packages.DistinctBy(package => package.DetailUrl ?? package.Name + "\n" + package.Version).ToList();
    }

    private static bool OfficialUrl(string? value, string host) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && uri.IdnHost.Equals(host, StringComparison.OrdinalIgnoreCase);
    private static JsonElement Property(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;
    private static string Text(JsonElement element, string name) => Property(element, name) is { ValueKind: JsonValueKind.String } value ? value.GetString()?.Trim() ?? "" : "";

    private static Task<string?> ReadBiosSerialAsync(CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        using var searcher = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\cimv2"), new ObjectQuery("SELECT SerialNumber FROM Win32_BIOS"),
            new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(5), ReturnImmediately = true });
        using var rows = searcher.Get();
        foreach (ManagementObject row in rows)
        {
            using (row)
            {
                token.ThrowIfCancellationRequested();
                return row["SerialNumber"]?.ToString();
            }
        }
        return null;
    }, token);
}
