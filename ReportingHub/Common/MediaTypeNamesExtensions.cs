namespace ReportingHub.Api.Common;

public static class MediaTypeNamesExtensions
{
    public static class Application
    {
        // Built-in System.Net.Mime.MediaTypeNames.Application.Json exists,
        // but we mirror it here for consistency.
        public const string Json = "application/json";

        // Excel OpenXML format (.xlsx)
        public const string ExcelXlsx =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        // Word OpenXML format (.docx)
        public const string WordDocx =
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        // PowerPoint OpenXML format (.pptx)
        public const string PowerPointPptx =
            "application/vnd.openxmlformats-officedocument.presentationml.presentation";

        // CSV text
        public const string Csv = "text/csv";
    }

    public static class Text
    {
        public const string Plain = "text/plain";
    }
}
