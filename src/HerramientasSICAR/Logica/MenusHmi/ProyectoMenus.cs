using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HerramientasSICAR.Logica.MenusHmi
{
    /// <summary>
    /// Texto de una entrada de lista de textos. <see cref="Texto"/> va a los tres idiomas; <see cref="Ingles"/> y
    /// <see cref="Aleman"/>, si se rellenan (a mano o con «Traducir»), sustituyen a Texto en en-US y de-DE.
    /// </summary>
    public sealed class TextoHmi
    {
        public string Texto { get; set; } = "";
        public string Ingles { get; set; } = "";
        public string Aleman { get; set; } = "";

        [JsonIgnore]
        public bool Vacio => string.IsNullOrWhiteSpace(Texto) && string.IsNullOrWhiteSpace(Ingles) && string.IsNullOrWhiteSpace(Aleman);

        /// <summary>Texto en es-ES (0), en-US (1) o de-DE (2).</summary>
        public string EnIdioma(int idioma)
        {
            string propio = idioma == 1 ? Ingles : idioma == 2 ? Aleman : null;
            return string.IsNullOrWhiteSpace(propio) ? (Texto ?? "").Trim() : propio.Trim();
        }

        public static TextoHmi De(string texto) => new TextoHmi { Texto = texto ?? "" };
    }

    /// <summary>Una función de usuario: cinco textos en CO_UserFunctions.</summary>
    public sealed class FuncionUsuario
    {
        public TextoHmi Nombre { get; set; } = new TextoHmi();
        public TextoHmi AccionIzquierda { get; set; } = new TextoHmi();
        public TextoHmi AccionDerecha { get; set; } = new TextoHmi();
        public TextoHmi FeedbackIzquierda { get; set; } = new TextoHmi();
        public TextoHmi FeedbackDerecha { get; set; } = new TextoHmi();

        /// <summary>Los cinco textos en el orden de la lista (valores N1 a N5).</summary>
        public IReadOnlyList<TextoHmi> Textos() => new[] { Nombre, AccionIzquierda, AccionDerecha, FeedbackIzquierda, FeedbackDerecha };

        [JsonIgnore]
        public bool Vacia => Textos().All(t => t.Vacio);
    }

    /// <summary>Una HMI del PLC: su nombre, el número de su IDB y qué pantallas se ven en ella.</summary>
    public sealed class HmiMenus
    {
        public string Nombre { get; set; } = "";

        /// <summary>Número del IDB HMIMenusConfigNN_IDB (1 a 8).</summary>
        public int Numero { get; set; }

        /// <summary>Por subgrupo (clave), si se ve cada posición (índice 0 = posición 1).</summary>
        public Dictionary<string, List<bool>> Visibles { get; set; } = new Dictionary<string, List<bool>>();

        public bool Visible(SubgrupoMenu subgrupo, int posicion) =>
            Visibles.TryGetValue(subgrupo.Clave, out var lista) && posicion >= 1 && posicion <= lista.Count && lista[posicion - 1];

        [JsonIgnore]
        public string NombreIdb => $"HMIMenusConfig{Numero:D2}_IDB";
    }

    /// <summary>
    /// Configuración de los menús de las HMI de un PLC: lo que guarda el Generador HMI en su archivo .hmimenus.
    /// </summary>
    public sealed class ProyectoMenus
    {
        public const string Extension = ".hmimenus";

        public int Version { get; set; } = 1;

        public List<HmiMenus> Hmis { get; set; } = new List<HmiMenus>();

        /// <summary>Títulos de los grupos OEM 1 a 8 (valores 10, 20… de CO_OEM_Screens_ID).</summary>
        public List<TextoHmi> TitulosOem { get; set; } = new List<TextoHmi>();

        /// <summary>Por subgrupo (clave), nombre de cada pantalla (índice 0 = posición 1). Comunes a todas las HMI.</summary>
        public Dictionary<string, List<TextoHmi>> Nombres { get; set; } = new Dictionary<string, List<TextoHmi>>();

        /// <summary>Las 24 pantallas de funciones de usuario, con 8 funciones cada una.</summary>
        public List<List<FuncionUsuario>> FuncionesUsuario { get; set; } = new List<List<FuncionUsuario>>();

        /// <summary>Proyecto vacío con una HMI.</summary>
        public static ProyectoMenus Nuevo()
        {
            var proyecto = new ProyectoMenus();
            proyecto.Completar();
            proyecto.AnadirHmi();
            return proyecto;
        }

        /// <summary>Nombre de una pantalla (vacío si no tiene).</summary>
        public TextoHmi Nombre(SubgrupoMenu subgrupo, int posicion) =>
            Nombres.TryGetValue(subgrupo.Clave, out var lista) && posicion >= 1 && posicion <= lista.Count ? lista[posicion - 1] : new TextoHmi();

        /// <summary>Añade una HMI con el primer número libre y la visibilidad por defecto; null si ya hay 8.</summary>
        public HmiMenus AnadirHmi()
        {
            int numero = Enumerable.Range(1, CatalogoMenus.MaxHmis).FirstOrDefault(n => Hmis.All(h => h.Numero != n));
            if (numero == 0)
                return null;
            var hmi = new HmiMenus { Nombre = $"HMI{numero:D2}", Numero = numero };
            Hmis.Add(hmi);
            Completar();
            Hmis.Sort((a, b) => a.Numero.CompareTo(b.Numero));
            return hmi;
        }

        /// <summary>Rellena lo que falte (archivo antiguo o a medias) para que todas las listas tengan su tamaño.</summary>
        public void Completar()
        {
            Hmis = Hmis ?? new List<HmiMenus>();
            TitulosOem = Ajustar(TitulosOem, CatalogoMenus.GruposOem, _ => new TextoHmi());
            Nombres = Nombres ?? new Dictionary<string, List<TextoHmi>>();
            foreach (var subgrupo in CatalogoMenus.Subgrupos.Where(s => s.Nombres > 0))
            {
                Nombres.TryGetValue(subgrupo.Clave, out var nombres);
                Nombres[subgrupo.Clave] = Ajustar(nombres, subgrupo.Nombres, _ => new TextoHmi());
            }

            FuncionesUsuario = Ajustar(FuncionesUsuario, CatalogoMenus.PantallasFunciones, _ => new List<FuncionUsuario>());
            for (int i = 0; i < FuncionesUsuario.Count; i++)
                FuncionesUsuario[i] = Ajustar(FuncionesUsuario[i], CatalogoMenus.FuncionesPorPantalla, _ => new FuncionUsuario());
            foreach (var funcion in FuncionesUsuario.SelectMany(p => p))
            {
                funcion.Nombre = funcion.Nombre ?? new TextoHmi();
                funcion.AccionIzquierda = funcion.AccionIzquierda ?? new TextoHmi();
                funcion.AccionDerecha = funcion.AccionDerecha ?? new TextoHmi();
                funcion.FeedbackIzquierda = funcion.FeedbackIzquierda ?? new TextoHmi();
                funcion.FeedbackDerecha = funcion.FeedbackDerecha ?? new TextoHmi();
            }

            foreach (var hmi in Hmis)
            {
                hmi.Visibles = hmi.Visibles ?? new Dictionary<string, List<bool>>();
                foreach (var subgrupo in CatalogoMenus.Subgrupos.Where(s => s.Posiciones > 0))
                {
                    hmi.Visibles.TryGetValue(subgrupo.Clave, out var visibles);
                    hmi.Visibles[subgrupo.Clave] = Ajustar(visibles, subgrupo.Posiciones, i => CatalogoMenus.VisiblePorDefecto(subgrupo, i + 1));
                }
            }
        }

        /// <summary>Errores que impiden generar (vacío si se puede).</summary>
        public IReadOnlyList<string> Errores()
        {
            var errores = new List<string>();
            if (Hmis.Count == 0)
                errores.Add(L.T("No hay ninguna HMI."));
            foreach (var repetido in Hmis.GroupBy(h => h.Numero).Where(g => g.Count() > 1))
                errores.Add(L.T("Hay varias HMI con el número {0}.", repetido.Key));
            foreach (var hmi in Hmis.Where(h => h.Numero < 1 || h.Numero > CatalogoMenus.MaxHmis))
                errores.Add(L.T("El número de la HMI {0} tiene que ir del 1 al 8.", hmi.Nombre));
            foreach (var hmi in Hmis.Where(h => string.IsNullOrWhiteSpace(h.Nombre)))
                errores.Add(L.T("La HMI {0} no tiene nombre.", hmi.Numero));
            return errores;
        }

        private static List<T> Ajustar<T>(List<T> lista, int tamano, Func<int, T> nuevo)
        {
            lista = lista ?? new List<T>();
            for (int i = 0; i < lista.Count; i++)
                if (lista[i] == null)
                    lista[i] = nuevo(i);
            while (lista.Count < tamano)
                lista.Add(nuevo(lista.Count));
            if (lista.Count > tamano)
                lista.RemoveRange(tamano, lista.Count - tamano);
            return lista;
        }

        private static readonly JsonSerializerOptions OpcionesJson = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public string AJson() => JsonSerializer.Serialize(this, OpcionesJson);

        public static ProyectoMenus DeJson(string json)
        {
            var proyecto = JsonSerializer.Deserialize<ProyectoMenus>(json, OpcionesJson)
                ?? throw new InvalidDataException(L.T("El archivo no es un proyecto del Generador HMI."));
            proyecto.Completar();
            return proyecto;
        }

        public void Guardar(string ruta) => File.WriteAllText(ruta, AJson(), new UTF8Encoding(false));

        public static ProyectoMenus Abrir(string ruta)
        {
            try
            {
                return DeJson(File.ReadAllText(ruta));
            }
            catch (JsonException)
            {
                throw new InvalidDataException(L.T("El archivo no es un proyecto del Generador HMI."));
            }
        }
    }
}
