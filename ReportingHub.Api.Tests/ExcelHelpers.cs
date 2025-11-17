using ClosedXML.Excel;
using System.Net.Http.Headers;
using System.Text;

using static ReportingHub.Api.Common.MediaTypeNamesExtensions;

namespace ReportingHub.Api.Tests;

public static class ExcelHelpers
{
    public static Stream CreateWorkbookStream(Dictionary<string, IEnumerable<string>> sheets)
    {
        var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            foreach (var kvp in sheets)
            {
                var ws = wb.Worksheets.Add(kvp.Key);
                int r = 1;
                foreach (var row in kvp.Value)
                {
                    ws.Cell(r++, 1).Value = row;
                }
            }
            wb.SaveAs(ms);
        }
        ms.Position = 0;
        return ms;
    }

    public static StreamContent AsXlsxContent(Stream stream, string fileName)
    {
        var sc = new StreamContent(stream);
        sc.Headers.ContentType = new MediaTypeHeaderValue(
            Application.ExcelXlsx);
        sc.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"files\"",
            FileName = $"\"{fileName}\""
        };
        return sc;
    }

    public static StringContent AsNamesJson(object obj) =>
        new StringContent(System.Text.Json.JsonSerializer.Serialize(obj), Encoding.UTF8, Application.Json);
}
