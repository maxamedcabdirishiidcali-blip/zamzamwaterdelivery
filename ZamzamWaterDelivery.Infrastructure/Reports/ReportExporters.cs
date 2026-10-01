using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Infrastructure.Reports;

public class CsvReportExporter : IReportExporter
{
    public ExportFormat Format => ExportFormat.Csv;
    public string ContentType => "text/csv";
    public string FileExtension => "csv";

    public byte[] Export(ReportDto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Escape(report.Title));
        sb.AppendLine(Escape(report.Subtitle));
        sb.AppendLine();
        sb.AppendLine(string.Join(",", report.Columns.Select(Escape)));
        foreach (var row in report.Rows) sb.AppendLine(string.Join(",", row.Select(Escape)));

        if (report.Summary.Count > 0)
        {
            sb.AppendLine();
            foreach (var (key, value) in report.Summary) sb.AppendLine($"{Escape(key)},{Escape(value)}");
        }

        // UTF-8 BOM so Excel opens the file with the correct encoding.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Escape(string value)
    {
        // Neutralise spreadsheet formula injection and quote special characters.
        if (value.Length > 0 && "=+-@".Contains(value[0]) && !decimal.TryParse(value, out _)) value = "'" + value;
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}

public class ExcelReportExporter : IReportExporter
{
    private static readonly XLColor Brand = XLColor.FromHtml("#0B5ED7");

    public ExportFormat Format => ExportFormat.Excel;
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string FileExtension => "xlsx";

    public byte[] Export(ReportDto report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");

        sheet.Cell(1, 1).Value = report.Title;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(Brand);
        sheet.Cell(2, 1).Value = report.Subtitle;
        sheet.Cell(3, 1).Value = $"Generated {report.GeneratedAt:dd MMM yyyy HH:mm}";
        sheet.Cell(3, 1).Style.Font.SetItalic();

        const int headerRow = 5;
        for (var c = 0; c < report.Columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = report.Columns[c];
            cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(Brand);
        }

        for (var r = 0; r < report.Rows.Count; r++)
        for (var c = 0; c < report.Rows[r].Count; c++)
            sheet.Cell(headerRow + 1 + r, c + 1).Value = report.Rows[r][c];

        if (report.Rows.Count > 0 && report.Columns.Count > 0)
        {
            var range = sheet.Range(headerRow, 1, headerRow + report.Rows.Count, report.Columns.Count);
            range.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Hair);
        }

        var summaryRow = headerRow + report.Rows.Count + 2;
        foreach (var (key, value) in report.Summary)
        {
            sheet.Cell(summaryRow, 1).Value = key;
            sheet.Cell(summaryRow, 1).Style.Font.SetBold();
            sheet.Cell(summaryRow, 2).Value = value;
            summaryRow++;
        }

        sheet.Columns().AdjustToContents(headerRow, summaryRow);
        sheet.SheetView.FreezeRows(headerRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

public class PdfReportExporter : IReportExporter
{
    private const string Brand = "#0B5ED7";
    private const string StripeColor = "#EEF5FF";

    static PdfReportExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public ExportFormat Format => ExportFormat.Pdf;
    public string ContentType => "application/pdf";
    public string FileExtension => "pdf";

    public byte[] Export(ReportDto report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8.5f));

                page.Header().PaddingBottom(8).Column(col =>
                {
                    col.Item().Text("Zamzam Water Delivery").FontSize(10).FontColor(Brand).SemiBold();
                    col.Item().Text(report.Title).FontSize(18).Bold();
                    col.Item().Text(report.Subtitle).FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"Generated {report.GeneratedAt:dd MMM yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().Column(col =>
                {
                    if (report.Summary.Count > 0)
                    {
                        col.Item().PaddingBottom(10).Row(row =>
                        {
                            foreach (var (key, value) in report.Summary)
                            {
                                row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
                                {
                                    c.Item().Text(key).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                                    c.Item().Text(value).FontSize(10).Bold();
                                });
                            }
                        });
                    }

                    if (report.Rows.Count == 0)
                    {
                        col.Item().PaddingTop(20).AlignCenter().Text("No data for the selected period.").Italic();
                        return;
                    }

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in report.Columns) columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            foreach (var column in report.Columns)
                                header.Cell().Background(Brand).Padding(4).Text(column).FontColor(Colors.White).SemiBold();
                        });

                        for (var r = 0; r < report.Rows.Count; r++)
                        {
                            string background = r % 2 == 0 ? "#FFFFFF" : StripeColor;
                            foreach (var value in report.Rows[r])
                                table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(4).Text(value);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}
