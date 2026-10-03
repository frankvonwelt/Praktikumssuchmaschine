using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace Praktikumssuchmaschine
{
    // Schreibt eine minimale .xlsx-Datei (ZIP mit XML) ohne externe Bibliothek
    public static class ExcelExport
    {
        public static void Save(string path, IList<Company> companies)
        {
            var sheet = new StringBuilder();
            sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            AppendTextRow(sheet, "Name", "Branche", "Adresse", "Entfernung (km)");
            foreach (var c in companies)
            {
                sheet.Append("<row>");
                sheet.Append(TextCell(c.Name)).Append(TextCell(c.Industry)).Append(TextCell(c.Address));
                sheet.Append("<c><v>").Append(c.DistanceKm.ToString("0.0", CultureInfo.InvariantCulture)).Append("</v></c>");
                sheet.Append("</row>");
            }
            sheet.Append("</sheetData></worksheet>");

            if (File.Exists(path)) File.Delete(path);
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                AddEntry(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
                AddEntry(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                AddEntry(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets><sheet name=\"Betriebe\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                AddEntry(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                AddEntry(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
            }
        }

        private static void AppendTextRow(StringBuilder sb, params string[] values)
        {
            sb.Append("<row>");
            foreach (var v in values) sb.Append(TextCell(v));
            sb.Append("</row>");
        }

        private static string TextCell(string text)
        {
            // Steuerzeichen sind in XML nicht erlaubt
            var clean = Regex.Replace(text ?? "", @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
            return "<c t=\"inlineStr\"><is><t>" + SecurityElement.Escape(clean) + "</t></is></c>";
        }

        private static void AddEntry(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }
    }
}
