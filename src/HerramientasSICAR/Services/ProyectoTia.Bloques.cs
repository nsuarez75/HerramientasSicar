using HerramientasSICAR.Logica;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HerramientasSICAR.Services
{
    /// <summary>
    /// Todo lo que las herramientas hacen con bloques de PLC, en un solo sitio: buscar, exportar, importar,
    /// compilar, el ciclo «exportar → cambiar el XML → importar» con su carpeta temporal y la forma común de apuntar
    /// fallos. Un bloque se identifica siempre por PLC, carpetas y nombre (<see cref="BloqueRef"/>).
    /// </summary>
    public sealed partial class ProyectoTia
    {
        // ------------------------------------------------------------------ Buscar

        /// <summary>Bloques del PLC que cumplen el filtro (carpetas desde la raíz y nombre), en orden del árbol.</summary>
        public List<BloqueRef> BuscarBloques(string nombrePlc, Func<IReadOnlyList<string>, string, bool> filtro)
        {
            var encontrados = new List<BloqueRef>();
            RecorrerBloques(BuscarPlc(nombrePlc).BlockGroup, new List<string>(), (bloque, grupos) =>
            {
                if (filtro(grupos, bloque.Name))
                    encontrados.Add(new BloqueRef { Plc = nombrePlc, Grupos = grupos, Nombre = bloque.Name, Protegido = bloque.IsKnowHowProtected });
            });
            return encontrados;
        }

        /// <summary>Los bloques con ese nombre (sin distinguir mayúsculas) en todos los PLC del proyecto.</summary>
        public List<BloqueRef> BuscarPorNombre(string nombreBloque)
        {
            string nombre = nombreBloque.Trim();
            return Plcs().SelectMany(p => BuscarBloques(p.Name, (_, n) => n.Equals(nombre, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        // Único recorrido del árbol de bloques: cada bloque con las carpetas desde la raíz del PLC hasta él.
        private static void RecorrerBloques(PlcBlockGroup grupo, List<string> ruta, Action<PlcBlock, IReadOnlyList<string>> alBloque)
        {
            foreach (PlcBlock bloque in grupo.Blocks)
                alBloque(bloque, ruta.ToList());
            foreach (PlcBlockGroup sub in grupo.Groups)
            {
                ruta.Add(sub.Name);
                RecorrerBloques(sub, ruta, alBloque);
                ruta.RemoveAt(ruta.Count - 1);
            }
        }

        private PlcBlock Bloque(BloqueRef referencia) =>
            BuscarGrupo(referencia.Plc, referencia.Grupos, crear: false)?.Blocks.Find(referencia.Nombre)
            ?? throw new InvalidOperationException(L.T("ya no existe en el proyecto"));

        // ------------------------------------------------------------------ Compilar

        /// <summary>Compila los bloques de todos los PLC del proyecto.</summary>
        public void CompilarTodo()
        {
            var plcs = Plcs().ToList();
            if (plcs.Count == 0)
                throw new InvalidOperationException(L.T("No se encontró ningún PLC con software en el proyecto."));
            foreach (var plc in plcs)
                plc.BlockGroup.GetService<ICompilable>()?.Compile();
        }

        /// <summary>Compila el bloque; devuelve si ha quedado compilado.</summary>
        private static bool Compilar(PlcBlock bloque)
        {
            bloque.GetService<ICompilable>()?.Compile();
            return bloque.IsConsistent;
        }

        // ------------------------------------------------------------------ Exportar e importar

        /// <summary>Exporta el bloque a &lt;carpeta&gt;\&lt;nombre&gt;.xml (compilándolo antes si hace falta) y devuelve la ruta.</summary>
        public string Exportar(BloqueRef referencia, string carpeta) => ExportarBloque(Bloque(referencia), carpeta, compilar: true, numerarSiExiste: false);

        /// <summary>
        /// La única forma de exportar un bloque. Los protegidos no se pueden exportar. Uno sin compilar se compila antes
        /// si <paramref name="compilar"/>; si no, se salta (el exportador del programa entero no cambia nada del proyecto).
        /// </summary>
        internal static string ExportarBloque(PlcBlock bloque, string carpeta, bool compilar, bool numerarSiExiste)
        {
            if (bloque.IsKnowHowProtected)
                throw new InvalidOperationException(L.T("está protegido con contraseña (know-how)"));
            if (!bloque.IsConsistent)
            {
                if (!compilar)
                    throw new InvalidOperationException(L.T("sin compilar"));
                if (!Compilar(bloque))
                    throw new InvalidOperationException(L.T("no compila; revisa sus errores en TIA"));
            }

            Directory.CreateDirectory(carpeta);
            string ruta = numerarSiExiste ? RutaLibre(carpeta, bloque.Name) : Path.Combine(carpeta, ArchivosBloque.NombreSeguro(bloque.Name) + ".xml");
            if (ArchivosBloque.RutaDemasiadoLarga(ruta))
                throw new InvalidOperationException(L.T("la ruta es demasiado larga para Windows ({0} caracteres); exporta a una carpeta con una ruta más corta", ruta.Length));

            var destino = new FileInfo(ruta);
            if (destino.Exists)
                destino.Delete();
            bloque.Export(destino, ExportOptions.WithDefaults);
            return destino.FullName;
        }

        /// <summary>XML del bloque, sin dejar nada en disco (se exporta a una carpeta temporal que se borra).</summary>
        public string LeerXml(BloqueRef referencia)
        {
            using (var temporal = new CarpetaTemporal())
                return File.ReadAllText(Exportar(referencia, temporal.Ruta));
        }

        // Ruta para un objeto en una carpeta plana: si ya hay uno con ese nombre (dos objetos iguales en carpetas distintas), se numera.
        private static string RutaLibre(string carpeta, string nombre)
        {
            string seguro = ArchivosBloque.NombreSeguro(nombre);
            string ruta = Path.Combine(carpeta, seguro + ".xml");
            for (int i = 2; File.Exists(ruta); i++)
                ruta = Path.Combine(carpeta, $"{seguro}_{i}.xml");
            return ruta;
        }

        /// <summary>
        /// La única forma de importar un bloque: en el grupo, sobrescribiendo el que tenga el mismo nombre.
        /// Con <paramref name="nombreParaCompilar"/>, lo compila después y devuelve si ha quedado compilado.
        /// </summary>
        internal static bool ImportarEn(PlcBlockGroup grupo, string rutaXml, string nombreParaCompilar = null)
        {
            var archivo = new FileInfo(rutaXml);
            if (!archivo.Exists)
                throw new FileNotFoundException(L.T("Archivo XML de importación no encontrado."), rutaXml);
            grupo.Blocks.Import(archivo, ImportOptions.Override);
            if (nombreParaCompilar == null)
                return true;
            var bloque = grupo.Blocks.Find(nombreParaCompilar);
            return bloque != null && Compilar(bloque);
        }

        /// <summary>
        /// Importa el XML de un bloque en un PLC: si el PLC ya tiene un bloque con ese nombre, en su carpeta
        /// (sobrescribiéndolo); si no, en <paramref name="carpetaSiNoExiste"/>, que se crea si hace falta.
        /// </summary>
        public void ImportarBloqueEnPlc(string nombrePlc, string nombreBloque, string rutaXml, IReadOnlyList<string> carpetaSiNoExiste,
            ResultadoOperacion resultado)
        {
            Intentar(resultado, nombreBloque, 0, () =>
            {
                var existente = BuscarBloques(nombrePlc, (_, n) => n.Equals(nombreBloque, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                var grupo = BuscarGrupo(nombrePlc, existente?.Grupos ?? carpetaSiNoExiste, crear: true);
                ImportarEn(grupo, rutaXml);
            });
        }

        // ------------------------------------------------------------------ Exportar → cambiar → importar

        /// <summary>
        /// Exporta los bloques a la carpeta (una subcarpeta por bloque, para que no choquen nombres de PLC distintos).
        /// Lo que falla se apunta en el resultado; devuelve los exportados con su XML.
        /// </summary>
        public List<(BloqueRef Bloque, string Ruta)> ExportarVarios(IReadOnlyList<BloqueRef> bloques, string carpeta,
            IProgress<string> progreso, ResultadoOperacion resultado)
        {
            // Exportar no es el trabajo: aquí solo se apuntan los fallos; los correctos se cuentan al importar.
            var exportacion = new ResultadoOperacion();
            var exportados = new List<(BloqueRef, string)>();
            for (int i = 0; i < bloques.Count; i++)
            {
                var b = bloques[i];
                progreso?.Report(L.T("Exportando {0}/{1}: {2}", i + 1, bloques.Count, b.Nombre));
                string ruta = null;
                bool seguir = Intentar(exportacion, b.Nombre, bloques.Count - i - 1, () => ruta = Exportar(b, Path.Combine(carpeta, i.ToString())));
                if (ruta != null)
                    exportados.Add((b, ruta));
                if (!seguir)
                    break;
            }
            resultado.Fallos.AddRange(exportacion.Fallos);
            return exportados;
        }

        /// <summary>
        /// Importa cada XML en la carpeta de su bloque, sobrescribiéndolo. Con <paramref name="compilar"/>, lo compila
        /// después y avisa si no compila (queda importado igualmente).
        /// </summary>
        public void ImportarVarios(IReadOnlyList<(BloqueRef Bloque, string Ruta)> bloques, bool compilar,
            IProgress<string> progreso, ResultadoOperacion resultado)
        {
            for (int i = 0; i < bloques.Count; i++)
            {
                var (b, ruta) = bloques[i];
                progreso?.Report(L.T("Importando {0}/{1}: {2}", i + 1, bloques.Count, b.Nombre));
                bool seguir = Intentar(resultado, b.Nombre, bloques.Count - i - 1, () =>
                {
                    var grupo = BuscarGrupo(b.Plc, b.Grupos, crear: false) ?? throw new InvalidOperationException(L.T("ya no existe en el proyecto"));
                    if (!ImportarEn(grupo, ruta, compilar ? b.Nombre : null))
                        resultado.Fallos.Add(L.T("{0}: importado, pero no compila; revísalo en TIA", b.Nombre));
                });
                if (!seguir)
                    break;
            }
        }

        /// <summary>
        /// Ciclo completo en el hilo de Openness: exporta los bloques a una carpeta temporal, aplica a cada XML el
        /// cambio (que recibe el bloque y el XML y devuelve el XML nuevo), los importa encima y borra la carpeta.
        /// </summary>
        public ResultadoOperacion ModificarBloques(IReadOnlyList<BloqueRef> bloques, Func<BloqueRef, string, string> cambio,
            bool compilar, IProgress<string> progreso)
        {
            var resultado = new ResultadoOperacion();
            using (var temporal = new CarpetaTemporal())
            {
                var exportados = ExportarVarios(bloques, temporal.Ruta, progreso, resultado);
                var cambiados = new List<(BloqueRef, string)>();
                foreach (var (b, ruta) in exportados)
                {
                    // El cambio es lógica de la herramienta: si falla en un bloque, ese no se importa y se sigue.
                    try
                    {
                        File.WriteAllText(ruta, cambio(b, File.ReadAllText(ruta)), new System.Text.UTF8Encoding(true));
                        cambiados.Add((b, ruta));
                    }
                    catch (Exception ex) when (!(ex is EngineeringObjectDisposedException))
                    {
                        resultado.Fallos.Add($"{b.Nombre}: {ex.Message}");
                    }
                }
                ImportarVarios(cambiados, compilar, progreso, resultado);
            }
            return resultado;
        }

        // ------------------------------------------------------------------ Fallos

        /// <summary>
        /// La forma común de hacer el trabajo de un elemento: si falla, apunta «nombre: motivo» y sigue. Devuelve
        /// false si TIA se ha cerrado o ya no tiene el proyecto: seguir solo llenaría la lista con el mismo error.
        /// </summary>
        internal bool Intentar(ResultadoOperacion resultado, string nombre, int quedan, Action trabajo)
        {
            try
            {
                trabajo();
                resultado.Correctos++;
                return true;
            }
            catch (Exception ex)
            {
                resultado.Fallos.Add($"{nombre}: {ex.Message}");
                if (!(ex is EngineeringObjectDisposedException) || ProyectoAbierto())
                    return true;
                resultado.Fallos.Add(L.T("TIA Portal se ha cerrado o ya no tiene el proyecto abierto: se para aquí y quedan {0} sin hacer.", quedan));
                return false;
            }
        }

        private bool ProyectoAbierto()
        {
            try
            {
                return _proyecto.Name != null;
            }
            catch (EngineeringObjectDisposedException)
            {
                return false;
            }
        }
    }

    /// <summary>Carpeta en %TEMP%\HerramientasSICAR que se borra al terminar, aunque algo falle.</summary>
    public sealed class CarpetaTemporal : IDisposable
    {
        public CarpetaTemporal()
        {
            Ruta = Path.Combine(Path.GetTempPath(), "HerramientasSICAR", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Ruta);
        }

        public string Ruta { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Ruta))
                    Directory.Delete(Ruta, true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Si Windows la tiene bloqueada, se queda en %TEMP%.
            }
        }
    }
}
