using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;

namespace HerramientasSICAR.Logica.MenusHmi
{
    /// <summary>
    /// Genera, a partir de un <see cref="ProyectoMenus"/>, los XML de Openness (TIA Portal V19) que se importan:
    /// las listas de textos de las HMI y el FC CallHMIMenusConfig_FC con el ocultamiento de pantallas de cada HMI.
    /// </summary>
    public static class GeneradorMenus
    {
        public const string NombreFc = "CallHMIMenusConfig_FC";
        public const int NumeroFc = 1450;
        public const string CarpetaListas = "Listas de textos";

        private static readonly string[] Culturas = { "es-ES", "en-US", "de-DE" };

        /// <summary>Escribe el FC y todas las listas en la carpeta; devuelve las rutas escritas.</summary>
        public static IReadOnlyList<string> Generar(ProyectoMenus proyecto, string carpeta)
        {
            var errores = proyecto.Errores();
            if (errores.Count > 0)
                throw new InvalidOperationException(string.Join(" ", errores));

            var escritos = new List<string>();
            Directory.CreateDirectory(carpeta);
            string rutaFc = Path.Combine(carpeta, NombreFc + ".xml");
            Escribir(rutaFc, Fc(proyecto));
            escritos.Add(rutaFc);

            string carpetaListas = Path.Combine(carpeta, CarpetaListas);
            Directory.CreateDirectory(carpetaListas);
            foreach (var lista in ListasTextos(proyecto))
            {
                string ruta = Path.Combine(carpetaListas, lista.Key + ".xml");
                Escribir(ruta, lista.Value);
                escritos.Add(ruta);
            }
            return escritos;
        }

        // Con BOM, como los XML que exporta TIA.
        private static void Escribir(string ruta, string xml) => File.WriteAllText(ruta, xml, new UTF8Encoding(true));

        // ------------------------------------------------------------------ Listas de textos

        /// <summary>Entrada de una lista de textos: valor y texto en es-ES, en-US y de-DE.</summary>
        public sealed class Entrada
        {
            public Entrada(int valor, string es, string en, string de)
            {
                Valor = valor;
                Textos = new[] { es, en, de };
            }

            public int Valor { get; }
            public IReadOnlyList<string> Textos { get; }
        }

        /// <summary>Todas las listas: nombre → XML.</summary>
        public static IReadOnlyDictionary<string, string> ListasTextos(ProyectoMenus proyecto)
        {
            var listas = new Dictionary<string, string>();
            foreach (var lista in CatalogoMenus.Listas)
                listas[lista.Nombre] = XmlLista(lista.Nombre, Entradas(proyecto, lista));
            listas[CatalogoMenus.ListaTextosFunciones] = XmlLista(CatalogoMenus.ListaTextosFunciones, EntradasFunciones(proyecto));
            foreach (var tipo in CatalogoMenus.DatosProduccion)
                for (int dataset = 1; dataset <= CatalogoMenus.DatasetsProduccion; dataset++)
                    listas[tipo.NombreLista(dataset)] = XmlLista(tipo.NombreLista(dataset), EntradasDataset(proyecto, tipo, dataset));
            return listas;
        }

        /// <summary>Entradas de una lista de nombres de pantallas: siempre todas, con el texto estándar si no hay nombre.</summary>
        public static IReadOnlyList<Entrada> Entradas(ProyectoMenus proyecto, ListaMenu lista)
        {
            if (lista == CatalogoMenus.ListaOem)
                return EntradasOem(proyecto);

            var entradas = new List<Entrada> { Estandar(lista, 0) };
            for (int valor = 1; valor <= lista.Entradas; valor++)
            {
                var subgrupo = CatalogoMenus.Subgrupos.FirstOrDefault(s =>
                    s.Lista == lista && valor > s.Desplazamiento && valor <= s.Desplazamiento + s.Nombres);
                var nombre = subgrupo == null ? null : proyecto.Nombre(subgrupo, valor - subgrupo.Desplazamiento);
                entradas.Add(nombre == null || nombre.Vacio ? Estandar(lista, valor) : DeTexto(valor, nombre));
            }
            return entradas;
        }

