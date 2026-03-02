using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ZenithFiler.Services
{
    /// <summary>
    /// 画像・Office 文書を PDF へ変換し、複数 PDF を結合するサービス。
    /// 画像→PDF は PDFsharp、Office→PDF は dynamic COM (Word/Excel/PowerPoint)。
    /// </summary>
    internal static class PdfConversionService
    {
        // ──────────────────────────────────────────────────────────────────────
        // 対応拡張子

        private static readonly HashSet<string> ImageFileExts = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif"
        };

        private static readonly HashSet<string> OfficeFileExts = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx", ".doc", ".docm",
            ".xlsx", ".xls", ".xlsm",
            ".pptx", ".ppt", ".pptm"
        };

        internal static bool IsImageFile(string path) =>
            ImageFileExts.Contains(Path.GetExtension(path));

        internal static bool IsOfficeFile(string path) =>
            OfficeFileExts.Contains(Path.GetExtension(path));

        internal static bool IsSupported(string path) =>
            IsImageFile(path) || IsOfficeFile(path);

        // ──────────────────────────────────────────────────────────────────────
        // 画像 → PDF

        /// <summary>単一画像ファイルを 1 ページの PDF に変換して outputPath へ保存する。</summary>
        internal static void CreatePdfFromSingleImage(string imagePath, string outputPath)
        {
            using var doc = new PdfDocument();
            AppendImagePage(doc, imagePath);
            doc.Save(outputPath);
        }

        /// <summary>複数の画像ファイルをそれぞれ 1 ページに割り当てた PDF を生成して outputPath へ保存する。</summary>
        internal static void CreatePdfFromImages(IList<string> imagePaths, string outputPath)
        {
            using var doc = new PdfDocument();
            foreach (var img in imagePaths)
                AppendImagePage(doc, img);
            doc.Save(outputPath);
        }

        private static void AppendImagePage(PdfDocument doc, string imagePath)
        {
            using var ximg = XImage.FromFile(imagePath);
            var page = doc.AddPage();
            page.Width  = XUnit.FromPoint(ximg.PointWidth);
            page.Height = XUnit.FromPoint(ximg.PointHeight);
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawImage(ximg, 0, 0, page.Width.Point, page.Height.Point);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Office → PDF (dynamic COM)

        // Word ExportAsFixedFormat 定数
        private const int WdExportFormatPDF = 17;
        private const int WdAlertsNone      = 0;

        // Excel ExportAsFixedFormat 定数
        private const int XlTypePDF = 0;

        // PowerPoint ExportAsFixedFormat 定数
        private const int PpFixedFormatTypePDF     = 2;
        private const int PpFixedFormatIntentScreen = 2;

        /// <summary>
        /// Office 文書（.docx / .xlsx / .pptx など）を dynamic COM 経由で PDF に変換する。
        /// Office 未インストール時は <see cref="InvalidOperationException"/> をスロー。
        /// </summary>
        internal static void ConvertOfficeToPdf(string inputPath, string outputPath)
        {
            string ext = Path.GetExtension(inputPath).ToLowerInvariant();
            switch (ext)
            {
                case ".docx" or ".doc" or ".docm":
                    ConvertWordToPdf(inputPath, outputPath);
                    break;
                case ".xlsx" or ".xls" or ".xlsm":
                    ConvertExcelToPdf(inputPath, outputPath);
                    break;
                case ".pptx" or ".ppt" or ".pptm":
                    ConvertPowerPointToPdf(inputPath, outputPath);
                    break;
                default:
                    throw new NotSupportedException($"未対応の Office 拡張子: {ext}");
            }
        }

        private static void ConvertWordToPdf(string inputPath, string outputPath)
        {
            var wordType = Type.GetTypeFromProgID("Word.Application")
                ?? throw new InvalidOperationException(
                    "Microsoft Word がインストールされていません。PDF 変換には Microsoft Office が必要です。");

            dynamic? wordApp = null;
            dynamic? doc     = null;
            try
            {
                wordApp = Activator.CreateInstance(wordType)!;
                wordApp.Visible       = false;
                wordApp.DisplayAlerts = WdAlertsNone;

                doc = wordApp.Documents.Open(inputPath, ReadOnly: true);
                doc.ExportAsFixedFormat(outputPath, WdExportFormatPDF);
            }
            finally
            {
                if (doc != null)
                {
                    try { doc.Close(false); } catch { }
                    Marshal.ReleaseComObject(doc);
                }
                if (wordApp != null)
                {
                    try { wordApp.Quit(false); } catch { }
                    Marshal.ReleaseComObject(wordApp);
                }
            }
        }

        private static void ConvertExcelToPdf(string inputPath, string outputPath)
        {
            var excelType = Type.GetTypeFromProgID("Excel.Application")
                ?? throw new InvalidOperationException(
                    "Microsoft Excel がインストールされていません。PDF 変換には Microsoft Office が必要です。");

            dynamic? excelApp = null;
            dynamic? wb       = null;
            try
            {
                excelApp = Activator.CreateInstance(excelType)!;
                excelApp.Visible       = false;
                excelApp.DisplayAlerts = false;

                wb = excelApp.Workbooks.Open(inputPath);
                wb.ExportAsFixedFormat(XlTypePDF, outputPath);
            }
            finally
            {
                if (wb != null)
                {
                    try { wb.Close(false); } catch { }
                    Marshal.ReleaseComObject(wb);
                }
                if (excelApp != null)
                {
                    try { excelApp.Quit(); } catch { }
                    Marshal.ReleaseComObject(excelApp);
                }
            }
        }

        private static void ConvertPowerPointToPdf(string inputPath, string outputPath)
        {
            var pptType = Type.GetTypeFromProgID("PowerPoint.Application")
                ?? throw new InvalidOperationException(
                    "Microsoft PowerPoint がインストールされていません。PDF 変換には Microsoft Office が必要です。");

            dynamic? pptApp = null;
            dynamic? pres   = null;
            try
            {
                pptApp = Activator.CreateInstance(pptType)!;
                pptApp.Visible = false;

                pres = pptApp.Presentations.Open(inputPath, ReadOnly: true, Untitled: false, WithWindow: false);
                pres.ExportAsFixedFormat(outputPath, PpFixedFormatTypePDF,
                    Intent: PpFixedFormatIntentScreen);
            }
            finally
            {
                if (pres != null)
                {
                    try { pres.Close(); } catch { }
                    Marshal.ReleaseComObject(pres);
                }
                if (pptApp != null)
                {
                    try { pptApp.Quit(); } catch { }
                    Marshal.ReleaseComObject(pptApp);
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // PDF 結合

        /// <summary>複数の PDF ファイルをページ順に結合して 1 ファイルとして outputPath へ保存する。</summary>
        internal static void MergePdfs(IList<string> pdfPaths, string outputPath)
        {
            using var outDoc = new PdfDocument();
            foreach (var pdfPath in pdfPaths)
            {
                using var inDoc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
                for (int i = 0; i < inDoc.PageCount; i++)
                    outDoc.AddPage(inDoc.Pages[i]);
            }
            outDoc.Save(outputPath);
        }
    }
}
