using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;

namespace HerramientasSICAR.Logic.Menus
{
    /// <summary>
    /// Genera, a partir de un <see cref="ProyectoMenus"/>, los XML de Openness (TIA Portal V19) que se importan:
    /// las listas de textos de las HMI y el FC CallHMIMenusConfig_FC con el ocultamiento de pantallas de cada HMI.
    /// </summary>
    public static class MenuGenerator
    {
        public const string FcName = "CallHMIMenusConfig_FC";
        public const int FcNumber = 1450;
        public const string ListsFolder = "Listas de textos";

        private static readonly string[] Cultures = { "es-ES", "en-US", "de-DE" };

        /// <summary>Escribe el FC y todas las listas en la carpeta; devuelve las rutas escritas.</summary>
        public static IReadOnlyList<string> Generate(MenuProject project, string folder)
        {
            var errors = project.Errors();
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(" ", errors));

            var written = new List<string>();
            Directory.CreateDirectory(folder);
            string fcPath = Path.Combine(folder, FcName + ".xml");
            Write(fcPath, Fc(project));
            written.Add(fcPath);

            string listsFolder = Path.Combine(folder, ListsFolder);
            Directory.CreateDirectory(listsFolder);
            foreach (var list in TextLists(project))
            {
                string path = Path.Combine(listsFolder, list.Key + ".xml");
                Write(path, list.Value);
                written.Add(path);
            }
            return written;
        }

        // Con BOM, como los XML que exporta TIA.
        private static void Write(string path, string xml) => File.WriteAllText(path, xml, new UTF8Encoding(true));

        // ------------------------------------------------------------------ Listas de textos

        /// <summary>Entrada de una lista de textos: valor y texto en es-ES, en-US y de-DE.</summary>
        public sealed class Entry
        {
            public Entry(int value, string es, string en, string fromItem)
            {
                Value = value;
                Texts = new[] { es, en, fromItem };
            }

            public int Value { get; }
            public IReadOnlyList<string> Texts { get; }
        }

        /// <summary>Todas las listas: nombre → XML.</summary>
        public static IReadOnlyDictionary<string, string> TextLists(MenuProject project)
        {
            var lists = new Dictionary<string, string>();
            foreach (var list in MenuCatalog.Lists)
                lists[list.Name] = ListXml(list.Name, Entries(project, list));
            lists[MenuCatalog.FunctionTextsList] = ListXml(MenuCatalog.FunctionTextsList, FunctionEntries(project));
            foreach (var tipo in MenuCatalog.ProductionDataKinds)
                for (int dataset = 1; dataset <= MenuCatalog.ProductionDatasets; dataset++)
                    lists[tipo.ListName(dataset)] = ListXml(tipo.ListName(dataset), DatasetEntries(project, tipo, dataset));
            return lists;
        }

        /// <summary>Entradas de una lista de nombres de pantallas: siempre todas, con el texto estándar si no hay nombre.</summary>
        public static IReadOnlyList<Entry> Entries(MenuProject project, MenuList list)
        {
            if (list == MenuCatalog.OemList)
                return OemEntries(project);

            var entries = new List<Entry> { Standard(list, 0) };
            for (int value = 1; value <= list.Entries; value++)
            {
                var subgroup = MenuCatalog.Subgroups.FirstOrDefault(s =>
                    s.TargetList == list && value > s.Offset && value <= s.Offset + s.Names);
                var name = subgroup == null ? null : project.Name(subgroup, value - subgroup.Offset);
                entries.Add(name == null || name.IsEmpty ? Standard(list, value) : FromText(value, name));
            }
            return entries;
        }

        // CO_OEM_Screens_ID: el título de cada grupo OEM en N0 y sus 8 pantallas en N1 a N8.
        private static IReadOnlyList<Entry> OemEntries(MenuProject project)
        {
            var list = MenuCatalog.OemList;
            var entries = new List<Entry> { Standard(list, 0) };
            foreach (var subgroup in MenuCatalog.Subgroups.Where(s => s.Group == MenuCatalog.Oem))
            {
                int n = subgroup.Id;
                var title = project.OemTitles[n - 1];
                string defaultTitle = $"OEM_{n:D2}";
                entries.Add(title.IsEmpty ? new Entry(n * 10, defaultTitle, defaultTitle, defaultTitle) : FromText(n * 10, title));
                for (int k = 1; k <= subgroup.Names; k++)
                {
                    var name = project.Name(subgroup, k);
                    string standard = $"<<OEM {n:D2} screen {k:D2}>>";
                    entries.Add(name.IsEmpty ? new Entry(n * 10 + k, standard, standard, standard) : FromText(n * 10 + k, name));
                }
            }
            return entries;
        }

