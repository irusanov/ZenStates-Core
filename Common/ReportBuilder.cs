using System;
using System.Globalization;
using System.Text;

namespace ZenStates.Core.Common
{
    /// <summary>
    /// Builds a debug report section: the banners, the "Name: value" columns and the byte dumps
    /// every GetReport() writes. Wraps a <see cref="StringBuilder"/> and can stand in for one -
    /// <see cref="Append(string)"/>, <see cref="AppendLine(string)"/> and <see cref="ToString"/>
    /// behave the same, so a report can mix these helpers with plain lines.
    /// <para>
    /// The exact text matters. A report is read back by the mock/debug-report parsers
    /// (Apob.CreateFromDebugReport, PowerTable.CreateFromDebugReport, DebugReportParser), which
    /// locate sections by their banner and values by their label, so these formats are a contract -
    /// change them and older reports, or the parsers, stop lining up.
    /// </para>
    /// <para>
    /// Plain methods rather than extension methods on StringBuilder: the library still targets
    /// net20, which has no ExtensionAttribute.
    /// </para>
    /// </summary>
    public sealed class ReportBuilder
    {
        /// <summary>Width of a "-- Section -----" divider.</summary>
        private const int DividerWidth = 49;

        /// <summary>Width of the "#####" rules above and below a heading.</summary>
        private const int HeadingRuleWidth = 54;

        /// <summary>Default column width for a "Name:" label.</summary>
        public const int LabelWidth = 20;

        private static readonly string HeadingRule = new string('#', HeadingRuleWidth);

        private readonly StringBuilder _sb;

        public ReportBuilder()
        {
            _sb = new StringBuilder();
        }

        public ReportBuilder(StringBuilder stringBuilder)
        {
            _sb = stringBuilder ?? new StringBuilder();
        }

        /// <summary>The report written so far.</summary>
        public StringBuilder Buffer
        {
            get { return _sb; }
        }

        /// <summary>
        /// A top-level section banner: a rule, the title, a rule. This is what the report parsers
        /// scan for when they look up a section by name.
        /// </summary>
        public static string Heading(string heading)
        {
            return HeadingRule + Environment.NewLine +
                   heading + Environment.NewLine +
                   HeadingRule + Environment.NewLine;
        }

        public ReportBuilder AppendHeading(string heading)
        {
            return AppendLine(Heading(heading));
        }

        /// <summary>A divider within a section, e.g. "-- Raw Data -------------------------------------".</summary>
        public ReportBuilder AppendSection(string title)
        {
            return AppendLine(PadRight("-- " + title + " ", DividerWidth, '-'));
        }

        /// <summary>"Name:               value", with "N/A" for a null value.</summary>
        public ReportBuilder AppendValue(string name, object value, int labelWidth = LabelWidth)
        {
            return AppendLine(PadRight(name + ":", labelWidth) + (value ?? "N/A"));
        }

        /// <summary>"Name:               0xDEADBEEF", hex-formatted to <paramref name="digits"/> digits.</summary>
        public ReportBuilder AppendHexValue(string name, ulong value, int digits = 8, int labelWidth = LabelWidth)
        {
            return AppendLine(PadRight(name + ":", labelWidth) +
                                 "0x" + value.ToString("X" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// A decoded value with the raw register value it came from, e.g.
        /// "RttNomWr:          RZQ/3 (80)          (6)".
        /// <para>
        /// A value the table did not carry at all is written as a plain "N/A", with no raw column -
        /// which is what these reports have always done, so older ones still line up.
        /// </para>
        /// </summary>
        public ReportBuilder AppendEncodedValue(string name, EncodedValueBase value, int labelWidth = 19, int valueWidth = 20)
        {
            if (value == null)
                return AppendValue(name, null, labelWidth);

            string raw = value.RawValue.HasValue
                ? value.RawValue.Value.ToString(CultureInfo.InvariantCulture)
                : "null";

            return AppendLine(PadRight(name + ":", labelWidth) +
                              PadRight(value.ToString(), valueWidth) +
                                 "(" + raw + ")");
        }

        /// <summary>
        /// The same, plus the raw value in hex - the wider form the APOB blocks use:
        /// "RttNomWr:           RZQ/3 (80)          6         0x6".
        /// </summary>
        public ReportBuilder AppendEncodedValueWithHex(string name, EncodedValueBase value,
            int labelWidth = LabelWidth, int valueWidth = 20, int rawWidth = 10)
        {
            string raw = "null";
            string rawHex = "null";

            if (value != null && value.RawValue.HasValue)
            {
                raw = value.RawValue.Value.ToString(CultureInfo.InvariantCulture);
                rawHex = "0x" + value.RawValue.Value.ToString("X", CultureInfo.InvariantCulture);
            }

            return AppendLine(PadRight(name + ":", labelWidth) +
                              PadRight(value != null ? value.ToString() : "N/A", valueWidth) +
                              PadRight(raw, rawWidth) +
                                 rawHex);
        }

        /// <summary>
        /// Space-separated hex bytes, 16 to a line. Read back by Apob.CreateFromDebugReport.
        /// </summary>
        /// <returns>False when there is nothing to dump, so the caller can say so in its own words.</returns>
        public bool AppendHexDump(byte[] data, int bytesPerLine = 16)
        {
            if (data == null || data.Length == 0)
                return false;

            for (int i = 0; i < data.Length; i += bytesPerLine)
            {
                int length = Math.Min(bytesPerLine, data.Length - i);

                for (int j = 0; j < length; j++)
                {
                    if (j > 0)
                        _sb.Append(' ');

                    _sb.Append(data[i + j].ToString("X2", CultureInfo.InvariantCulture));
                }

                _sb.AppendLine();
            }

            return true;
        }

        /// <summary>
        /// One byte per line with its index, "Index 027: 5E (94)" - the form the AOD table and the
        /// BIOS memory controller config are dumped in, and the one those parsers expect.
        /// </summary>
        /// <returns>False when there is nothing to dump.</returns>
        public bool AppendIndexedBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
                return false;

            for (int i = 0; i < data.Length; i++)
                _sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Index {0:D3}: {1:X2} ({1})", i, data[i]));

            return true;
        }

        /// <summary>Appends "&lt;FAILED&gt;" and the exception message, the way every report reports its own trouble.</summary>
        public ReportBuilder AppendFailure(Exception ex)
        {
            AppendLine("<FAILED>");
            return AppendLine(ex != null ? ex.Message : "Unknown error");
        }

        public ReportBuilder Append(string text)
        {
            _sb.Append(text);
            return this;
        }

        public ReportBuilder AppendLine()
        {
            _sb.AppendLine();
            return this;
        }

        public ReportBuilder AppendLine(string text)
        {
            _sb.AppendLine(text);
            return this;
        }

        public override string ToString()
        {
            return _sb.ToString();
        }

        // string.PadRight exists on net20, but going through one place keeps the column handling
        // in a single spot - and never truncates, matching what string.Format's alignment did.
        private static string PadRight(string value, int width, char padding = ' ')
        {
            return value.PadRight(width, padding);
        }
    }
}
