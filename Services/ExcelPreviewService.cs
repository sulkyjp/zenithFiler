using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using ExcelDataReader;

namespace ZenithFiler.Services;

/// <summary>
/// Quick Preview 用にスプレッドシートファイルを DataTable として読み込むサービス。
/// .xlsx / .xls / .xlsm / .csv / .tsv に対応。
/// </summary>
internal static class ExcelPreviewService
{
    private const int MaxRows = 500;
    private const int MaxColumns = 50;

    static ExcelPreviewService()
    {
        // ExcelDataReader が CodePages (Shift_JIS 等) を使えるように登録
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// ファイルをプレビュー用に読み込む。
    /// </summary>
    /// <returns>(DataTable, シート名, 元の総行数, 総シート数)</returns>
    public static (DataTable Table, string SheetName, int TotalRows, int TotalSheets) ReadForPreview(
        string filePath, CancellationToken token)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        bool isCsv = ext is ".csv" or ".tsv";

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        IExcelDataReader reader;
        if (isCsv)
        {
            var csvConfig = new ExcelReaderConfiguration
            {
                FallbackEncoding = Encoding.GetEncoding(932), // Shift_JIS
                AutodetectSeparators = new[] { ',', '\t', ';' },
            };
            reader = ExcelReaderFactory.CreateCsvReader(stream, csvConfig);
        }
        else
        {
            var config = new ExcelReaderConfiguration
            {
                FallbackEncoding = Encoding.GetEncoding(932),
            };
            reader = ExcelReaderFactory.CreateReader(stream, config);
        }

        using (reader)
        {
            token.ThrowIfCancellationRequested();

            // DataSet に変換（Excel: 先頭行をヘッダーとして使用、CSV: ヘッダーなし）
            var dsConfig = new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = !isCsv,
                },
            };

            var dataSet = reader.AsDataSet(dsConfig);
            token.ThrowIfCancellationRequested();

            if (dataSet.Tables.Count == 0)
            {
                var empty = new DataTable("(empty)");
                return (empty, "(empty)", 0, 0);
            }

            var firstTable = dataSet.Tables[0];
            string sheetName = firstTable.TableName;
            int totalRows = firstTable.Rows.Count;
            int totalSheets = dataSet.Tables.Count;

            // トランケート: 行数制限
            var result = firstTable;
            if (totalRows > MaxRows || firstTable.Columns.Count > MaxColumns)
            {
                result = TruncateTable(firstTable, token);
            }

            return (result, sheetName, totalRows, totalSheets);
        }
    }

    private static DataTable TruncateTable(DataTable source, CancellationToken token)
    {
        int colCount = Math.Min(source.Columns.Count, MaxColumns);
        int rowCount = Math.Min(source.Rows.Count, MaxRows);

        var truncated = new DataTable(source.TableName);

        // カラムをコピー（最大 MaxColumns）
        for (int c = 0; c < colCount; c++)
        {
            truncated.Columns.Add(source.Columns[c].ColumnName, typeof(string));
        }

        // 行をコピー（最大 MaxRows）
        for (int r = 0; r < rowCount; r++)
        {
            token.ThrowIfCancellationRequested();
            var newRow = truncated.NewRow();
            for (int c = 0; c < colCount; c++)
            {
                var val = source.Rows[r][c];
                newRow[c] = val == DBNull.Value ? "" : val?.ToString() ?? "";
            }
            truncated.Rows.Add(newRow);
        }

        return truncated;
    }
}