        /// <summary>
        /// CO_UserFunctions: cinco textos por función (pantalla P, función F → valores P·100 + F·10 + 1 a 5, desde 111).
        /// Lo vacío sale vacío, salvo una función sin ningún texto, que lleva los textos estándar.
        /// </summary>
        public static IReadOnlyList<Entry> FunctionEntries(MenuProject project)
        {
            string[] fields = { "Name", "Action left", "Action right", "Feed back left", "Feed back right" };
            var entries = new List<Entry> { new Entry(0, "<<No text>>", "<<No text>>", "<<No text>>") };
            for (int p = 1; p <= MenuCatalog.FunctionScreens; p++)
            {
                for (int f = 1; f <= MenuCatalog.FunctionsPerScreen; f++)
                {
                    var function = project.UserFunctionScreens[p - 1][f - 1];
                    int baseValue = 111 + (p - 1) * 100 + (f - 1) * 10;
                    var texts = function.Texts();
                    for (int c = 0; c < fields.Length; c++)
                    {
                        if (function.IsEmptyFunction)
                        {
                            string standard = $"<<S{p} L{f} {fields[c]}>>";
                            entries.Add(new Entry(baseValue + c, standard, standard, standard));
                        }
                        else
                            entries.Add(FromText(baseValue + c, texts[c]));
                    }
                }
            }
            return entries;
        }

        /// <summary>
        /// SO_ProdData_&lt;tipo&gt;_Dataset_NN: un texto por fila. Un dataset sin ningún texto lleva los estándar en todas
        /// sus filas; si tiene alguno, lo vacío sale vacío.
        /// </summary>
        public static IReadOnlyList<Entry> DatasetEntries(MenuProject project, ProductionDataKind tipo, int dataset)
        {
            var texts = project.Dataset(tipo, dataset);
            bool empty = texts.All(t => t.IsEmpty);
            Entry Standard(int row) => new Entry(row, tipo.StandardText(0, dataset, row), tipo.StandardText(1, dataset, row), tipo.StandardText(2, dataset, row));

            var entries = new List<Entry> { Standard(0) };
            for (int row = 1; row <= tipo.Rows; row++)
                entries.Add(empty ? Standard(row) : FromText(row, texts[row - 1]));
            return entries;
        }

        private static Entry Standard(MenuList list, int value) =>
            new Entry(value, list.StandardText(0, value), list.StandardText(1, value), list.StandardText(2, value));

        private static Entry FromText(int value, HmiText text) =>
            new Entry(value, text.InLanguage(0), text.InLanguage(1), text.InLanguage(2));

