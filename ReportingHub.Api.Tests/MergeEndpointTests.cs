using ClosedXML.Excel;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using static ReportingHub.Api.Common.MediaTypeNamesExtensions;

namespace ReportingHub.Api.Tests;

public class MergeEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MergeEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("http://localhost")
        });
    }

    private const string Endpoint = "/excel/merge";

    private static MultipartFormDataContent CreateMultipartContent(
        IEnumerable<(Stream file, string name)> files,
        string? namesJson = null,
        string? apiKey = null)
    {
        var form = new MultipartFormDataContent();

        foreach (var (file, name) in files)
        {
            form.Add(ExcelHelpers.AsXlsxContent(file, name));
        }

        if (!string.IsNullOrWhiteSpace(namesJson))
        {
            var namesContent = new StringContent(namesJson, Encoding.UTF8, Application.Json);
            form.Add(namesContent, "names");
        }

        return form;
    }
    [Fact]
    public async Task HealthCheck_Boots()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var resp = await client.GetAsync("/");
        resp.Should().NotBeNull();
    }

    [Fact]
    public async Task Merge_ReturnsXlsx_OnValidInputs()
    {
        // Arrange
        using var f1 = ExcelHelpers.CreateWorkbookStream(new()
        {
            ["Sheet1"] = new[] { "A1", "A2" }
        });
        using var f2 = ExcelHelpers.CreateWorkbookStream(new()
        {
            ["Report"] = new[] { "B1" }
        });

        var names = new
        {
            mode = "pattern",
            pattern = "{sheet}",
            collision = "dedupe"
        };
        var namesJson = System.Text.Json.JsonSerializer.Serialize(names);

        using var form = CreateMultipartContent(
            new[] { (f1, "A.xlsx"), (f2, "B.xlsx") },
            namesJson);

        // If you use API key middleware, add the header:
        // _client.DefaultRequestHeaders.Add("X-Api-Key", "test-key");

        // Act
        var res = await _client.PostAsync(Endpoint, form);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should()
            .Be(Application.ExcelXlsx);

        // Validate it is a real workbook and sheets are unique
        using var payload = await res.Content.ReadAsStreamAsync();
        using var wb = new XLWorkbook(payload);
        wb.Worksheets.Should().NotBeEmpty();
        wb.Worksheets.Select(w => w.Name).Should().OnlyHaveUniqueItems();
    }


    [Fact]
    public async Task Merge_Returns400_WhenNoFiles()
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent("true", Encoding.UTF8, System.Net.Mime.MediaTypeNames.Text.Plain), "noop");

        var res = await _client.PostAsync("/excel/merge", form);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"error\"");
        body.Should().Contain("correlationId");
    }

    [Fact]
    public async Task Merge_Returns400_WhenInvalidFileType()
    {
        var form = new MultipartFormDataContent();
        var txt = new StringContent("not an xlsx");
        txt.Headers.ContentType = new MediaTypeHeaderValue(Text.Plain);
        txt.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"files\"",
            FileName = "\"bad.txt\""
        };
        form.Add(txt);

        var res = await _client.PostAsync(Endpoint, form);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("Only .xlsx allowed");
    }

    [Fact]
    public async Task Merge_AppliesMapNaming()
    {
        using var src = ExcelHelpers.CreateWorkbookStream(new()
        {
            ["Sheet1"] = new[] { "x" }
        });

        var names = new
        {
            mode = "map",
            collision = "dedupe",
            map = new
            {
                // Map Sheet1 to GrantsFY25
                A_xlsx = new Dictionary<string, string> { ["Sheet1"] = "GrantsFY25" }
            }
        };

        // The serialized JSON needs the real filename key: "A.xlsx"
        var json = System.Text.Json.JsonSerializer.Serialize(names)
            .Replace("\"A_xlsx\"", "\"A.xlsx\"");

        using var form = CreateMultipartContent(new[] { (src, "A.xlsx") }, json);

        var res = await _client.PostAsync(Endpoint, form);
        res.EnsureSuccessStatusCode();

        using var payload = await res.Content.ReadAsStreamAsync();
        using var wb = new XLWorkbook(payload);

        wb.Worksheets.Select(w => w.Name).Should().Contain("GrantsFY25");
    }

    [Fact]
    public async Task Merge_Dedupes_OnNameCollision()
    {
        using var f1 = ExcelHelpers.CreateWorkbookStream(new() { ["Sheet1"] = new[] { "1" } });
        using var f2 = ExcelHelpers.CreateWorkbookStream(new() { ["Sheet1"] = new[] { "2" } });

        var names = new { mode = "pattern", pattern = "{sheet}", collision = "dedupe" };
        var namesJson = System.Text.Json.JsonSerializer.Serialize(names);

        using var form = CreateMultipartContent(new[] { (f1, "A.xlsx"), (f2, "B.xlsx") }, namesJson);

        var res = await _client.PostAsync(Endpoint, form);
        res.EnsureSuccessStatusCode();

        using var payload = await res.Content.ReadAsStreamAsync();
        using var wb = new XLWorkbook(payload);

        // Should have 2 worksheets with unique names (e.g., "Sheet1", "Sheet1 (2)" or similar)
        wb.Worksheets.Count.Should().Be(2);
        wb.Worksheets.Select(w => w.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Merge_IgnoresInvalidNamesJson_AndUsesDefaults()
    {
        using var f1 = ExcelHelpers.CreateWorkbookStream(new() { ["Sheet1"] = new[] { "a" } });
        using var f2 = ExcelHelpers.CreateWorkbookStream(new() { ["Sheet1"] = new[] { "b" } });

        // Bad JSON
        const string invalidJson = "{ invalid json }";

        using var form = CreateMultipartContent(new[] { (f1, "A.xlsx"), (f2, "B.xlsx") }, invalidJson);


        var res = await _client.PostAsync(Endpoint, form);

        res.EnsureSuccessStatusCode();
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        // Validate workbook is returned and sheets are deduped by the default policy
        using var payload = await res.Content.ReadAsStreamAsync();
        using var wb = new XLWorkbook(payload);

        wb.Worksheets.Count.Should().Be(2);

        // Default behavior assumption: keep original sheet names and dedupe collisions
        // (e.g., "Sheet1" and "Sheet1 (2)" — we don't assert exact suffix, just uniqueness)
        wb.Worksheets.Select(w => w.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Merge_Returns400_OnNameCollision_WhenPolicyIsError()
    {
        // Two files whose sheets resolve to the same target name via pattern
        using var f1 = ExcelHelpers.CreateWorkbookStream(new() { ["Sheet1"] = new[] { "A1" } });
        using var f2 = ExcelHelpers.CreateWorkbookStream(new() { ["Sheet1"] = new[] { "B1" } });

        var names = new
        {
            mode = "pattern",
            pattern = "{sheet}",   // both become "Sheet1"
            collision = "error"    // explicit: error out on collision
        };

        var namesJson = JsonSerializer.Serialize(names);

        using var form = CreateMultipartContent(
            new[] { (f1, "A.xlsx"), (f2, "B.xlsx") },
            namesJson);

        var res = await _client.PostAsync(Endpoint, form);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadAsStringAsync();

        body.Should().Contain("\"error\"");
        body.Should().Contain("Collision", because: "error code/message should indicate a naming collision");
        body.Should().Contain("correlationId");
    }

    [Fact]
    public async Task Merge_MapsExplicitFileAndSheetNames_AsConfigured()
    {
        // Create one workbook with Sheet1, map it to GrantsFY25
        using var src = ExcelHelpers.CreateWorkbookStream(new()
        {
            ["Sheet1"] = ["value"]
        });

        // JSON keys cannot have dots directly in anonymous object property names,
        // so serialize a placeholder then replace the key with "A.xlsx"
        var names = new
        {
            mode = "map",
            collision = "dedupe", // irrelevant with one file, but keeps behavior explicit
            map = new
            {
                A_xlsx = new Dictionary<string, string>
                {
                    ["Sheet1"] = "GrantsFY25"
                }
            }
        };

        var json = JsonSerializer.Serialize(names)
            .Replace("\"A_xlsx\"", "\"A.xlsx\"");

        using var form = CreateMultipartContent([(src, "A.xlsx")], json);

        var res = await _client.PostAsync(Endpoint, form);
        res.EnsureSuccessStatusCode();

        using var payload = await res.Content.ReadAsStreamAsync();
        using var wb = new XLWorkbook(payload);

        var sheetNames = wb.Worksheets.Select(w => w.Name).ToArray();
        sheetNames.Should().Contain("GrantsFY25");
        sheetNames.Should().NotContain("Sheet1"); // ensure mapping actually renamed
    }

    //[Fact]
    //public async Task Merge_MapsWithWildcard_ForAnySheet()
    //{
    //    using var src = ExcelHelpers.CreateWorkbookStream(new()
    //    {
    //        ["Anything"] = ["x"]
    //    });

    //    var names = new
    //    {
    //        mode = "map",
    //        collision = "dedupe",
    //        map = new
    //        {
    //            A_xlsx = new Dictionary<string, string>
    //            {
    //                ["*"] = "{file}-{sheet}"  // e.g., "A.xlsx-Anything"
    //            }
    //        }
    //    };

    //    var json = JsonSerializer.Serialize(names)
    //        .Replace("\"A_xlsx\"", "\"A.xlsx\"");

    //    using var form = CreateMultipartContent([(src, "A.xlsx")], json);

    //    var res = await _client.PostAsync(Endpoint, form);
    //    res.EnsureSuccessStatusCode();

    //    using var payload = await res.Content.ReadAsStreamAsync();
    //    using var wb = new XLWorkbook(payload);

    //    wb.Worksheets.Select(w => w.Name)
    //      .Should().Contain("A.xlsx-Anything");
    //}

}
