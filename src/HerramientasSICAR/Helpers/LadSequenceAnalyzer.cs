using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace HerramientasSICAR.Helpers
{
    public class AsignacionLad
    {
        public string Destino { get; set; }
        public string NombreCorto { get; set; }
        public string Tipo { get; set; }
        public string Valor { get; set; }
        public string Expresion
        {
            get => _expresion ?? Condicion?.ToString() ?? "";
            set => _expresion = value;
        }

        public ExpresionLogica Condicion { get; set; }
        private string _expresion;
    }

    public class PasoSecuencia
    {
        public int Network { get; set; }
        public string Paso { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Lenguaje { get; set; }
        public string IlockAuto { get; set; } = "";
        public string IlockExtSync { get; set; } = "";
        public string Aviso { get; set; } = "";
        public List<AsignacionLad> Asignaciones { get; } = new List<AsignacionLad>();

        /// <summary>Logic of the ilockAuto coil; null when the network does not write it (manual steps).</summary>
        public ExpresionLogica ExpresionIlockAuto { get; set; }

        /// <summary>Logic of the ilockExtSync coil; null when the network does not write it.</summary>
        public ExpresionLogica ExpresionIlockExtSync { get; set; }

        /// <summary>Logic of the transAuto coil; null when the network does not write it.</summary>
        public ExpresionLogica ExpresionTransAuto { get; set; }
    }

    /// <summary>
    /// Interprets the LAD networks of an exported SimaticML block the same way
    /// SIMATIC Automation Compare Tool does: Parts are indexed by UId and the logic
    /// is rebuilt from the Wires topology, never from the order of the Parts.
    /// Each coil / output is then resolved backwards to the powerrail as a boolean expression.
    /// </summary>
    public class LadSequenceAnalyzer
    {
        public List<PasoSecuencia> AnalizarBloque(string rutaXml)
        {
            if (!File.Exists(rutaXml))
                throw new FileNotFoundException(L.T("El archivo XML exportado no existe."), rutaXml);

            XDocument doc = XDocument.Load(rutaXml);

            var bloque = doc.Root.Elements()
                .FirstOrDefault(e => e.Name.LocalName.StartsWith("SW.Blocks.", StringComparison.OrdinalIgnoreCase));
            if (bloque == null)
            {
                string tipo = TipoObjetoRaiz(rutaXml) ?? "desconocido";
                throw new InvalidDataException(
                    L.T("El archivo no es un bloque de programa sino {0}.\n\nAbre el FB de secuencia exportado (p.ej. FB####_<estación>_FB.xml).", DescribirTipo(tipo)));
            }

            string lenguajeBloque = Atributo(bloque, "ProgrammingLanguage") ?? "";

            var pasos = new List<PasoSecuencia>();
            int indice = 0;

            foreach (var cu in bloque.Descendants().Where(e => e.Name.LocalName == "SW.Blocks.CompileUnit"))
            {
                indice++;

                // A compile unit without language inherits it from the block (same as ACT)
                string lenguaje = Atributo(cu, "ProgrammingLanguage") ?? lenguajeBloque;

                var paso = new PasoSecuencia
                {
                    Network = indice,
                    Lenguaje = lenguaje,
                    Titulo = ObtenerTitulo(cu)
                };

                var flgNet = cu.Descendants().FirstOrDefault(e => e.Name.LocalName == "FlgNet");

                if (!lenguaje.Equals("LAD", StringComparison.OrdinalIgnoreCase) &&
                    !lenguaje.Equals("F_LAD", StringComparison.OrdinalIgnoreCase))
                {
                    // Only LAD steps are interpreted; other networks are listed for reference
                    paso.Aviso = "Lenguaje " + lenguaje + " no interpretado";
                    pasos.Add(paso);
                    continue;
                }

                if (flgNet == null)
                {
                    paso.Aviso = "Network vacía";
                    pasos.Add(paso);
                    continue;
                }

                try
                {
                    var red = new RedLad(flgNet);
                    paso.Paso = red.Etiqueta ?? "";
                    paso.Titulo = ObtenerTitulo(cu, paso.Paso);
                    paso.Asignaciones.AddRange(red.ObtenerAsignaciones());
                }
                catch (Exception ex)
                {
                    paso.Aviso = "Error interpretando la network: " + ex.Message;
                }

                // SICAR sequence markers
                // SICAR sync markers: the last plain coil wins, as in the PLC scan
                paso.ExpresionIlockAuto = UltimaBobina(paso.Asignaciones, "ilockAuto");
                paso.ExpresionIlockExtSync = UltimaBobina(paso.Asignaciones, "ilockExtSync");
                paso.ExpresionTransAuto = UltimaBobina(paso.Asignaciones, "transAuto");
                paso.IlockAuto = paso.ExpresionIlockAuto?.ToString() ?? "";
                paso.IlockExtSync = paso.ExpresionIlockExtSync?.ToString() ?? "";

                pasos.Add(paso);
            }

            return pasos;
        }

        /// <summary>Name of the first SimaticML object of the file (SW.Blocks.FB, SW.Tags.PlcTagTable...), reading only its start.</summary>
        public static string TipoObjetoRaiz(string rutaXml)
        {
            using (var reader = System.Xml.XmlReader.Create(rutaXml))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.LocalName.StartsWith("SW.", StringComparison.Ordinal))
                        return reader.LocalName;
                }
            }
            return null;
        }

        public static string DescribirTipo(string tipo)
        {
            switch (tipo)
            {
                case "SW.Tags.PlcTagTable": return L.T("una tabla de variables");
                case "SW.Blocks.GlobalDB":
                case "SW.Blocks.InstanceDB":
                case "SW.Blocks.ArrayDB": return L.T("un DB");
                case "SW.Types.PlcStruct": return L.T("un tipo de datos (UDT)");
                case "SW.Blocks.FB": return L.T("un FB");
                case "SW.Blocks.FC": return L.T("una FC");
                case "SW.Blocks.OB": return L.T("un OB");
                default: return L.T("un objeto de tipo {0}", tipo);
            }
        }

        /// <summary>
        /// Looks in the same folder for the sequence FBs of the station the given file belongs to
        /// (e.g. "++2040+FIX01.xml" → "FB1006_++2040+FIX01_FB.xml"; "++2051+BUF01.xml" → its POS1 and POS2 sequences).
        /// </summary>
        public static List<string> BuscarFbsSecuenciaAsociados(string rutaXml)
        {
            string nombre = Path.GetFileNameWithoutExtension(rutaXml);
            string carpeta = Path.GetDirectoryName(rutaXml);
            if (string.IsNullOrEmpty(carpeta) || !Directory.Exists(carpeta))
                return new List<string>();

            return Directory.GetFiles(carpeta, "*" + nombre + "*.xml")
                .Where(f => !string.Equals(f, rutaXml, StringComparison.OrdinalIgnoreCase))
                .Where(f => Path.GetFileNameWithoutExtension(f).Split('_')
                    .Any(t => t.Equals(nombre, StringComparison.OrdinalIgnoreCase) ||
                              t.StartsWith(nombre + "+", StringComparison.OrdinalIgnoreCase)))
                .Where(f => TipoObjetoRaiz(f) == "SW.Blocks.FB" && File.ReadAllText(f).Contains("Name=\"ilockAuto\""))
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ExpresionLogica UltimaBobina(List<AsignacionLad> asignaciones, string nombre)
        {
            return asignaciones
                .LastOrDefault(a => a.Tipo == "=" && a.NombreCorto.Equals(nombre, StringComparison.OrdinalIgnoreCase))?
                .Condicion;
        }

        private static string Atributo(XElement objeto, string nombre)
        {
            return objeto.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "AttributeList")?
                .Elements()
                .FirstOrDefault(e => e.Name.LocalName == nombre)?
                .Value;
        }

        private static string ObtenerTitulo(XElement cu, string etiqueta = "")
        {
            var titulo = cu.Elements()
                .Where(e => e.Name.LocalName == "ObjectList")
                .Elements()
                .FirstOrDefault(e => e.Name.LocalName == "MultilingualText" &&
                                     (string)e.Attribute("CompositionName") == "Title");
            if (titulo == null)
                return "";

            var textos = titulo.Descendants()
                .Where(e => e.Name.LocalName == "MultilingualTextItem")
                .Select(i => new { Cultura = Atributo(i, "Culture") ?? "", Texto = Atributo(i, "Text") ?? "" })
                .Where(t => !string.IsNullOrWhiteSpace(t.Texto))
                .ToList();

            // Untranslated cultures often keep a stale copy of another step's title,
            // so the text that starts with the step label wins
            var elegido = (string.IsNullOrEmpty(etiqueta) ? null
                              : textos.FirstOrDefault(t => t.Texto.TrimStart().StartsWith(etiqueta, StringComparison.OrdinalIgnoreCase)))
                          ?? textos.FirstOrDefault(t => t.Cultura.StartsWith("es", StringComparison.OrdinalIgnoreCase))
                          ?? textos.FirstOrDefault();
            return elegido?.Texto.Trim() ?? "";
        }

        #region LAD network model

        private class Nodo
        {
            public int UId;
            public string Tipo;        // Access, Part, Call
            public string Nombre;      // instruction name, or operand text for Access
            public string NombreCorto; // last component of the operand
            public string Instancia;   // instance operand for timers / FB calls
            public HashSet<string> Negados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private struct Pin
        {
            public int UId;
            public string Nombre;
            public Pin(int uid, string nombre) { UId = uid; Nombre = nombre; }
        }

        private class Fuente
        {
            public bool Powerrail;
            public bool Abierto;
            public int? Operando;   // UId of an Access
            public Pin? Salida;     // output pin of a Part / Call
        }

        private class RedLad
        {
            private readonly Dictionary<int, Nodo> _nodos = new Dictionary<int, Nodo>();
            private readonly Dictionary<(int, string), Fuente> _entradas = new Dictionary<(int, string), Fuente>();
            private readonly List<(Pin Salida, int Operando)> _salidasAOperando = new List<(Pin, int)>();
            private readonly Dictionary<(int, string), ExpresionLogica> _cache = new Dictionary<(int, string), ExpresionLogica>();
            private readonly HashSet<(int, string)> _enCurso = new HashSet<(int, string)>();

            public string Etiqueta { get; }

            public RedLad(XElement flgNet)
            {
                Etiqueta = flgNet.Elements().FirstOrDefault(e => e.Name.LocalName == "Labels")?
                    .Descendants().FirstOrDefault(e => e.Name.LocalName == "Label")?
                    .Attribute("Name")?.Value;

                var parts = flgNet.Elements().FirstOrDefault(e => e.Name.LocalName == "Parts");
                if (parts != null)
                {
                    foreach (var p in parts.Elements())
                        RegistrarParte(p);
                }

                var wires = flgNet.Elements().FirstOrDefault(e => e.Name.LocalName == "Wires");
                if (wires != null)
                {
                    foreach (var w in wires.Elements().Where(e => e.Name.LocalName == "Wire"))
                        RegistrarWire(w);
                }
            }

            private void RegistrarParte(XElement p)
            {
                int uid = int.Parse((string)p.Attribute("UId"));
                var nodo = new Nodo { UId = uid, Tipo = p.Name.LocalName };

                switch (p.Name.LocalName)
                {
                    case "Access":
                        nodo.Nombre = TextoOperando(p);
                        string ultimo = UltimoComponente(p);
                        nodo.NombreCorto = ultimo != "" ? ultimo : nodo.Nombre;
                        break;

                    case "Part":
                        nodo.Nombre = (string)p.Attribute("Name");
                        foreach (var neg in p.Elements().Where(e => e.Name.LocalName == "Negated"))
                            nodo.Negados.Add((string)neg.Attribute("Name"));
                        var inst = p.Elements().FirstOrDefault(e => e.Name.LocalName == "Instance");
                        if (inst != null)
                            nodo.Instancia = TextoOperando(inst);
                        break;

                    case "Call":
                        var info = p.Elements().FirstOrDefault(e => e.Name.LocalName == "CallInfo");
                        nodo.Nombre = (string)info?.Attribute("Name") ?? "Call";
                        var instCall = info?.Elements().FirstOrDefault(e => e.Name.LocalName == "Instance");
                        if (instCall != null)
                            nodo.Instancia = TextoOperando(instCall);
                        foreach (var neg in p.Descendants().Where(e => e.Name.LocalName == "Negated"))
                            nodo.Negados.Add((string)neg.Attribute("Name"));
                        break;

                    default:
                        return;
                }

                _nodos[uid] = nodo;
            }

            private void RegistrarWire(XElement w)
            {
                var hijos = w.Elements().ToList();
                if (hijos.Count == 0)
                    return;

                Fuente fuente;
                IEnumerable<XElement> destinos;

                if (hijos.Any(h => h.Name.LocalName == "Powerrail"))
                {
                    // Every NameCon on a powerrail wire is fed by the left rail
                    fuente = new Fuente { Powerrail = true };
                    destinos = hijos.Where(h => h.Name.LocalName != "Powerrail");
                }
                else
                {
                    // First connection is the source, the rest are destinations (same rule as ACT)
                    var primero = hijos[0];
                    destinos = hijos.Skip(1);
                    switch (primero.Name.LocalName)
                    {
                        case "IdentCon":
                            fuente = new Fuente { Operando = UIdDe(primero) };
                            break;
                        case "NameCon":
                            fuente = new Fuente { Salida = new Pin(UIdDe(primero), (string)primero.Attribute("Name")) };
                            break;
                        default:
                            fuente = new Fuente { Abierto = true };
                            break;
                    }
                }

                foreach (var d in destinos)
                {
                    switch (d.Name.LocalName)
                    {
                        case "NameCon":
                            _entradas[(UIdDe(d), ((string)d.Attribute("Name")).ToLowerInvariant())] = fuente;
                            break;
                        case "IdentCon":
                            if (fuente.Salida.HasValue)
                                _salidasAOperando.Add((fuente.Salida.Value, UIdDe(d)));
                            break;
                    }
                }
            }

            private static int UIdDe(XElement e) => int.Parse((string)e.Attribute("UId"));

            public IEnumerable<AsignacionLad> ObtenerAsignaciones()
            {
                foreach (var nodo in _nodos.Values.Where(n => n.Tipo != "Access").OrderBy(n => n.UId))
                {
                    string nombre = nodo.Nombre.ToLowerInvariant();
                    string tipo = null;

                    switch (nombre)
                    {
                        case "coil": tipo = "="; break;
                        case "scoil": tipo = "S"; break;
                        case "rcoil": tipo = "R"; break;
                        case "pcoil": tipo = "P="; break;
                        case "ncoil": tipo = "N="; break;
                    }

                    if (tipo != null)
                    {
                        var destino = OperandoDe(nodo.UId, "operand");
                        if (destino == null)
                            continue;
                        yield return new AsignacionLad
                        {
                            Destino = destino.Nombre,
                            NombreCorto = destino.NombreCorto,
                            Tipo = tipo,
                            Condicion = Entrada(nodo.UId, "in")
                        };
                        continue;
                    }

                    if (nombre == "sr" || nombre == "rs")
                    {
                        var destino = OperandoDe(nodo.UId, "operand");
                        if (destino == null)
                            continue;
                        string set = nombre == "sr" ? "s" : "s1";
                        string reset = nombre == "sr" ? "r1" : "r";
                        yield return new AsignacionLad { Destino = destino.Nombre, NombreCorto = destino.NombreCorto, Tipo = "S", Condicion = Entrada(nodo.UId, set) };
                        yield return new AsignacionLad { Destino = destino.Nombre, NombreCorto = destino.NombreCorto, Tipo = "R", Condicion = Entrada(nodo.UId, reset) };
                        continue;
                    }

                    if (nombre == "jump" || nombre == "jumpnot")
                    {
                        var etiqueta = OperandoDe(nodo.UId, "label");
                        yield return new AsignacionLad
                        {
                            Destino = etiqueta?.Nombre ?? "?",
                            NombreCorto = etiqueta?.NombreCorto ?? "",
                            Tipo = nombre == "jump" ? "JMP" : "JMPN",
                            Condicion = Entrada(nodo.UId, "in")
                        };
                        continue;
                    }

                    if (nombre == "returnvalue")
                        continue;

                    // Box outputs written to operands (Move, Add, calls...)
                    var condicion = _entradas.ContainsKey((nodo.UId, "en")) ? Entrada(nodo.UId, "en") : null;
                    foreach (var salida in _salidasAOperando.Where(s => s.Salida.UId == nodo.UId))
                    {
                        if (!_nodos.TryGetValue(salida.Operando, out var destino))
                            continue;

                        string valor = nombre == "move" || nombre == "s_move"
                            ? OperandoDe(nodo.UId, "in")?.Nombre ?? Entrada(nodo.UId, "in").ToString()
                            : nodo.Nombre + "." + salida.Salida.Nombre;

                        yield return new AsignacionLad
                        {
                            Destino = destino.Nombre,
                            NombreCorto = destino.NombreCorto,
                            Tipo = nombre == "move" || nombre == "s_move" ? "MOVE" : nodo.Nombre.ToUpperInvariant(),
                            Valor = valor,
                            Condicion = condicion
                        };
                    }
                }
            }

            private Nodo OperandoDe(int uid, string pin)
            {
                if (_entradas.TryGetValue((uid, pin), out var f) && f.Operando.HasValue &&
                    _nodos.TryGetValue(f.Operando.Value, out var n))
                    return n;
                return null;
            }

            private ExpresionLogica Entrada(int uid, string pin)
            {
                pin = pin.ToLowerInvariant();
                ExpresionLogica e;
                if (!_entradas.TryGetValue((uid, pin), out var f))
                    e = ExpresionLogica.Literal("<sin conexión>");
                else
                    e = Evaluar(f);

                if (_nodos.TryGetValue(uid, out var nodo) && nodo.Negados.Contains(pin))
                    e = ExpresionLogica.Not(e);
                return e;
            }

            private ExpresionLogica Evaluar(Fuente f)
            {
                if (f.Powerrail)
                    return ExpresionLogica.True;
                if (f.Abierto)
                    return ExpresionLogica.Literal("...");
                if (f.Operando.HasValue)
                    return ExpresionLogica.Literal(_nodos.TryGetValue(f.Operando.Value, out var op) ? op.Nombre : "?");
                return Salida(f.Salida.Value.UId, f.Salida.Value.Nombre);
            }

            private ExpresionLogica Salida(int uid, string pin)
            {
                var clave = (uid, pin.ToLowerInvariant());
                if (_cache.TryGetValue(clave, out var guardada))
                    return guardada;
                if (!_enCurso.Add(clave))
                    return ExpresionLogica.Literal("<bucle>");

                var r = CalcularSalida(uid, clave.Item2);
                _enCurso.Remove(clave);
                _cache[clave] = r;
                return r;
            }

            private ExpresionLogica CalcularSalida(int uid, string pin)
            {
                if (!_nodos.TryGetValue(uid, out var nodo))
                    return ExpresionLogica.Literal("?" + uid);

                string nombre = nodo.Nombre.ToLowerInvariant();
                string operando = OperandoDe(uid, "operand")?.Nombre ?? "?";

                switch (nombre)
                {
                    case "contact":
                        {
                            var op = ExpresionLogica.Literal(operando);
                            return ExpresionLogica.And(Entrada(uid, "in"), nodo.Negados.Contains("operand") ? ExpresionLogica.Not(op) : op);
                        }
                    case "pcontact":
                    case "ncontact":
                        return ExpresionLogica.And(Entrada(uid, "pre"), ExpresionLogica.Literal((nombre == "pcontact" ? "P(" : "N(") + operando + ")"));
                    case "pbox":
                    case "nbox":
                        return ExpresionLogica.Literal((nombre == "pbox" ? "P(" : "N(") + Entrada(uid, "in") + ")");
                    case "not":
                        return ExpresionLogica.Not(Entrada(uid, "in"));
                    case "o":
                        return ExpresionLogica.Or(EntradasNumeradas(uid, "in"));
                    case "a":
                        return ExpresionLogica.And(EntradasNumeradas(uid, "in"));
                    case "x":
                        return ExpresionLogica.Literal("XOR(" + string.Join(", ", EntradasNumeradas(uid, "in")) + ")");
                    case "coil":
                    case "scoil":
                    case "rcoil":
                    case "pcoil":
                    case "ncoil":
                        // Coils in series pass the RLO through
                        return Entrada(uid, "in");
                    case "eq":
                    case "ne":
                    case "gt":
                    case "ge":
                    case "lt":
                    case "le":
                        {
                            var cmp = ExpresionLogica.Literal(Comparando(uid, "in1") + " " + Comparador(nombre) + " " + Comparando(uid, "in2"));
                            return ExpresionLogica.And(Entrada(uid, "pre"), cmp);
                        }
                }

                if (pin == "eno")
                    return Entrada(uid, "en");

                // Timers, counters, FB calls...: keep the output as an opaque signal
                string origen = nodo.Instancia ?? (nodo.Nombre + "#" + uid);
                return ExpresionLogica.Literal(origen + "." + pin.ToUpperInvariant());
            }

            private string Comparando(int uid, string pin)
            {
                var op = OperandoDe(uid, pin);
                return op != null ? op.Nombre : Entrada(uid, pin).ToString();
            }

            private static string Comparador(string nombre)
            {
                switch (nombre)
                {
                    case "eq": return "==";
                    case "ne": return "<>";
                    case "gt": return ">";
                    case "ge": return ">=";
                    case "lt": return "<";
                    default: return "<=";
                }
            }

            private List<ExpresionLogica> EntradasNumeradas(int uid, string prefijo)
            {
                return _entradas.Keys
                    .Where(k => k.Item1 == uid && k.Item2.StartsWith(prefijo) &&
                                int.TryParse(k.Item2.Substring(prefijo.Length), out _))
                    .OrderBy(k => int.Parse(k.Item2.Substring(prefijo.Length)))
                    .Select(k => Entrada(uid, k.Item2))
                    .ToList();
            }

            private static string TextoOperando(XElement access)
            {
                string scope = (string)access.Attribute("Scope") ?? "";

                var simbolo = access.Elements().FirstOrDefault(e => e.Name.LocalName == "Symbol");
                if (simbolo != null)
                {
                    var partes = simbolo.Elements().Where(e => e.Name.LocalName == "Component").Select(TextoComponente).ToList();
                    if (scope == "GlobalVariable")
                        return string.Join(".", partes.Select(p => "\"" + p + "\""));
                    return "#" + string.Join(".", partes);
                }

                var constante = access.Elements().FirstOrDefault(e => e.Name.LocalName == "Constant");
                if (constante != null)
                {
                    var valor = constante.Descendants().FirstOrDefault(e => e.Name.LocalName == "ConstantValue");
                    if (valor != null)
                        return valor.Value;
                    string nombre = (string)constante.Attribute("Name") ?? "";
                    return scope == "GlobalConstant" ? "\"" + nombre + "\"" : "#" + nombre;
                }

                var etiqueta = access.Elements().FirstOrDefault(e => e.Name.LocalName == "Label");
                if (etiqueta != null)
                    return (string)etiqueta.Attribute("Name");

                var direccion = access.Elements().FirstOrDefault(e => e.Name.LocalName == "Address");
                if (direccion != null)
                    return (string)direccion.Attribute("Area") + (string)direccion.Attribute("BitOffset");

                return access.Value;
            }

            private static string TextoComponente(XElement componente)
            {
                string nombre = (string)componente.Attribute("Name");
                var indices = componente.Elements().Where(e => e.Name.LocalName == "Access").Select(TextoOperando).ToList();
                return indices.Count > 0 ? nombre + "[" + string.Join(",", indices) + "]" : nombre;
            }

            private static string UltimoComponente(XElement access)
            {
                var simbolo = access.Elements().FirstOrDefault(e => e.Name.LocalName == "Symbol");
                var ultimo = simbolo?.Elements().LastOrDefault(e => e.Name.LocalName == "Component");
                return (string)ultimo?.Attribute("Name") ?? "";
            }
        }

        #endregion
    }
}