        // CO_OEM_Screens_ID: el título de cada grupo OEM en N0 y sus 8 pantallas en N1 a N8.
        private static IReadOnlyList<Entrada> EntradasOem(ProyectoMenus proyecto)
        {
            var lista = CatalogoMenus.ListaOem;
            var entradas = new List<Entrada> { Estandar(lista, 0) };
            foreach (var subgrupo in CatalogoMenus.Subgrupos.Where(s => s.Grupo == CatalogoMenus.Oem))
            {
                int n = subgrupo.Id;
                var titulo = proyecto.TitulosOem[n - 1];
                string tituloPorDefecto = $"OEM_{n:D2}";
                entradas.Add(titulo.Vacio ? new Entrada(n * 10, tituloPorDefecto, tituloPorDefecto, tituloPorDefecto) : DeTexto(n * 10, titulo));
                for (int k = 1; k <= subgrupo.Nombres; k++)
                {
                    var nombre = proyecto.Nombre(subgrupo, k);
                    string estandar = $"<<OEM {n:D2} screen {k:D2}>>";
                    entradas.Add(nombre.Vacio ? new Entrada(n * 10 + k, estandar, estandar, estandar) : DeTexto(n * 10 + k, nombre));
                }
            }
            return entradas;
        }

        /// <summary>
        /// CO_UserFunctions: cinco textos por función (pantalla P, función F → valores P·100 + F·10 + 1 a 5, desde 111).
        /// Lo vacío sale vacío, salvo una función sin ningún texto, que lleva los textos estándar.
        /// </summary>
        public static IReadOnlyList<Entrada> EntradasFunciones(ProyectoMenus proyecto)
        {
            string[] campos = { "Name", "Action left", "Action right", "Feed back left", "Feed back right" };
            var entradas = new List<Entrada> { new Entrada(0, "<<No text>>", "<<No text>>", "<<No text>>") };
            for (int p = 1; p <= CatalogoMenus.PantallasFunciones; p++)
            {
                for (int f = 1; f <= CatalogoMenus.FuncionesPorPantalla; f++)
                {
                    var funcion = proyecto.FuncionesUsuario[p - 1][f - 1];
                    int valorBase = 111 + (p - 1) * 100 + (f - 1) * 10;
                    var textos = funcion.Textos();
                    for (int c = 0; c < campos.Length; c++)
                    {
                        if (funcion.Vacia)
                        {
                            string estandar = $"<<S{p} L{f} {campos[c]}>>";
                            entradas.Add(new Entrada(valorBase + c, estandar, estandar, estandar));
                        }
                        else
                            entradas.Add(DeTexto(valorBase + c, textos[c]));
                    }
                }
            }
            return entradas;
        }

        /// <summary>
        /// SO_ProdData_&lt;tipo&gt;_Dataset_NN: un texto por fila. Un dataset sin ningún texto lleva los estándar en todas
        /// sus filas; si tiene alguno, lo vacío sale vacío.
        /// </summary>
        public static IReadOnlyList<Entrada> EntradasDataset(ProyectoMenus proyecto, TipoDatosProduccion tipo, int dataset)
        {
            var textos = proyecto.Dataset(tipo, dataset);
            bool vacio = textos.All(t => t.Vacio);
            Entrada Estandar(int fila) => new Entrada(fila, tipo.TextoEstandar(0, dataset, fila), tipo.TextoEstandar(1, dataset, fila), tipo.TextoEstandar(2, dataset, fila));

            var entradas = new List<Entrada> { Estandar(0) };
            for (int fila = 1; fila <= tipo.Filas; fila++)
                entradas.Add(vacio ? Estandar(fila) : DeTexto(fila, textos[fila - 1]));
            return entradas;
        }

        private static Entrada Estandar(ListaMenu lista, int valor) =>
            new Entrada(valor, lista.TextoEstandar(0, valor), lista.TextoEstandar(1, valor), lista.TextoEstandar(2, valor));

        private static Entrada DeTexto(int valor, TextoHmi texto) =>
            new Entrada(valor, texto.EnIdioma(0), texto.EnIdioma(1), texto.EnIdioma(2));

