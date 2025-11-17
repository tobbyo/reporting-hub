using ClosedXML.Excel;
using Microsoft.AspNetCore.Http.HttpResults;
using ReportingHub.Api.Contracts;
using ReportingHub.Api.Contracts.Enums;
using ReportingHub.Api.Infrastructure;
using ReportingHub.Api.Infrastructure.Parsing;
using System.Text.Json;
using System.Text.RegularExpressions;

using static ReportingHub.Api.Common.MediaTypeNamesExtensions;

namespace ReportingHub.Api.Endpoints;

public static class ExcelMergeEndpoints
{
    private const long MaxFileBytes = 256L * 1024 * 1024; // 256 MB per file
    private const int MaxFiles = 20;
    private const int MaxSheetsTotal = 200;
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/excel/merge", Merge)
            .Accepts<MergeForm>("multipart/form-data")
            .Produces<FileContentHttpResult>(StatusCodes.Status200OK, contentType: Application.ExcelXlsx)
            .Produces(StatusCodes.Status400BadRequest, contentType: Application.Json)
            .WithOpenApi();
    }

    /*
     * ******** 
     * SIMPLE MERGE - NO MODE OR PATTERN
     * curl -X POST "https://localhost:5001/excel/merge" \
      -H "accept: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" \
      -H "Content-Type: multipart/form-data" \
      -F "files=@/path/to/A.xlsx;type=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" \
      -F "files=@/path/to/B.xlsx;type=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
     * 
     * ******* 
     * MODE = PATTERN
     * PATTERN = {sheet} OR {file}_{sheet}
     * curl -X POST "https://localhost:5001/excel/merge" \
      -H "accept: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" \
      -H "Content-Type: multipart/form-data" \
      -F "files=@/path/to/A.xlsx;type=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" \
      -F "files=@/path/to/B.xlsx;type=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" \
      -F 'names={ "mode": "pattern", "pattern": "{sheet}", "collision": "dedupe" }'
     * 
     * 
     * ******* 
     * MODE = MAP (EXPLICIT MAPPING NAMES)
     * curl -X POST "https://localhost:5001/excel/merge" \
      -H "accept: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" \
      -H "Content-Type: multipart/form-data" \
      -F "files=@/path/to/A.xlsx;type=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" \
      -F "files=@/path/to/B.xlsx;type=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" \
      -F 'names={
            "mode": "map",
            "collision": "dedupe",
            "map": {
              "A.xlsx": { "Sheet1": "GrantsFY25" },
              "B.xlsx": { "*": "{file}-{sheet}" }
            }
          }'
    */

    private static async Task<IResult> Merge(HttpRequest request, ApiResults api, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ExcelMerge");

        if (!request.HasFormContentType)
            return api.Bad("InvalidContentType", "Send files as multipart/form-data.");

        //NamesRules? naming = null;
        IFormCollection form;

        try
        {
            form = await request.ReadFormAsync();
        }
        catch (InvalidDataException)
        {
            return api.Bad("InvalidMultipart", "Invalid multipart/form-data payload.");
        }

        var files = form.Files;
        if (files.Count == 0) return api.Bad("NoFiles", "No files uploaded.");
        if (files.Count > MaxFiles) return api.Bad("TooManyFiles", $"Too many files. Max {MaxFiles} files.");


        NamesRules rules = NamesRulesParser.Parse(form["names"].FirstOrDefault());

        using var outWb = new XLWorkbook();
        int totalSheets = 0;

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            if (file.Length > MaxFileBytes)
                return api.Bad("PayloadTooLarge", $"File '{file.FileName}' exceeds {MaxFileBytes} bytes.");
            var isXlsx = file.ContentType?.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) == true
                || file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);

            if (!isXlsx)
                return api.Bad("InvalidFileType", $"Unsupported file type for '{file.FileName}'. Only .xlsx allowed.");

            try
            {
                await using var inMem = new MemoryStream((int)Math.Min(file.Length, int.MaxValue));
                await file.CopyToAsync(inMem);
                inMem.Position = 0;

                using var srcWb = new XLWorkbook(inMem);
                foreach (var srcSheet in srcWb.Worksheets)
                {
                    if (++totalSheets > MaxSheetsTotal)
                        return api.Bad("TooManyWorksheets", $"Too many worksheets in total. Max {MaxSheetsTotal}.");


                    string proposed = ResolveName(rules, file.FileName, srcSheet.Name);
                    string safe = SafeSheetName(proposed);


                    //Ensure uniqueness or error based on collision policy
                    if (outWb.Worksheets.Any(ws => ws.Name.Equals(safe, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (rules.Collision == CollisionPolicy.Error)
                            return api.Bad("NameCollision", $"The sheet name '{safe}' already exists.");

                        // dedupe (default): suffix _1, _2...
                        safe = Dedup(outWb, safe);
                    }

                    srcSheet.CopyTo(outWb, safe);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to open/merge: {File}", file.FileName);
                return api.Bad("InvalidWorkbook", $"'{file.FileName}' is not a valid .xlsx or is corrupted.");
            }

        }

        await using var outStream = new MemoryStream();

        outWb.SaveAs(outStream);
        var bytes = outStream.ToArray();

        return Results.File(bytes, Application.ExcelXlsx, "merged.xlsx");
    }

    private static string ResolveName(NamesRules rules, string fileName, string sheetName)
    {
        string fileOnly = Path.GetFileName(fileName);

        if (rules.Mode == MergeMode.Map && rules.Map is not null &&
            rules.Map.TryGetValue(fileOnly, out var mapForFile))
        {
            if (mapForFile.TryGetValue(sheetName, out var exact)) return exact;
            if (mapForFile.TryGetValue("*", out var wildcard))
                return ApplyPattern(wildcard, fileOnly, sheetName, 0);
        }

        // pattern mode (default)
        var pattern = rules.Pattern ?? "{file}_{sheet}";
        return ApplyPattern(pattern, fileOnly, sheetName, 0);
    }

    // Supports tokens: {file}, {sheet}, {index}
    private static string ApplyPattern(string pattern, string file, string sheet, int index)
    {
        var baseName = pattern
            .Replace("{file}", Path.GetFileNameWithoutExtension(file))
            .Replace("{sheet}", sheet)
            .Replace("{index}", index.ToString());

        return baseName;
    }

    private static string SafeSheetName(string name)
    {
        // Excel rules: <=31 chars, no : \ / ? * [ ]
        var cleaned = Regex.Replace(name, @"[:\\/\?\*\[\]]", "_").Trim();
        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    private static string Dedup(XLWorkbook wb, string baseName)
    {
        int i = 1;
        string name = baseName;
        while (wb.Worksheets.Any(ws => ws.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            var candidate = $"{baseName}_{i++}";
            name = SafeSheetName(candidate);
        }
        return name;
    }
}
