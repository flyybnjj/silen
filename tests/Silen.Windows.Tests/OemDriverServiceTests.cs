using System.Net;
using System.Text.Json;
using Silen.Core;
using Xunit;
using Xunit.Abstractions;

namespace Silen.Windows.Tests;

public sealed class OemDriverServiceTests
{
    private static readonly MachineInfo Lenovo = new() { Manufacturer = "LENOVO", Model = "83GS", ProductName = "LOQ 15IAX9", SystemSku = "LENOVO_MT_83GS_BU_idea_FM_LOQ 15IAX9", OsName = "Microsoft Windows 11 Home", DeviceKind = DeviceKind.Laptop, IsLaptop = true };

    [Fact]
    public async Task Model_lookup_uses_official_identity_and_real_catalog_metadata_without_reading_serial()
    {
        using var client = Client(async (request, token) =>
        {
            var expected = request.RequestUri!.AbsolutePath.Contains("/api/") ? "/us/en/api/v4/downloads/drivers?productId=83GS" : "/us/en/products/83GS/downloads/";
            Assert.Equal(expected, request.RequestUri.PathAndQuery);
            return await Fixture(request.RequestUri.AbsolutePath.Contains("/api/") ? "lenovo-83gs-drivers.json" : "lenovo-83gs-product.html", token);
        });
        var result = await new OemDriverService(client, _ => throw new InvalidOperationException("Model lookup read serial")).FindAsync(Lenovo);
        Assert.True(result.Success);
        Assert.True(result.ModelMatched);
        Assert.Equal("LOQ 15IAX9 - Type 83GS", result.DeviceName);
        Assert.Equal("https://pcsupport.lenovo.com/us/en/products/83gs/downloads/", result.SupportUrl);
        Assert.Empty(result.SerialDisplay);
        Assert.Equal(2, result.Packages.Count);
        Assert.Equal("Nvidia_32.0.15.8186", result.Packages[0].Version);
        Assert.Equal("Display and Video Graphics", result.Packages[0].Category);
        Assert.Equal("Windows 11 (64-bit)", result.Packages[0].OperatingSystem);
        Assert.Equal("download.lenovo.com", new Uri(result.Packages[0].DownloadUrl!).Host);
        Assert.Equal("https://pcsupport.lenovo.com/us/en/downloads/DS565703", result.Packages[0].DetailUrl);
        Assert.Contains("catálogo", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("instalados", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("83GS003DCK", "", "83GS")]
    [InlineData("", "LENOVO_MT_83GS_BU_idea_FM_LOQ 15IAX9", "83GS")]
    [InlineData("20L5", "", "20L5")]
    [InlineData("LOQ 15IAX9", "", null)]
    [InlineData("Default string", "To be filled by O.E.M.", null)]
    [InlineData(null, "LENOVO_MT_83GS_BU_idea_FM_LOQ 15IAX9", "83GS")]
    [InlineData("LOQ 15IAX9", null, null)]
    [InlineData(null, null, null)]
    public void Lenovo_machine_type_is_derived_from_model_or_structured_sku(string? model, string? sku, string? expected) =>
        Assert.Equal(expected, OemDriverService.LenovoMachineType(new MachineInfo { Model = model!, SystemSku = sku! }));

    [Fact]
    public async Task Persisted_null_model_uses_a_usable_lenovo_sku()
    {
        var machine = JsonSerializer.Deserialize<MachineInfo>("{\"Manufacturer\":\"LENOVO\",\"Model\":null,\"ProductName\":null,\"SystemSku\":\"LENOVO_MT_83GS_BU_idea_FM_LOQ 15IAX9\"}")!;
        using var client = Client(async (request, token) =>
        {
            Assert.Equal(request.RequestUri!.AbsolutePath.Contains("/api/") ? "/us/en/api/v4/downloads/drivers?productId=83GS" : "/us/en/products/83GS/downloads/", request.RequestUri.PathAndQuery);
            return await Fixture(request.RequestUri.AbsolutePath.Contains("/api/") ? "lenovo-83gs-drivers.json" : "lenovo-83gs-product.html", token);
        });
        var result = await new OemDriverService(client, _ => throw new InvalidOperationException("Model lookup read serial")).FindAsync(machine);
        Assert.True(result.Success);
        Assert.True(result.ModelMatched);
        Assert.Equal("LOQ 15IAX9 - Type 83GS", result.DeviceName);
        Assert.Equal(2, result.Packages.Count);
        Assert.Empty(result.SerialDisplay);
    }

    [Fact]
    public async Task Persisted_null_lenovo_model_and_sku_offer_an_unmatched_official_portal()
    {
        var machine = JsonSerializer.Deserialize<MachineInfo>("{\"Manufacturer\":\"LENOVO\",\"Model\":null,\"ProductName\":null,\"SystemSku\":null}")!;
        using var client = Client((_, _) => throw new InvalidOperationException("Unknown Lenovo identity transmitted"));
        var result = await new OemDriverService(client).FindAsync(machine);
        Assert.True(result.Success);
        Assert.False(result.ModelMatched);
        Assert.Equal("https://pcsupport.lenovo.com/us/en/", result.SupportUrl);
        Assert.Empty(result.DeviceName);
        Assert.Empty(result.Packages);
    }

    [Fact]
    public async Task Persisted_null_microsoft_model_keeps_the_non_surface_portal()
    {
        var machine = JsonSerializer.Deserialize<MachineInfo>("{\"Manufacturer\":\"Microsoft Corporation\",\"Model\":null,\"ProductName\":\"Generic PC\",\"SystemSku\":null}")!;
        using var client = Client((_, _) => throw new InvalidOperationException("Unverified OEM API used"));
        var result = await new OemDriverService(client).FindAsync(machine);
        Assert.True(result.Success);
        Assert.False(result.ModelMatched);
        Assert.Equal("https://www.catalog.update.microsoft.com/", result.SupportUrl);
        Assert.Equal("Generic PC", result.DeviceName);
        Assert.Empty(result.Packages);
    }

    [Fact]
    public async Task Persisted_null_manufacturer_does_not_invent_an_oem()
    {
        var machine = JsonSerializer.Deserialize<MachineInfo>("{\"Manufacturer\":null,\"Model\":null,\"ProductName\":\"Legacy PC\",\"SystemSku\":null}")!;
        using var client = Client((_, _) => throw new InvalidOperationException("Unknown manufacturer transmitted"));
        var result = await new OemDriverService(client).FindAsync(machine);
        Assert.False(result.Success);
        Assert.False(result.ModelMatched);
        Assert.Empty(result.Provider);
        Assert.Empty(result.SupportUrl);
        Assert.Equal("Legacy PC", result.DeviceName);
        Assert.Empty(result.Packages);
    }

    [Fact]
    public async Task Persisted_all_null_identity_returns_unknown_support()
    {
        var machine = JsonSerializer.Deserialize<MachineInfo>("{\"Manufacturer\":null,\"Model\":null,\"ProductName\":null,\"SystemSku\":null}")!;
        using var client = Client((_, _) => throw new InvalidOperationException("Unknown manufacturer transmitted"));
        var result = await new OemDriverService(client).FindAsync(machine);
        Assert.False(result.Success);
        Assert.False(result.ModelMatched);
        Assert.Empty(result.Provider);
        Assert.Empty(result.DeviceName);
        Assert.Empty(result.SupportUrl);
        Assert.Empty(result.Packages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("To be filled by O.E.M.")]
    [InlineData("System Serial Number")]
    [InlineData("Default string")]
    [InlineData("Unknown")]
    [InlineData("N/A")]
    [InlineData("00000000")]
    [InlineData("FFFFFFFF")]
    [InlineData("123456789")]
    [InlineData("0123456789")]
    public async Task Placeholder_serial_is_rejected_before_any_network_request(string? serial)
    {
        using var client = Client((_, _) => throw new InvalidOperationException("Placeholder was transmitted"));
        var result = await new OemDriverService(client, _ => Task.FromResult(serial)).FindAsync(Lenovo, useSerial: true);
        Assert.False(result.Success);
        Assert.False(result.ModelMatched);
        Assert.Empty(result.Packages);
        Assert.Empty(result.SerialDisplay);
    }

    [Fact]
    public async Task Explicit_serial_is_transient_and_never_returned_as_a_support_url_or_result_data()
    {
        const string serial = "PFTEST1234";
        using var client = Client(async (request, token) =>
        {
            if (!request.RequestUri!.AbsolutePath.Contains("/api/"))
            {
                Assert.Equal("/us/en/products/PFTEST1234/downloads/", request.RequestUri.AbsolutePath);
                var html = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "lenovo-83gs-product.html"), token);
                return Ok(html.Replace("\"Serial\":\"\"", "\"Serial\":\"PFTEST1234\""));
            }
            Assert.Equal("?productId=83GS", request.RequestUri.Query);
            return await Fixture("lenovo-83gs-drivers.json", token);
        });
        var result = await new OemDriverService(client, _ => Task.FromResult<string?>(serial)).FindAsync(Lenovo, useSerial: true);
        Assert.True(result.Success);
        Assert.EndsWith("1234", result.SerialDisplay);
        Assert.DoesNotContain(serial, JsonSerializer.Serialize(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Explicit_serial_lookup_rejects_a_contradictory_returned_serial_before_the_catalog()
    {
        using var client = Client(async (request, token) =>
        {
            Assert.DoesNotContain("/api/", request.RequestUri!.AbsolutePath);
            var html = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "lenovo-83gs-product.html"), token);
            return Ok(html.Replace("\"Serial\":\"\"", "\"Serial\":\"PFWRONG5678\""));
        });
        var result = await new OemDriverService(client, _ => Task.FromResult<string?>("PFTEST1234")).FindAsync(Lenovo, useSerial: true);
        Assert.False(result.Success);
        Assert.False(result.ModelMatched);
        Assert.Empty(result.Packages);
        var serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("PFTEST1234", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PFWRONG5678", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("pftest1234")]
    public async Task Explicit_serial_lookup_preserves_empty_missing_and_matching_serial_metadata(string? returnedSerial)
    {
        using var client = Client(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/api/")) return await Fixture("lenovo-83gs-drivers.json", token);
            var html = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "lenovo-83gs-product.html"), token);
            return Ok(returnedSerial is null ? html.Replace("\"Serial\":\"\",", "") : html.Replace("\"Serial\":\"\"", "\"Serial\":\"" + returnedSerial + "\""));
        });
        var result = await new OemDriverService(client, _ => Task.FromResult<string?>("PFTEST1234")).FindAsync(Lenovo, useSerial: true);
        Assert.True(result.Success);
        Assert.True(result.ModelMatched);
        Assert.Equal(2, result.Packages.Count);
        Assert.EndsWith("1234", result.SerialDisplay);
        Assert.DoesNotContain("PFTEST1234", JsonSerializer.Serialize(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mismatched_official_machine_type_does_not_claim_a_model_match_or_fetch_a_catalog()
    {
        using var client = Client(async (request, token) =>
        {
            Assert.DoesNotContain("/api/", request.RequestUri!.AbsolutePath);
            var html = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "lenovo-83gs-product.html"), token);
            return Ok(html.Replace("\"MachineType\":\"83GS\"", "\"MachineType\":\"82XX\""));
        });
        var result = await new OemDriverService(client).FindAsync(Lenovo);
        Assert.False(result.Success);
        Assert.False(result.ModelMatched);
        Assert.Empty(result.Packages);
    }

    [Fact]
    public async Task Offline_failure_retains_the_official_portal_without_a_false_catalog_success()
    {
        using var client = Client((_, _) => throw new HttpRequestException("Sensitive request URL must not be exposed"));
        var result = await new OemDriverService(client).FindAsync(Lenovo);
        Assert.False(result.Success);
        Assert.Empty(result.Packages);
        Assert.Equal("pcsupport.lenovo.com", new Uri(result.SupportUrl).Host);
        Assert.DoesNotContain("Sensitive", result.Message);
    }

    [Fact]
    public async Task Not_found_returns_an_honest_fallback()
    {
        using var client = Client((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var result = await new OemDriverService(client).FindAsync(Lenovo);
        Assert.False(result.Success);
        Assert.False(result.ModelMatched);
        Assert.Empty(result.Packages);
        Assert.Contains("encontr", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Malformed_catalog_is_not_reported_as_an_empty_success()
    {
        using var client = Client(async (request, token) => request.RequestUri!.AbsolutePath.Contains("/api/") ? Ok("<html>unavailable</html>") : await Fixture("lenovo-83gs-product.html", token));
        var result = await new OemDriverService(client).FindAsync(Lenovo);
        Assert.False(result.Success);
        Assert.Empty(result.Packages);
    }

    [Fact]
    public async Task Cancellation_interrupts_a_pending_http_lookup()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        using var client = Client(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Ok(""); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OemDriverService(client).FindAsync(Lenovo, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task Timeout_has_a_truthful_official_portal_fallback()
    {
        using var client = Client(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Ok(""); });
        var result = await new OemDriverService(client, timeout: TimeSpan.FromMilliseconds(30)).FindAsync(Lenovo);
        Assert.False(result.Success);
        Assert.Empty(result.Packages);
        Assert.Contains("tiempo", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Dell Inc.", "www.dell.com")]
    [InlineData("HP", "support.hp.com")]
    [InlineData("ASUSTeK COMPUTER INC.", "www.asus.com")]
    [InlineData("Acer", "www.acer.com")]
    [InlineData("Micro-Star International Co., Ltd.", "www.msi.com")]
    public async Task Other_known_oems_offer_a_portal_without_inventing_model_matches(string manufacturer, string host)
    {
        using var client = Client((_, _) => throw new InvalidOperationException("Unverified OEM API used"));
        var result = await new OemDriverService(client).FindAsync(new MachineInfo { Manufacturer = manufacturer, Model = "Sample model", DeviceKind = DeviceKind.Laptop });
        Assert.True(result.Success);
        Assert.False(result.ModelMatched);
        Assert.Empty(result.Packages);
        Assert.Equal(host, new Uri(result.SupportUrl).Host);
        Assert.Contains("modelo", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_oems_do_not_invent_official_urls()
    {
        using var client = Client((_, _) => throw new InvalidOperationException("Unknown manufacturer transmitted"));
        var result = await new OemDriverService(client).FindAsync(new MachineInfo { Manufacturer = "Unknown vendor" });
        Assert.False(result.Success);
        Assert.Empty(result.SupportUrl);
        Assert.Empty(result.Packages);
    }

    [Theory]
    [InlineData("https://download.lenovo.com.attacker.example/fake.exe")]
    [InlineData("http://download.lenovo.com/fake.exe")]
    [InlineData("https://user@download.lenovo.com/fake.exe")]
    [InlineData("https://download.lenovo.com:8443/fake.exe")]
    public void Catalog_parser_rejects_unapproved_or_insecure_downloads(string unsafeUrl)
    {
        var fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "lenovo-83gs-drivers.json"));
        using var document = JsonDocument.Parse(fixture);
        var firstUrl = document.RootElement.GetProperty("body").GetProperty("DownloadItems")[0].GetProperty("Files")[0].GetProperty("URL").GetString()!;
        var packages = OemDriverService.ParseLenovoCatalog(fixture.Replace(firstUrl, unsafeUrl));
        Assert.Null(packages[0].DownloadUrl);
        Assert.Equal("pcsupport.lenovo.com", new Uri(packages[0].DetailUrl!).Host);
    }

    [Fact]
    public async Task Surface_support_requires_a_surface_device_identity()
    {
        using var client = Client((_, _) => throw new InvalidOperationException("Unverified OEM API used"));
        var surface = await new OemDriverService(client).FindAsync(new MachineInfo { Manufacturer = "Microsoft Corporation", Model = "Surface Laptop 7" });
        var generic = await new OemDriverService(client).FindAsync(new MachineInfo { Manufacturer = "Microsoft Corporation", Model = "Generic PC" });
        Assert.Equal("https://support.microsoft.com/surface", surface.SupportUrl);
        Assert.Equal("https://www.catalog.update.microsoft.com/", generic.SupportUrl);
        Assert.False(surface.ModelMatched);
    }

    [Fact]
    public async Task A_redirect_to_an_unapproved_host_is_rejected_before_it_is_requested()
    {
        using var client = Client((request, _) =>
        {
            Assert.Equal("pcsupport.lenovo.com", request.RequestUri!.Host);
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://attacker.example/catalog");
            return Task.FromResult(response);
        });
        var result = await new OemDriverService(client).FindAsync(Lenovo);
        Assert.False(result.Success);
        Assert.Empty(result.Packages);
    }

    [Fact]
    public async Task A_verified_empty_catalog_is_distinct_from_a_failed_response()
    {
        using var client = Client(async (request, token) => request.RequestUri!.AbsolutePath.Contains("/api/") ? Ok("{\"message\":\"succeed\",\"body\":{\"DownloadItems\":[]}}") : await Fixture("lenovo-83gs-product.html", token));
        var result = await new OemDriverService(client).FindAsync(Lenovo);
        Assert.True(result.Success);
        Assert.True(result.ModelMatched);
        Assert.Empty(result.Packages);
        Assert.Contains("no entregó paquetes", result.Message);
    }

    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => new(new FixtureHandler(send));
    private static HttpResponseMessage Ok(string content) => new(HttpStatusCode.OK) { Content = new StringContent(content) };
    private static async Task<HttpResponseMessage> Fixture(string file, CancellationToken token) => Ok(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", file), token));
    private sealed class FixtureHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}

public sealed class LiveOemVerificationTests(ITestOutputHelper output)
{
    [LiveOemFact]
    [Trait("Category", "LiveOem")]
    public async Task Official_lenovo_83gs_lookup_returns_a_verified_model_catalog()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var machine = new MachineInfo { Manufacturer = "LENOVO", Model = "83GS", ProductName = "LOQ 15IAX9", SystemSku = "LENOVO_MT_83GS_BU_idea_FM_LOQ 15IAX9", DeviceKind = DeviceKind.Laptop };
        var result = await new OemDriverService().FindAsync(machine, cancellationToken: cancellation.Token);
        Assert.True(result.Success, result.Message);
        Assert.True(result.ModelMatched);
        Assert.Equal("LOQ 15IAX9 - Type 83GS", result.DeviceName);
        Assert.NotEmpty(result.Packages);
        Assert.Empty(result.SerialDisplay);
        Assert.All(result.Packages.Where(package => package.DownloadUrl is not null), package => Assert.Equal("download.lenovo.com", new Uri(package.DownloadUrl!).Host));
        output.WriteLine(JsonSerializer.Serialize(new { result.Provider, result.DeviceName, result.ModelMatched, result.SupportUrl, Packages = result.Packages.Count, Categories = result.Packages.Select(package => package.Category).Distinct(), result.Message }));
    }
}

public sealed class LiveOemFactAttribute : FactAttribute
{
    public LiveOemFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SILEN_LIVE_OEM_CHECK") != "1") Skip = "Set SILEN_LIVE_OEM_CHECK=1 for the explicit official model-only network verification.";
    }
}