        /// <summary>XML de una lista de textos de HMI WinCC clásica, igual que el que exporta TIA.</summary>
        public static string XmlLista(string nombre, IEnumerable<Entrada> entradas)
        {
            var xml = new Xml();
            xml.Linea(0, "<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            xml.Linea(0, "<Document>");
            xml.Linea(1, "<Engineering version=\"V19\" />");
            xml.Linea(1, $"<Hmi.TextGraphicList.TextList ID=\"{xml.Id()}\">");
            xml.Linea(2, "<AttributeList>");
            xml.Linea(3, "<ListRange>Decimal</ListRange>");
            xml.Linea(3, $"<Name>{Escapar(nombre)}</Name>");
            xml.Linea(2, "</AttributeList>");
            xml.Linea(2, "<ObjectList>");
            xml.Multilingue(3, "Comment", new[] { "", "", "" }, conCuerpo: false);
            foreach (var entrada in entradas)
            {
                xml.Linea(3, $"<Hmi.TextGraphicList.TextListEntry ID=\"{xml.Id()}\" CompositionName=\"Entries\">");
                xml.Linea(4, "<AttributeList>");
                xml.Linea(5, "<DefaultEntry>false</DefaultEntry>");
                xml.Linea(5, "<EntryType>SingleValue</EntryType>");
                xml.Linea(5, $"<From>{entrada.Valor}</From>");
                xml.Linea(5, $"<To>{entrada.Valor}</To>");
                xml.Linea(4, "</AttributeList>");
                xml.Linea(4, "<ObjectList>");
                xml.Multilingue(5, "Text", entrada.Textos, conCuerpo: true);
                xml.Linea(4, "</ObjectList>");
                xml.Linea(3, "</Hmi.TextGraphicList.TextListEntry>");
            }
            xml.Linea(2, "</ObjectList>");
            xml.Linea(1, "</Hmi.TextGraphicList.TextList>");
            xml.Texto.Append("</Document>");
            return xml.Texto.ToString();
        }

        // ------------------------------------------------------------------ FC

        /// <summary>Líneas AWL de la configuración de una HMI (lo que va en su segmento STL).</summary>
        public static IReadOnlyList<LineaAwl> Awl(HmiMenus hmi)
        {
            var lineas = new List<LineaAwl>
            {
                LineaAwl.Comentario($" --- Configuration for {hmi.Nombre} ---"),
                LineaAwl.Nop(),
                LineaAwl.Vacia()
            };
            foreach (string texto in new[]
            {
                " The screens accesses is configurated for each HMI depending on the existence of the",
                " screen and an additonal user configuration.",
                "",
                " Some screens have a data block associated and the presence of it in the CPU indicates",
                " the existence of the screen.  In this case, the no presence of the data block in the",
                " CPU hides the access of the screen.  But if it is present, the user can also hide it",
                " with an additional configuration due a operational needs.",
                "",
                " Besides, for the screens which do not have a data block associated, their existence and",
                " therefore their access must be defined by the user.",
                "",
                " In both cases, that additional configuration must be programmed in this network, before the",
                " call to the FB450.",
                " For each HMI a call to FB450 must be done and the user additional configuration can also",
                " be done.",
                "",
                " Only the necessary screens and their access should be visible.",
                "",
                " Log. 0 = screen visible / Log. 1 = screen invisible and not accessible",
                ""
            })
                lineas.Add(texto.Length == 0 ? LineaAwl.Vacia() : LineaAwl.Comentario(texto));

            GrupoMenu grupoActual = null;
            foreach (var subgrupo in CatalogoMenus.Subgrupos.Where(s => s.Posiciones > 0))
            {
                if (subgrupo.Grupo != grupoActual)
                {
                    grupoActual = subgrupo.Grupo;
                    lineas.Add(LineaAwl.Comentario($" Group {grupoActual.Id} ({grupoActual.Comentario})"));
                }
                lineas.Add(LineaAwl.Comentario($" Subgroup {subgrupo.Id} ({subgrupo.Comentario})"));
                lineas.Add(LineaAwl.Comentario($" =     HMIMenusConfig0x_IDB.statCfg.Blanking.{grupoActual.NombrePlc}.{subgrupo.NombrePlc}[y]"));
                lineas.Add(LineaAwl.Vacia());

                var visibles = Enumerable.Range(1, subgrupo.Posiciones).Where(p => hmi.Visible(subgrupo, p)).ToList();
                var ocultas = Enumerable.Range(1, subgrupo.Posiciones).Where(p => !hmi.Visible(subgrupo, p)).ToList();
                // Log. 0 = visible (alwaysFalse), log. 1 = oculta (alwaysTrue).
                Asignar(lineas, "alwaysFalse", visibles, hmi, subgrupo);
                Asignar(lineas, "alwaysTrue", ocultas, hmi, subgrupo);
            }
            return lineas;
        }

        private static void Asignar(List<LineaAwl> lineas, string marca, List<int> posiciones, HmiMenus hmi, SubgrupoMenu subgrupo)
        {
            if (posiciones.Count == 0)
                return;
            lineas.Add(LineaAwl.Y(marca));
            foreach (int p in posiciones)
                lineas.Add(LineaAwl.Asignacion(new[] { hmi.NombreIdb, "statCfg", "Blanking", subgrupo.Grupo.NombrePlc, subgrupo.NombrePlc }, p));
            lineas.Add(LineaAwl.Vacia());
        }

        /// <summary>XML del FC CallHMIMenusConfig_FC con las HMI del proyecto, en orden de número.</summary>
        public static string Fc(ProyectoMenus proyecto)
        {
            var xml = new Xml();
            xml.Linea(0, "<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            xml.Linea(0, "<Document>");
            xml.Linea(1, "<Engineering version=\"V19\" />");
            xml.Linea(1, $"<SW.Blocks.FC ID=\"{xml.Id()}\">");
            xml.Linea(2, "<AttributeList>");
            xml.Linea(3, "<AutoNumber>false</AutoNumber>");
            xml.Linea(3, "<HeaderAuthor />");
            xml.Linea(3, "<HeaderFamily>08_HMI_Management</HeaderFamily>");
            xml.Linea(3, "<HeaderName />");
            xml.Linea(3, "<HeaderVersion>4.0</HeaderVersion>");
            xml.Texto.Append("      <Interface><Sections xmlns=\"http://www.siemens.com/automation/Openness/SW/Interface/v5\">\n");
            foreach (string seccion in new[] { "Input", "Output", "InOut", "Temp", "Constant" })
                xml.Texto.Append($"  <Section Name=\"{seccion}\" />\n");
            xml.Texto.Append("  <Section Name=\"Return\">\n");
            xml.Texto.Append("    <Member Name=\"Ret_Val\" Datatype=\"Void\" Accessibility=\"Public\" />\n");
            xml.Texto.Append("  </Section>\n");
            xml.Texto.Append("</Sections></Interface>\n");
            xml.Linea(3, "<IsIECCheckEnabled>false</IsIECCheckEnabled>");
            xml.Linea(3, "<MemoryLayout>Standard</MemoryLayout>");
            xml.Linea(3, $"<Name>{NombreFc}</Name>");
            xml.Linea(3, "<Namespace />");
            xml.Linea(3, $"<Number>{NumeroFc}</Number>");
            xml.Linea(3, "<ProgrammingLanguage>LAD</ProgrammingLanguage>");
            xml.Linea(3, "<SetENOAutomatically>false</SetENOAutomatically>");
            xml.Linea(2, "</AttributeList>");
            xml.Linea(2, "<ObjectList>");
            xml.Multilingue(3, "Comment", new[] { "", "", "" }, conCuerpo: false);

            foreach (var hmi in proyecto.Hmis.OrderBy(h => h.Numero))
            {
                SegmentoStl(xml, $"------------------------ HMI {hmi.Numero} ------------------------",
                    new[] { LineaAwl.Vacia(), LineaAwl.Vacia() });
                SegmentoStl(xml, "User configuration of the screen accesses", Awl(hmi));
                SegmentoLlamada(xml, hmi);
            }

            const string titulo = "Call blocks for the configuration of the accesses to the HMI screens";
            xml.Multilingue(3, "Title", new[] { titulo, titulo, "" }, conCuerpo: false);
            xml.Linea(2, "</ObjectList>");
            xml.Linea(1, "</SW.Blocks.FC>");
            xml.Texto.Append("</Document>");
            return xml.Texto.ToString();
        }

        private static void SegmentoStl(Xml xml, string titulo, IReadOnlyList<LineaAwl> lineas)
        {
            xml.Linea(3, $"<SW.Blocks.CompileUnit ID=\"{xml.Id()}\" CompositionName=\"CompileUnits\">");
            xml.Linea(4, "<AttributeList>");
            var t = xml.Texto;
            t.Append("          <NetworkSource><StatementList xmlns=\"http://www.siemens.com/automation/Openness/SW/NetworkSource/StatementList/v5\">\n");
            int uid = 21;
            foreach (var linea in lineas)
            {
                t.Append($"  <StlStatement UId=\"{uid++}\">\n");
                switch (linea.Tipo)
                {
                    case TipoLineaAwl.Vacia:
                        t.Append("    <StlToken Text=\"EMPTY_LINE\" />\n");
                        break;
                    case TipoLineaAwl.Comentario:
                        t.Append("    <LineComment Inserted=\"false\">\n");
                        t.Append($"      <Text>{Escapar(linea.Texto)}</Text>\n");
                        t.Append("    </LineComment>\n");
                        t.Append("    <StlToken Text=\"COMMENT\" />\n");
                        break;
                    case TipoLineaAwl.Nop:
                        t.Append("    <StlToken Text=\"NOP_0\" />\n");
                        break;
                    case TipoLineaAwl.Y:
                        t.Append("    <StlToken Text=\"A\" />\n");
                        t.Append("    <Access Scope=\"GlobalVariable\">\n");
                        t.Append("      <Symbol>\n");
                        t.Append($"        <Component Name=\"{Escapar(linea.Texto)}\" />\n");
                        t.Append("      </Symbol>\n");
                        t.Append("    </Access>\n");
                        break;
                    case TipoLineaAwl.Asignacion:
                        t.Append("    <StlToken Text=\"Assign\" />\n");
                        t.Append("    <Access Scope=\"GlobalVariable\">\n");
                        t.Append("      <Symbol>\n");
                        for (int i = 0; i < linea.Ruta.Count - 1; i++)
                            t.Append($"        <Component Name=\"{Escapar(linea.Ruta[i])}\" />\n");
                        t.Append($"        <Component Name=\"{Escapar(linea.Ruta[linea.Ruta.Count - 1])}\" AccessModifier=\"Array\">\n");
                        t.Append("          <Access Scope=\"LiteralConstant\">\n");
                        t.Append("            <Constant>\n");
                        t.Append("              <ConstantType>DInt</ConstantType>\n");
                        t.Append($"              <ConstantValue>{linea.Indice}</ConstantValue>\n");
                        t.Append("            </Constant>\n");
                        t.Append("          </Access>\n");
                        t.Append("        </Component>\n");
                        t.Append("      </Symbol>\n");
                        t.Append("    </Access>\n");
                        break;
                }
                t.Append("  </StlStatement>\n");
            }
            t.Append("</StatementList></NetworkSource>\n");
            xml.Linea(5, "<ProgrammingLanguage>STL</ProgrammingLanguage>");
            xml.Linea(4, "</AttributeList>");
            CierreSegmento(xml, titulo);
        }

        private static void SegmentoLlamada(Xml xml, HmiMenus hmi)
        {
            xml.Linea(3, $"<SW.Blocks.CompileUnit ID=\"{xml.Id()}\" CompositionName=\"CompileUnits\">");
            xml.Linea(4, "<AttributeList>");
            var t = xml.Texto;
            t.Append("          <NetworkSource><FlgNet xmlns=\"http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5\">\n");
            t.Append("  <Parts>\n");
            t.Append("    <Call UId=\"21\">\n");
            t.Append("      <CallInfo Name=\"HMIMenusConfig_FB\" BlockType=\"FB\">\n");
            t.Append("        <Instance Scope=\"GlobalVariable\" UId=\"22\">\n");
            t.Append($"          <Component Name=\"{hmi.NombreIdb}\" />\n");
            t.Append("        </Instance>\n");
            t.Append("      </CallInfo>\n");
            t.Append("    </Call>\n");
            t.Append("  </Parts>\n");
            t.Append("  <Wires>\n");
            t.Append("    <Wire UId=\"23\">\n");
            t.Append("      <Powerrail />\n");
            t.Append("      <NameCon UId=\"21\" Name=\"en\" />\n");
            t.Append("    </Wire>\n");
            t.Append("  </Wires>\n");
            t.Append("</FlgNet></NetworkSource>\n");
            xml.Linea(5, "<ProgrammingLanguage>LAD</ProgrammingLanguage>");
            xml.Linea(4, "</AttributeList>");
            CierreSegmento(xml, "Call the management block of the screen accesses ");
        }

        private static void CierreSegmento(Xml xml, string titulo)
        {
            xml.Linea(4, "<ObjectList>");
            xml.Multilingue(5, "Comment", new[] { "", "", "" }, conCuerpo: false);
            xml.Multilingue(5, "Title", new[] { "", titulo, "" }, conCuerpo: false);
            xml.Linea(4, "</ObjectList>");
            xml.Linea(3, "</SW.Blocks.CompileUnit>");
        }

        internal static string Escapar(string texto) => SecurityElement.Escape(texto ?? "");

        // Escritor con sangría de dos espacios y los ID hexadecimales consecutivos de los XML de Openness.
        private sealed class Xml
        {
            private int _id;

            public StringBuilder Texto { get; } = new StringBuilder();

            public string Id() => (_id++).ToString("X");

            public void Linea(int nivel, string texto) => Texto.Append(' ', nivel * 2).Append(texto).Append('\n');

            // Texto multilingüe en es-ES, en-US y de-DE. Las listas de textos guardan el texto como <body><p>…</p></body>.
            public void Multilingue(int nivel, string composicion, IReadOnlyList<string> textos, bool conCuerpo)
            {
                Linea(nivel, $"<MultilingualText ID=\"{Id()}\" CompositionName=\"{composicion}\">");
                Linea(nivel + 1, "<ObjectList>");
                for (int i = 0; i < Culturas.Length; i++)
                {
                    Linea(nivel + 2, $"<MultilingualTextItem ID=\"{Id()}\" CompositionName=\"Items\">");
                    Linea(nivel + 3, "<AttributeList>");
                    Linea(nivel + 4, $"<Culture>{Culturas[i]}</Culture>");
                    string texto = textos[i] ?? "";
                    if (conCuerpo)
                        Linea(nivel + 4, texto.Length == 0 ? "<Text><body><p/></body></Text>" : $"<Text><body><p>{Escapar(texto)}</p></body></Text>");
                    else
                        Linea(nivel + 4, texto.Length == 0 ? "<Text />" : $"<Text>{Escapar(texto)}</Text>");
                    Linea(nivel + 3, "</AttributeList>");
                    Linea(nivel + 2, "</MultilingualTextItem>");
                }
                Linea(nivel + 1, "</ObjectList>");
                Linea(nivel, "</MultilingualText>");
            }
        }
    }

    public enum TipoLineaAwl { Vacia, Comentario, Nop, Y, Asignacion }

    /// <summary>Una instrucción del segmento AWL (STL) de una HMI.</summary>
    public sealed class LineaAwl
    {
        private LineaAwl(TipoLineaAwl tipo, string texto = null, IReadOnlyList<string> ruta = null, int indice = 0)
        {
            Tipo = tipo;
            Texto = texto;
            Ruta = ruta;
            Indice = indice;
        }

        public TipoLineaAwl Tipo { get; }

        /// <summary>Texto del comentario, o variable de la instrucción Y.</summary>
        public string Texto { get; }

        /// <summary>Componentes de la variable de una asignación, sin el índice.</summary>
        public IReadOnlyList<string> Ruta { get; }

        public int Indice { get; }

        public static LineaAwl Vacia() => new LineaAwl(TipoLineaAwl.Vacia);
        public static LineaAwl Comentario(string texto) => new LineaAwl(TipoLineaAwl.Comentario, texto);
        public static LineaAwl Nop() => new LineaAwl(TipoLineaAwl.Nop);
        public static LineaAwl Y(string variable) => new LineaAwl(TipoLineaAwl.Y, variable);
        public static LineaAwl Asignacion(IReadOnlyList<string> ruta, int indice) => new LineaAwl(TipoLineaAwl.Asignacion, ruta: ruta, indice: indice);

        /// <summary>La instrucción como se ve en el editor de TIA.</summary>
        public override string ToString()
        {
            switch (Tipo)
            {
                case TipoLineaAwl.Comentario: return "//" + Texto;
                case TipoLineaAwl.Nop: return "      NOP 0";
                case TipoLineaAwl.Y: return "      A     " + Texto;
                case TipoLineaAwl.Asignacion: return "      =     " + string.Join(".", Ruta) + "[" + Indice + "]";
                default: return "";
            }
        }
    }
}
