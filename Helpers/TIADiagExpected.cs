using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HerramientasSICAR.Helpers
{
    public class TIADiagExpected
    {
        private static readonly Regex _regActuador = new Regex(@"-Y([A-Z])-([HW])", RegexOptions.IgnoreCase);
        private static readonly Regex _regPresencia = new Regex(@"Presencia|PP\d+|Ausencia", RegexOptions.IgnoreCase);

        public void ProcesarYGenerarExcel(string rutaXml, string rutaExcel)
        {
            if (!File.Exists(rutaXml))
                throw new FileNotFoundException("El archivo XML exportado no existe.", rutaXml);

            var filasOriginales = AnalizarBloque(rutaXml);

            List<Dictionary<string, string>> filasCompletas = new List<Dictionary<string, string>>();

            for (int i = 1; i <= 254; i++)
            {
                string paso = "S" + i.ToString("000");

                var encontrado = filasOriginales
                    .FirstOrDefault(f =>
                        f.ContainsKey("NETWORK") &&
                        f["NETWORK"].Equals(paso, StringComparison.OrdinalIgnoreCase));

                if (encontrado != null)
                {
                    filasCompletas.Add(encontrado);
                }
                else
                {
                    filasCompletas.Add(new Dictionary<string, string>()
                    {
                        { "NETWORK", paso }
                    });
                }
            }

            ExportarExcel(filasCompletas, rutaExcel);

            if (!File.Exists(rutaExcel))
                throw new IOException("No se pudo crear el archivo Excel.");
        }

        private List<Dictionary<string, string>> AnalizarBloque(string rutaXml)
        {
            XDocument doc = XDocument.Load(rutaXml);
            List<Dictionary<string, string>> hojaData = new List<Dictionary<string, string>>();

            var networks = doc.Descendants()
                .Where(x => x.Name.LocalName == "FlgNet");

            foreach (var net in networks)
            {
                Dictionary<string, string> fila = new Dictionary<string, string>();

                var lbl = net.Descendants()
                             .FirstOrDefault(x => x.Name.LocalName == "Label");

                string paso = lbl != null ? (string)lbl.Attribute("Name") : "SIN_LABEL";
                fila["NETWORK"] = paso;

                var partsNode = net.Elements()
                                   .FirstOrDefault(x => x.Name.LocalName == "Parts");

                if (partsNode == null)
                    continue;

                var accesos = partsNode.Elements()
                    .Where(x => x.Name.LocalName == "Access")
                    .Select((acc, index) =>
                    {
                        var comp = acc.Descendants()
                            .Where(c => c.Name.LocalName == "Component")
                            .LastOrDefault();

                        string nombre = comp != null && comp.Attribute("Name") != null
                            ? comp.Attribute("Name").Value
                            : "";

                        return new
                        {
                            index,
                            nombre,
                            scope = (string)acc.Attribute("Scope")
                        };
                    })
                    .ToList();

                if (accesos.Count == 0)
                    continue;

                // Markers
                int idxILOCK = accesos
                    .FirstOrDefault(a => a.nombre.Equals("ilockAuto", StringComparison.OrdinalIgnoreCase))?.index
                    ?? int.MaxValue;

                int idxTRANS = accesos
                    .FirstOrDefault(a => a.nombre.Equals("transAuto", StringComparison.OrdinalIgnoreCase))?.index
                    ?? int.MaxValue;

                int idxInicioILOCK = accesos
                    .Where(a => a.scope == "GlobalVariable" && a.index < idxILOCK)
                    .Select(a => a.index)
                    .DefaultIfEmpty(0)
                    .Min();

                foreach (var a in accesos)
                {
                    if (a.scope != "GlobalVariable")
                        continue;

                    string nombre = a.nombre;

                    bool perteneceILOCK = (a.index >= idxInicioILOCK && a.index < idxILOCK);
                    bool perteneceTRANS = (a.index > idxILOCK && a.index < idxTRANS);

                    // -------------------------
                    // ACTUATORS
                    // -------------------------
                    var match = _regActuador.Match(nombre);
                    if (match.Success)
                    {
                        string eje = match.Groups[1].Value;
                        string pos = match.Groups[2].Value;
                        string key = "Y" + eje;

                        string val = "";
                        if (perteneceILOCK)
                            val = pos == "H" ? "'h'" : "'w'";
                        else if (perteneceTRANS)
                            val = pos == "H" ? "'H'" : "'W'";

                        fila[key] = val;
                        continue;
                    }

                    // -------------------------
                    // PRESENCE (includes Absence)
                    // -------------------------
                    if (_regPresencia.IsMatch(nombre))
                    {
                        bool esAusencia = nombre.IndexOf("Ausencia", StringComparison.OrdinalIgnoreCase) >= 0;

                        string val = "";

                        if (perteneceILOCK)
                            val = esAusencia ? "'0'" : "'1'";
                        else if (perteneceTRANS)
                            val = esAusencia ? "'-'" : "'+'";

                        fila[nombre] = val;
                    }
                }

                hojaData.Add(fila);
            }

            return hojaData;
        }

        private void ExportarExcel(List<Dictionary<string, string>> filas, string ruta)
        {
            using (ExcelPackage pck = new ExcelPackage())
            {
                var ws = pck.Workbook.Worksheets.Add("DATA");

                List<string> columnas = new List<string>();
                foreach (var d in filas)
                {
                    foreach (var k in d.Keys)
                        if (!columnas.Contains(k))
                            columnas.Add(k);
                }

                for (int c = 0; c < columnas.Count; c++)
                    ws.Cells[1, c + 1].Value = columnas[c];

                for (int f = 0; f < filas.Count; f++)
                {
                    foreach (var kv in filas[f])
                    {
                        int colIndex = columnas.IndexOf(kv.Key) + 1;
                        ws.Cells[f + 2, colIndex].Value = kv.Value;
                    }
                }

                ws.Cells.AutoFitColumns();
                pck.SaveAs(new FileInfo(ruta));
            }
        }
    }
}
