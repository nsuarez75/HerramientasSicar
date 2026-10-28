using HerramientasSICAR.Logica;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HerramientasSICAR.Services
{
    /// <summary>
    /// Conexión con un proyecto que ya está abierto en TIA Portal V19. Todas las herramientas trabajan sobre él.
    /// No abre, guarda ni cierra proyectos: eso lo hace el usuario en TIA.
    /// </summary>
    public sealed class TiaOpennessService : IDisposable
    {
        private readonly HiloOpenness _hilo = new HiloOpenness();
        private TiaPortal _tiaPortal;
        private Project _proyecto;
        private string _rutaProyecto;

        /// <summary>Rutas de los proyectos abiertos en las instancias de TIA Portal que están en marcha.</summary>
        public Task<List<string>> ListarProyectosAbiertosAsync()
        {
            return _hilo.Ejecutar(() =>
            {
                // Nunca se llama a Dispose de un TiaPortalProcess: cierra la instancia de TIA del usuario.
                return TiaPortal.GetProcesses()
                    .Where(p => p.ProjectPath != null)
                    .Select(p => p.ProjectPath.FullName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });
        }

        /// <summary>
        /// Se engancha a la instancia de TIA que tiene abierto el proyecto y devuelve sus PLC y HMI,
        /// sin leer lo que tienen dentro (eso es lo que tarda: <see cref="LeerArbolAsync"/>).
        /// </summary>
        public Task<List<DispositivoInfo>> ConectarAsync(string rutaProyecto)
        {
            return _hilo.Ejecutar(() =>
            {
                _rutaProyecto = rutaProyecto;
                _proyecto = BuscarProyecto(rutaProyecto);
                if (_proyecto == null)
                {
                    // Nunca se llama a Dispose de un TiaPortalProcess ni del TiaPortal enganchado:
                    // puede cerrar la instancia de TIA del usuario. Al salir la aplicación se suelta sola.
                    var proceso = TiaPortal.GetProcesses().FirstOrDefault(p => p.ProjectPath != null &&
                        p.ProjectPath.FullName.Equals(rutaProyecto, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException(L.T("El proyecto ya no está abierto en TIA Portal."));

                    // La primera vez TIA pide permiso al usuario para dejar entrar a esta aplicación.
                    _tiaPortal = proceso.Attach();
                    _proyecto = BuscarProyecto(rutaProyecto);
                }
                if (_proyecto == null)
                    throw new InvalidOperationException(L.T("No se pudo acceder al proyecto abierto en TIA Portal."));

                var proyecto = new ProyectoTia(_proyecto);
                return proyecto.Plcs().Select(p => new DispositivoInfo { Nombre = p.Name })
                    .Concat(proyecto.Hmis().Select(h => new DispositivoInfo { Nombre = h.Name, EsHmi = true }))
                    .ToList();
            });
        }

        /// <summary>Lee el árbol de los PLC y HMI indicados, avisando por cuál va (y, en las HMI, por qué rama).</summary>
        public Task<ArbolProyecto> LeerArbolAsync(IReadOnlyCollection<DispositivoInfo> dispositivos, IProgress<string> progreso) => _hilo.Ejecutar(() =>
        {
            ComprobarConexion();
            var proyecto = new ProyectoTia(_proyecto);
            var plcs = proyecto.Plcs().Where(p => dispositivos.Any(d => !d.EsHmi && d.Nombre == p.Name)).ToList();
            var hmis = proyecto.Hmis().Where(h => dispositivos.Any(d => d.EsHmi && d.Nombre == h.Name)).ToList();
            int total = plcs.Count + hmis.Count;
            int n = 0;

            var arbol = new ArbolProyecto();
            foreach (var plc in plcs)
            {
                progreso.Report(L.T("Leyendo el árbol del proyecto: {0} ({1}/{2})…", plc.Name, ++n, total));
                arbol.Plcs.Add(LeerPlc(plc));
            }
            foreach (var hmi in hmis)
            {
                n++;
                arbol.Hmis.Add(HmiTia.Leer(hmi, rama => progreso.Report(
                    L.T("Leyendo el árbol del proyecto: {0} › {1} ({2}/{3})…", hmi.Name, L.T(ArchivosHmi.Nombre(rama)), n, total))));
            }
            return arbol;
        });

        /// <summary>Vuelve a leer los bloques de un PLC del proyecto conectado.</summary>
        public Task<PlcInfo> LeerPlcAsync(string plc) => _hilo.Ejecutar(() =>
        {
            ComprobarConexion();
            return LeerPlc(new ProyectoTia(_proyecto).BuscarPlc(plc));
        });

        /// <summary>Vuelve a leer algunas ramas de una HMI del proyecto conectado; las que el panel no tiene no salen.</summary>
        public Task<List<RamaInfo>> LeerRamasHmiAsync(string hmi, IEnumerable<RamaHmi> ramas) => _hilo.Ejecutar(() =>
        {
            ComprobarConexion();
            return HmiTia.LeerRamas(new ProyectoTia(_proyecto).BuscarHmi(hmi), ramas);
        });

        /// <summary>Ejecuta un trabajo de una herramienta sobre el proyecto conectado, en el hilo de Openness.</summary>
        public Task<T> EnProyecto<T>(Func<ProyectoTia, T> trabajo) => _hilo.Ejecutar(() =>
        {
            ComprobarConexion();
            return trabajo(new ProyectoTia(_proyecto));
        });

        public Task EnProyecto(Action<ProyectoTia> trabajo) => EnProyecto(p => { trabajo(p); return true; });

        /// <summary>
        /// Exporta los bloques y elementos de HMI marcados a la carpeta, respetando las carpetas de TIA.
        /// Los elementos de HMI van en una subcarpeta por rama. Si hay más de un dispositivo, cada uno va en su propia subcarpeta.
        /// </summary>
        public Task<ResultadoOperacion> ExportarAsync(IReadOnlyList<BloqueRef> bloques, IReadOnlyList<ElementoHmiRef> elementosHmi,
            string carpeta, IProgress<string> progreso)
        {
            return _hilo.Ejecutar(() =>
            {
                ComprobarConexion();
                var proyecto = new ProyectoTia(_proyecto);
                var resultado = new ResultadoOperacion();
                bool variosDispositivos = bloques.Select(b => b.Plc).Concat(elementosHmi.Select(e => e.Hmi)).Distinct().Count() > 1;
                int total = bloques.Count + elementosHmi.Count;
                int n = 0;

                foreach (var referencia in bloques)
                {
                    progreso.Report(L.T("Exportando {0}/{1}: {2}", ++n, total, referencia.Nombre));
                    bool seguir = Intentar(resultado, referencia.Nombre, total - n, () =>
                    {
                        var bloque = proyecto.BuscarGrupo(referencia.Plc, referencia.Grupos, crear: false)?.Blocks.Find(referencia.Nombre)
                            ?? throw new InvalidOperationException(L.T("ya no existe en el proyecto"));

                        var partes = new List<string> { carpeta };
                        if (variosDispositivos)
                            partes.Add(ArchivosBloque.NombreSeguro(referencia.Plc));
                        partes.AddRange(referencia.Grupos.Select(ArchivosBloque.NombreSeguro));

                        ProyectoTia.Exportar(bloque, Path.Combine(partes.ToArray()));
                    });
                    if (!seguir)
                        return resultado;
                }

                foreach (var referencia in elementosHmi)
                {
                    progreso.Report(L.T("Exportando {0}/{1}: {2}", ++n, total, referencia.Nombre));
                    bool seguir = Intentar(resultado, referencia.Nombre, total - n, () =>
                    {
                        var partes = new List<string> { carpeta };
                        if (variosDispositivos)
                            partes.Add(ArchivosBloque.NombreSeguro(referencia.Hmi));
                        partes.Add(ArchivosHmi.Nombre(referencia.Rama));
                        partes.AddRange(referencia.Grupos.Select(ArchivosBloque.NombreSeguro));

                        HmiTia.Exportar(proyecto.BuscarHmi(referencia.Hmi), referencia, Path.Combine(partes.ToArray()));
                    });
                    if (!seguir)
                        break;
                }
                return resultado;
            });
        }

        /// <summary>
        /// Importa los XML en el grupo destino, creando los subgrupos que falten.
        /// Los bloques que ya existen con el mismo nombre se sobrescriben.
        /// </summary>
        public Task<ResultadoOperacion> ImportarAsync(string plc, IReadOnlyList<string> gruposDestino,
            IReadOnlyList<ArchivoImportar> archivos, IProgress<string> progreso)
        {
            return _hilo.Ejecutar(() =>
            {
                ComprobarConexion();
                var proyecto = new ProyectoTia(_proyecto);
                var resultado = new ResultadoOperacion();

                for (int i = 0; i < archivos.Count; i++)
                {
                    var archivo = archivos[i];
                    string nombre = Path.GetFileName(archivo.Ruta);
                    progreso.Report(L.T("Importando {0}/{1}: {2}", i + 1, archivos.Count, nombre));
                    bool seguir = Intentar(resultado, nombre, archivos.Count - i - 1, () =>
                    {
                        var grupo = proyecto.BuscarGrupo(plc, gruposDestino.Concat(archivo.Grupos).ToList(), crear: true);
                        grupo.Blocks.Import(new FileInfo(archivo.Ruta), ImportOptions.Override);
                    });
                    if (!seguir)
                        break;
                }
                return resultado;
            });
        }

        /// <summary>
        /// Importa los XML en una HMI, cada uno en su rama. Si lo seleccionado en el árbol es de su misma rama
        /// (<paramref name="rama"/> y <paramref name="grupos"/>), cuelga de ahí. Lo que ya existe con el mismo nombre se sobrescribe.
        /// </summary>
        public Task<ResultadoOperacion> ImportarHmiAsync(string hmi, RamaHmi? rama, IReadOnlyList<string> grupos,
            IReadOnlyList<ArchivoHmi> archivos, IProgress<string> progreso)
        {
            return _hilo.Ejecutar(() =>
            {
                ComprobarConexion();
                var destino = new ProyectoTia(_proyecto).BuscarHmi(hmi);
                var resultado = new ResultadoOperacion();

                for (int i = 0; i < archivos.Count; i++)
                {
                    var archivo = archivos[i];
                    string nombre = Path.GetFileName(archivo.Ruta);
                    progreso.Report(L.T("Importando {0}/{1}: {2}", i + 1, archivos.Count, nombre));
                    if (!Intentar(resultado, nombre, archivos.Count - i - 1,
                            () => HmiTia.Importar(destino, archivo, ArchivosHmi.CarpetasDestino(rama, grupos, archivo))))
                        break;
                }
                return resultado;
            });
        }

        /// <summary>
        /// Hace el trabajo de un elemento y apunta si ha ido bien o el fallo. Devuelve false si TIA se ha cerrado
        /// (o ya no tiene el proyecto): seguir solo llenaría la lista de fallos con el mismo error.
        /// </summary>
        private bool Intentar(ResultadoOperacion resultado, string nombre, int quedan, Action trabajo)
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
                if (!(ex is EngineeringObjectDisposedException) || BuscarProyecto(_rutaProyecto) != null)
                    return true;

                resultado.Fallos.Add(L.T("TIA Portal se ha cerrado o ya no tiene el proyecto abierto: se para aquí y quedan {0} sin hacer.", quedan));
                return false;
            }
        }

        private static PlcInfo LeerPlc(PlcSoftware plc) => new PlcInfo { Nombre = plc.Name, Raiz = LeerGrupo(plc.BlockGroup) };

        private static GrupoInfo LeerGrupo(PlcBlockGroup grupo)
        {
            var info = new GrupoInfo { Nombre = grupo.Name };
            foreach (PlcBlockGroup subgrupo in grupo.Groups)
                info.Grupos.Add(LeerGrupo(subgrupo));
            foreach (PlcBlock bloque in grupo.Blocks)
            {
                info.Bloques.Add(new BloqueInfo
                {
                    Nombre = bloque.Name,
                    Tipo = bloque.GetType().Name.EndsWith("DB", StringComparison.Ordinal) ? "DB" : bloque.GetType().Name,
                    Numero = bloque.Number,
                    Protegido = bloque.IsKnowHowProtected
                });
            }
            return info;
        }

        /// <summary>Comprueba que el proyecto conectado sigue abierto en TIA; si el usuario lo cerró, avisa.</summary>
        private void ComprobarConexion()
        {
            if (_rutaProyecto == null)
                throw new InvalidOperationException(L.T("Conecta con un proyecto de TIA en la barra superior."));
            _proyecto = BuscarProyecto(_rutaProyecto)
                ?? throw new InvalidOperationException(L.T("El proyecto ya no está abierto en TIA Portal. Vuelve a conectar."));
        }

        /// <summary>El proyecto en la instancia ya enganchada, si sigue abierto en ella.</summary>
        private Project BuscarProyecto(string rutaProyecto)
        {
            try
            {
                return _tiaPortal?.Projects.FirstOrDefault(p =>
                    p.Path.FullName.Equals(rutaProyecto, StringComparison.OrdinalIgnoreCase));
            }
            catch (EngineeringObjectDisposedException)
            {
                // El usuario cerró esa instancia de TIA.
                _tiaPortal = null;
                return null;
            }
        }

        public void Dispose()
        {
            _hilo.Dispose();
        }
    }

    /// <summary>
    /// Operaciones de las herramientas sobre el proyecto conectado. Solo se usa dentro de
    /// <see cref="TiaOpennessService.EnProyecto{T}"/>, en el hilo de Openness.
    /// </summary>
    public sealed class ProyectoTia
    {
        private readonly Project _proyecto;

        internal ProyectoTia(Project proyecto)
        {
            _proyecto = proyecto;
        }

        /// <summary>Carpeta del proyecto (la del .ap19): ahí dejan sus archivos las herramientas.</summary>
        public string Carpeta => _proyecto.Path.DirectoryName;

        /// <summary>Compila los bloques de todos los PLC del proyecto.</summary>
        public void CompilarTodo()
        {
            var plcs = Plcs().ToList();
            if (plcs.Count == 0)
                throw new InvalidOperationException(L.T("No se encontró ningún PLC con software en el proyecto."));
            foreach (var plc in plcs)
                plc.BlockGroup.GetService<ICompilable>()?.Compile();
        }

        public void CompilarBloque(string nombreBloque)
        {
            var compilable = Bloque(nombreBloque).GetService<ICompilable>()
                ?? throw new InvalidOperationException(L.T("El bloque no es compilable."));
            compilable.Compile();
        }

        /// <summary>Exporta el bloque a &lt;carpeta&gt;\&lt;nombre&gt;.xml y devuelve la ruta.</summary>
        public string ExportarBloque(string nombreBloque, string carpeta) => Exportar(Bloque(nombreBloque), carpeta);

        /// <summary>Grupo donde está el bloque, o null si no existe.</summary>
        public PlcBlockGroup GrupoDe(string nombreBloque) => BuscarBloque(nombreBloque)?.Parent as PlcBlockGroup;

        /// <summary>Importa el XML en el grupo; si ya hay un bloque con ese nombre, se sobrescribe.</summary>
        public void ImportarBloque(string rutaXml, PlcBlockGroup grupo)
        {
            var archivo = new FileInfo(rutaXml);
            if (!archivo.Exists)
                throw new FileNotFoundException(L.T("Archivo XML de importación no encontrado."), rutaXml);
            grupo.Blocks.Import(archivo, ImportOptions.Override);
        }

        internal static string Exportar(PlcBlock bloque, string carpeta)
        {
            if (bloque.IsKnowHowProtected)
                throw new InvalidOperationException(L.T("está protegido con contraseña (know-how)"));

            // Openness no exporta bloques sin compilar: se compilan antes.
            if (!bloque.IsConsistent)
            {
                bloque.GetService<ICompilable>()?.Compile();
                if (!bloque.IsConsistent)
                    throw new InvalidOperationException(L.T("no compila; revisa sus errores en TIA"));
            }

            string ruta = Path.Combine(carpeta, ArchivosBloque.NombreSeguro(bloque.Name) + ".xml");
            if (ArchivosBloque.RutaDemasiadoLarga(ruta))
                throw new InvalidOperationException(L.T("la ruta es demasiado larga para Windows ({0} caracteres); exporta a una carpeta con una ruta más corta", ruta.Length));

            Directory.CreateDirectory(carpeta);
            var destino = new FileInfo(ruta);
            if (destino.Exists)
                destino.Delete();

            bloque.Export(destino, ExportOptions.WithDefaults);
            return destino.FullName;
        }

        internal IEnumerable<PlcSoftware> Plcs() => Software<PlcSoftware>();

        /// <summary>HMI WinCC clásicas (paneles y estaciones PC). Las Unified no salen: su software es de otro tipo.</summary>
        internal IEnumerable<HmiTarget> Hmis() => Software<HmiTarget>();

        internal HmiTarget BuscarHmi(string nombreHmi) => Hmis().FirstOrDefault(h => h.Name == nombreHmi)
            ?? throw new InvalidOperationException(L.T("no se encuentra la HMI '{0}'", nombreHmi));

        private IEnumerable<T> Software<T>() where T : class
        {
            var dispositivos = new List<Device>(_proyecto.Devices);
            var pendientes = new Queue<DeviceUserGroup>(_proyecto.DeviceGroups);
            while (pendientes.Count > 0)
            {
                var grupo = pendientes.Dequeue();
                dispositivos.AddRange(grupo.Devices);
                foreach (DeviceUserGroup subgrupo in grupo.Groups)
                    pendientes.Enqueue(subgrupo);
            }

            foreach (var dispositivo in dispositivos)
                foreach (var software in BuscarSoftware<T>(dispositivo.DeviceItems))
                    yield return software;
        }

        internal PlcSoftware BuscarPlc(string nombrePlc) => Plcs().FirstOrDefault(p => p.Name == nombrePlc)
            ?? throw new InvalidOperationException(L.T("no se encuentra el PLC '{0}'", nombrePlc));

        internal PlcBlockGroup BuscarGrupo(string nombrePlc, IReadOnlyList<string> grupos, bool crear)
        {
            PlcBlockGroup grupo = BuscarPlc(nombrePlc).BlockGroup;
            foreach (string nombre in grupos)
            {
                PlcBlockGroup siguiente = grupo.Groups.Find(nombre);
                if (siguiente == null)
                {
                    if (!crear)
                        return null;
                    siguiente = grupo.Groups.Create(nombre);
                }
                grupo = siguiente;
            }
            return grupo;
        }

        private PlcBlock Bloque(string nombreBloque) => BuscarBloque(nombreBloque)
            ?? throw new InvalidOperationException(L.T("No se encontró el bloque '{0}' en el proyecto.", nombreBloque));

        /// <summary>Busca el bloque por nombre (sin distinguir mayúsculas) en todos los PLC.</summary>
        private PlcBlock BuscarBloque(string nombreBloque) =>
            Plcs().Select(p => BuscarBloque(p.BlockGroup, nombreBloque.Trim())).FirstOrDefault(b => b != null);

        private static PlcBlock BuscarBloque(PlcBlockGroup grupo, string nombreBloque)
        {
            foreach (PlcBlock bloque in grupo.Blocks)
                if (bloque.Name.Equals(nombreBloque, StringComparison.OrdinalIgnoreCase))
                    return bloque;

            foreach (PlcBlockGroup subgrupo in grupo.Groups)
            {
                var encontrado = BuscarBloque(subgrupo, nombreBloque);
                if (encontrado != null)
                    return encontrado;
            }
            return null;
        }

        private static IEnumerable<T> BuscarSoftware<T>(DeviceItemComposition elementos) where T : class
        {
            foreach (DeviceItem elemento in elementos)
            {
                if (elemento.GetService<SoftwareContainer>()?.Software is T software)
                    yield return software;

                foreach (var interior in BuscarSoftware<T>(elemento.DeviceItems))
                    yield return interior;
            }
        }
    }
}