        /// <summary>XML de una lista de textos de HMI WinCC clásica, igual que el que exporta TIA.</summary>
        public static string ListXml(string name, IEnumerable<Entry> entries)
        {
            var xml = new Xml();
            xml.Line(0, "<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            xml.Line(0, "<Document>");
            xml.Line(1, "<Engineering version=\"V19\" />");
            xml.Line(1, $"<Hmi.TextGraphicList.TextList ID=\"{xml.Id()}\">");
            xml.Line(2, "<AttributeList>");
            xml.Line(3, "<ListRange>Decimal</ListRange>");
            xml.Line(3, $"<Name>{EscapeXml(name)}</Name>");
            xml.Line(2, "</AttributeList>");
            xml.Line(2, "<ObjectList>");
            xml.Multilingual(3, "Comment", new[] { "", "", "" }, withBody: false);
            foreach (var input in entries)
            {
                xml.Line(3, $"<Hmi.TextGraphicList.TextListEntry ID=\"{xml.Id()}\" CompositionName=\"Entries\">");
                xml.Line(4, "<AttributeList>");
                xml.Line(5, "<DefaultEntry>false</DefaultEntry>");
                xml.Line(5, "<EntryType>SingleValue</EntryType>");
                xml.Line(5, $"<From>{input.Value}</From>");
                xml.Line(5, $"<To>{input.Value}</To>");
                xml.Line(4, "</AttributeList>");
                xml.Line(4, "<ObjectList>");
                xml.Multilingual(5, "Text", input.Texts, withBody: true);
                xml.Line(4, "</ObjectList>");
                xml.Line(3, "</Hmi.TextGraphicList.TextListEntry>");
            }
            xml.Line(2, "</ObjectList>");
            xml.Line(1, "</Hmi.TextGraphicList.TextList>");
            xml.Text.Append("</Document>");
            return xml.Text.ToString();
        }

        // ------------------------------------------------------------------ FC

        /// <summary>Líneas AWL de la configuración de una HMI (lo que va en su segmento STL).</summary>
        public static IReadOnlyList<AwlLine> Awl(HmiMenus hmi)
        {
            var lines = new List<AwlLine>
            {
                AwlLine.CommentText($" --- Configuration for {hmi.Name} ---"),
                AwlLine.Nop(),
                AwlLine.IsEmptyFunction()
            };
            foreach (string text in new[]
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
                lines.Add(text.Length == 0 ? AwlLine.IsEmptyFunction() : AwlLine.CommentText(text));

            MenuGroup currentGroup = null;
            foreach (var subgroup in MenuCatalog.Subgroups.Where(s => s.Positions > 0))
            {
                if (subgroup.Group != currentGroup)
                {
                    currentGroup = subgroup.Group;
                    lines.Add(AwlLine.CommentText($" Group {currentGroup.Id} ({currentGroup.CommentText})"));
                }
                lines.Add(AwlLine.CommentText($" Subgroup {subgroup.Id} ({subgroup.CommentText})"));
                lines.Add(AwlLine.CommentText($" =     HMIMenusConfig0x_IDB.statCfg.Blanking.{currentGroup.PlcName}.{subgroup.PlcName}[y]"));
                lines.Add(AwlLine.IsEmptyFunction());

                var visibleScreens = Enumerable.Range(1, subgroup.Positions).Where(p => hmi.Visible(subgroup, p)).ToList();
                var hidden = Enumerable.Range(1, subgroup.Positions).Where(p => !hmi.Visible(subgroup, p)).ToList();
                // Log. 0 = visible (alwaysFalse), log. 1 = oculta (alwaysTrue).
                Assign(lines, "alwaysFalse", visibleScreens, hmi, subgroup);
                Assign(lines, "alwaysTrue", hidden, hmi, subgroup);
            }
            return lines;
        }

        private static void Assign(List<AwlLine> lines, string mark, List<int> positions, HmiMenus hmi, MenuSubgroup subgroup)
        {
            if (positions.Count == 0)
                return;
            lines.Add(AwlLine.Y(mark));
            foreach (int p in positions)
                lines.Add(AwlLine.Assignment(new[] { hmi.IdbName, "statCfg", "Blanking", subgroup.Group.PlcName, subgroup.PlcName }, p));
            lines.Add(AwlLine.IsEmptyFunction());
        }

        /// <summary>XML del FC CallHMIMenusConfig_FC con las HMI del proyecto, en orden de número.</summary>
        public static string Fc(MenuProject project)
        {
            var xml = new Xml();
            xml.Line(0, "<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            xml.Line(0, "<Document>");
            xml.Line(1, "<Engineering version=\"V19\" />");
            xml.Line(1, $"<SW.Blocks.FC ID=\"{xml.Id()}\">");
            xml.Line(2, "<AttributeList>");
            xml.Line(3, "<AutoNumber>false</AutoNumber>");
            xml.Line(3, "<HeaderAuthor />");
            xml.Line(3, "<HeaderFamily>08_HMI_Management</HeaderFamily>");
            xml.Line(3, "<HeaderName />");
            xml.Line(3, "<HeaderVersion>4.0</HeaderVersion>");
            xml.Text.Append("      <Interface><Sections xmlns=\"http://www.siemens.com/automation/Openness/SW/Interface/v5\">\n");
            foreach (string section in new[] { "Input", "Output", "InOut", "Temp", "Constant" })
                xml.Text.Append($"  <Section Name=\"{section}\" />\n");
            xml.Text.Append("  <Section Name=\"Return\">\n");
            xml.Text.Append("    <Member Name=\"Ret_Val\" Datatype=\"Void\" Accessibility=\"Public\" />\n");
            xml.Text.Append("  </Section>\n");
            xml.Text.Append("</Sections></Interface>\n");
            xml.Line(3, "<IsIECCheckEnabled>false</IsIECCheckEnabled>");
            xml.Line(3, "<MemoryLayout>Standard</MemoryLayout>");
            xml.Line(3, $"<Name>{FcName}</Name>");
            xml.Line(3, "<Namespace />");
            xml.Line(3, $"<Number>{FcNumber}</Number>");
            xml.Line(3, "<ProgrammingLanguage>LAD</ProgrammingLanguage>");
            xml.Line(3, "<SetENOAutomatically>false</SetENOAutomatically>");
            xml.Line(2, "</AttributeList>");
            xml.Line(2, "<ObjectList>");
            xml.Multilingual(3, "Comment", new[] { "", "", "" }, withBody: false);

            foreach (var hmi in project.Hmis.OrderBy(h => h.Number))
            {
                StlNetwork(xml, $"------------------------ HMI {hmi.Number} ------------------------",
                    new[] { AwlLine.IsEmptyFunction(), AwlLine.IsEmptyFunction() });
                StlNetwork(xml, "User configuration of the screen accesses", Awl(hmi));
                CallNetwork(xml, hmi);
            }

            const string title = "Call blocks for the configuration of the accesses to the HMI screens";
            xml.Multilingual(3, "Title", new[] { title, title, "" }, withBody: false);
            xml.Line(2, "</ObjectList>");
            xml.Line(1, "</SW.Blocks.FC>");
            xml.Text.Append("</Document>");
            return xml.Text.ToString();
        }

        private static void StlNetwork(Xml xml, string title, IReadOnlyList<AwlLine> lines)
        {
            xml.Line(3, $"<SW.Blocks.CompileUnit ID=\"{xml.Id()}\" CompositionName=\"CompileUnits\">");
            xml.Line(4, "<AttributeList>");
            var t = xml.Text;
            t.Append("          <NetworkSource><StatementList xmlns=\"http://www.siemens.com/automation/Openness/SW/NetworkSource/StatementList/v5\">\n");
            int uid = 21;
            foreach (var line in lines)
            {
                t.Append($"  <StlStatement UId=\"{uid++}\">\n");
                switch (line.Kind)
                {
                    case AwlLineKind.IsEmptyFunction:
                        t.Append("    <StlToken Text=\"EMPTY_LINE\" />\n");
                        break;
                    case AwlLineKind.CommentText:
                        t.Append("    <LineComment Inserted=\"false\">\n");
                        t.Append($"      <Text>{EscapeXml(line.Text)}</Text>\n");
                        t.Append("    </LineComment>\n");
                        t.Append("    <StlToken Text=\"COMMENT\" />\n");
                        break;
                    case AwlLineKind.Nop:
                        t.Append("    <StlToken Text=\"NOP_0\" />\n");
                        break;
                    case AwlLineKind.Y:
                        t.Append("    <StlToken Text=\"A\" />\n");
                        t.Append("    <Access Scope=\"GlobalVariable\">\n");
                        t.Append("      <Symbol>\n");
                        t.Append($"        <Component Name=\"{EscapeXml(line.Text)}\" />\n");
                        t.Append("      </Symbol>\n");
                        t.Append("    </Access>\n");
                        break;
                    case AwlLineKind.Assignment:
                        t.Append("    <StlToken Text=\"Assign\" />\n");
                        t.Append("    <Access Scope=\"GlobalVariable\">\n");
                        t.Append("      <Symbol>\n");
                        for (int i = 0; i < line.FilePath.Count - 1; i++)
                            t.Append($"        <Component Name=\"{EscapeXml(line.FilePath[i])}\" />\n");
                        t.Append($"        <Component Name=\"{EscapeXml(line.FilePath[line.FilePath.Count - 1])}\" AccessModifier=\"Array\">\n");
                        t.Append("          <Access Scope=\"LiteralConstant\">\n");
                        t.Append("            <Constant>\n");
                        t.Append("              <ConstantType>DInt</ConstantType>\n");
                        t.Append($"              <ConstantValue>{line.Index}</ConstantValue>\n");
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
            xml.Line(5, "<ProgrammingLanguage>STL</ProgrammingLanguage>");
            xml.Line(4, "</AttributeList>");
            NetworkEnd(xml, title);
        }

        private static void CallNetwork(Xml xml, HmiMenus hmi)
        {
            xml.Line(3, $"<SW.Blocks.CompileUnit ID=\"{xml.Id()}\" CompositionName=\"CompileUnits\">");
            xml.Line(4, "<AttributeList>");
            var t = xml.Text;
            t.Append("          <NetworkSource><FlgNet xmlns=\"http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5\">\n");
            t.Append("  <Parts>\n");
            t.Append("    <Call UId=\"21\">\n");
            t.Append("      <CallInfo Name=\"HMIMenusConfig_FB\" BlockType=\"FB\">\n");
            t.Append("        <Instance Scope=\"GlobalVariable\" UId=\"22\">\n");
            t.Append($"          <Component Name=\"{hmi.IdbName}\" />\n");
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
            xml.Line(5, "<ProgrammingLanguage>LAD</ProgrammingLanguage>");
            xml.Line(4, "</AttributeList>");
            NetworkEnd(xml, "Call the management block of the screen accesses ");
        }

        private static void NetworkEnd(Xml xml, string title)
        {
            xml.Line(4, "<ObjectList>");
            xml.Multilingual(5, "Comment", new[] { "", "", "" }, withBody: false);
            xml.Multilingual(5, "Title", new[] { "", title, "" }, withBody: false);
            xml.Line(4, "</ObjectList>");
            xml.Line(3, "</SW.Blocks.CompileUnit>");
        }

        internal static string EscapeXml(string text) => SecurityElement.Escape(text ?? "");

        // Escritor con sangría de dos espacios y los ID hexadecimales consecutivos de los XML de Openness.
        private sealed class Xml
        {
            private int _id;

            public StringBuilder Text { get; } = new StringBuilder();

            public string Id() => (_id++).ToString("X");

            public void Line(int level, string text) => Text.Append(' ', level * 2).Append(text).Append('\n');

            // Texto multilingüe en es-ES, en-US y de-DE. Las listas de textos guardan el texto como <body><p>…</p></body>.
            public void Multilingual(int level, string composition, IReadOnlyList<string> texts, bool withBody)
            {
                Line(level, $"<MultilingualText ID=\"{Id()}\" CompositionName=\"{composition}\">");
                Line(level + 1, "<ObjectList>");
                for (int i = 0; i < Cultures.Length; i++)
                {
                    Line(level + 2, $"<MultilingualTextItem ID=\"{Id()}\" CompositionName=\"Items\">");
                    Line(level + 3, "<AttributeList>");
                    Line(level + 4, $"<Culture>{Cultures[i]}</Culture>");
                    string text = texts[i] ?? "";
                    if (withBody)
                        Line(level + 4, text.Length == 0 ? "<Text><body><p/></body></Text>" : $"<Text><body><p>{EscapeXml(text)}</p></body></Text>");
                    else
                        Line(level + 4, text.Length == 0 ? "<Text />" : $"<Text>{EscapeXml(text)}</Text>");
                    Line(level + 3, "</AttributeList>");
                    Line(level + 2, "</MultilingualTextItem>");
                }
                Line(level + 1, "</ObjectList>");
                Line(level, "</MultilingualText>");
            }
        }
    }

    public enum AwlLineKind { IsEmptyFunction, CommentText, Nop, Y, Assignment }

    /// <summary>Una instrucción del segmento AWL (STL) de una HMI.</summary>
    public sealed class AwlLine
    {
        private AwlLine(AwlLineKind tipo, string text = null, IReadOnlyList<string> path = null, int index = 0)
        {
            Kind = tipo;
            Text = text;
            FilePath = path;
            Index = index;
        }

        public AwlLineKind Kind { get; }

        /// <summary>Texto del comentario, o variable de la instrucción Y.</summary>
        public string Text { get; }

        /// <summary>Componentes de la variable de una asignación, sin el índice.</summary>
        public IReadOnlyList<string> FilePath { get; }

        public int Index { get; }

        public static AwlLine IsEmptyFunction() => new AwlLine(AwlLineKind.IsEmptyFunction);
        public static AwlLine CommentText(string text) => new AwlLine(AwlLineKind.CommentText, text);
        public static AwlLine Nop() => new AwlLine(AwlLineKind.Nop);
        public static AwlLine Y(string variable) => new AwlLine(AwlLineKind.Y, variable);
        public static AwlLine Assignment(IReadOnlyList<string> path, int index) => new AwlLine(AwlLineKind.Assignment, path: path, index: index);

        /// <summary>La instrucción como se ve en el editor de TIA.</summary>
        public override string ToString()
        {
            switch (Kind)
            {
                case AwlLineKind.CommentText: return "//" + Text;
                case AwlLineKind.Nop: return "      NOP 0";
                case AwlLineKind.Y: return "      A     " + Text;
                case AwlLineKind.Assignment: return "      =     " + string.Join(".", FilePath) + "[" + Index + "]";
                default: return "";
            }
        }
    }
}
