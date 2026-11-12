using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HerramientasSICAR.Helpers
{
    public class DiagExpectedGenerator
    {
        private static readonly Regex _actuatorRegex = new Regex(@"-Y([A-Z])-([HW])", RegexOptions.IgnoreCase);
        private static readonly Regex _presenceRegex = new Regex(@"Presencia|PP\d+|Ausencia", RegexOptions.IgnoreCase);

        public void ProcessAndGenerateExcel(string xmlPath, string excelPath)
        {
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException(L.T("El archivo XML exportado no existe."), xmlPath);

            var originalRows = AnalyzeBlock(xmlPath);

            List<Dictionary<string, string>> fullRows = new List<Dictionary<string, string>>();

            for (int i = 1; i <= 254; i++)
            {
                string step = "S" + i.ToString("000");

                var found = originalRows
                    .FirstOrDefault(f =>
                        f.ContainsKey("NETWORK") &&
                        f["NETWORK"].Equals(step, StringComparison.OrdinalIgnoreCase));

                if (found != null)
                {
                    fullRows.Add(found);
                }
                else
                {
                    fullRows.Add(new Dictionary<string, string>()
                    {
                        { "NETWORK", step }
                    });
                }
            }

            ExportExcel(fullRows, excelPath);

            if (!File.Exists(excelPath))
                throw new IOException(L.T("No se pudo crear el archivo Excel."));
        }

        private List<Dictionary<string, string>> AnalyzeBlock(string xmlPath)
        {
            XDocument doc = XDocument.Load(xmlPath);
            List<Dictionary<string, string>> dataSheet = new List<Dictionary<string, string>>();

            var networks = doc.Descendants()
                .Where(x => x.Name.LocalName == "FlgNet");

            foreach (var net in networks)
            {
                Dictionary<string, string> row = new Dictionary<string, string>();

                var lbl = net.Descendants()
                             .FirstOrDefault(x => x.Name.LocalName == "Label");

                string step = lbl != null ? (string)lbl.Attribute("Name") : "SIN_LABEL";
                row["NETWORK"] = step;

                var partsNode = net.Elements()
                                   .FirstOrDefault(x => x.Name.LocalName == "Parts");

                if (partsNode == null)
                    continue;

                var accesses = partsNode.Elements()
                    .Where(x => x.Name.LocalName == "Access")
                    .Select((acc, index) =>
                    {
                        var comp = acc.Descendants()
                            .Where(c => c.Name.LocalName == "Component")
                            .LastOrDefault();

                        string name = comp != null && comp.Attribute("Name") != null
                            ? comp.Attribute("Name").Value
                            : "";

                        return new
                        {
                            index,
                            name,
                            scope = (string)acc.Attribute("Scope")
                        };
                    })
                    .ToList();

                if (accesses.Count == 0)
                    continue;

                // Markers
                int ilockIndex = accesses
                    .FirstOrDefault(a => a.name.Equals("ilockAuto", StringComparison.OrdinalIgnoreCase))?.index
                    ?? int.MaxValue;

                int transIndex = accesses
                    .FirstOrDefault(a => a.name.Equals("transAuto", StringComparison.OrdinalIgnoreCase))?.index
                    ?? int.MaxValue;

                int ilockStartIndex = accesses
                    .Where(a => a.scope == "GlobalVariable" && a.index < ilockIndex)
                    .Select(a => a.index)
                    .DefaultIfEmpty(0)
                    .Min();

                foreach (var a in accesses)
                {
                    if (a.scope != "GlobalVariable")
                        continue;

                    string name = a.name;

                    bool inIlock = (a.index >= ilockStartIndex && a.index < ilockIndex);
                    bool inTrans = (a.index > ilockIndex && a.index < transIndex);

                    // -------------------------
                    // ACTUATORS
                    // -------------------------
                    var match = _actuatorRegex.Match(name);
                    if (match.Success)
                    {
                        string axis = match.Groups[1].Value;
                        string pos = match.Groups[2].Value;
                        string key = "Y" + axis;

                        string val = "";
                        if (inIlock)
                            val = pos == "H" ? "'h'" : "'w'";
                        else if (inTrans)
                            val = pos == "H" ? "'H'" : "'W'";

                        row[key] = val;
                        continue;
                    }

                    // -------------------------
                    // PRESENCE (includes Absence)
                    // -------------------------
                    if (_presenceRegex.IsMatch(name))
                    {
                        bool isAbsence = name.IndexOf("Ausencia", StringComparison.OrdinalIgnoreCase) >= 0;

                        string val = "";

                        if (inIlock)
                            val = isAbsence ? "'0'" : "'1'";
                        else if (inTrans)
                            val = isAbsence ? "'-'" : "'+'";

                        row[name] = val;
                    }
                }

                dataSheet.Add(row);
            }

            return dataSheet;
        }

        private void ExportExcel(List<Dictionary<string, string>> rows, string path)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("DATA");

                List<string> columns = new List<string>();
                foreach (var d in rows)
                {
                    foreach (var k in d.Keys)
                        if (!columns.Contains(k))
                            columns.Add(k);
                }

                for (int c = 0; c < columns.Count; c++)
                    ws.Cell(1, c + 1).Value = columns[c];

                for (int f = 0; f < rows.Count; f++)
                {
                    foreach (var kv in rows[f])
                    {
                        // Una celda sin valor se deja vacía, como hacía EPPlus
                        if (kv.Value == null)
                            continue;

                        int columnIndex = columns.IndexOf(kv.Key) + 1;
                        ws.Cell(f + 2, columnIndex).Value = kv.Value;
                    }
                }

                ws.Columns().AdjustToContents();
                workbook.SaveAs(path);
            }
        }
    }
}
