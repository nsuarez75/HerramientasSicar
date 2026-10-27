using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    public partial class ComentadorViewModel : Herramienta
    {
        public ComentadorViewModel(Contexto contexto) : base(contexto)
        {
        }

        public override string Nombre => L.T("Comentador");
        public override string Descripcion => L.T("Genera el comentario de los pasos de secuencia en castellano, inglés y alemán a partir del título de cada segmento.");
        public override string Icono => "languages";
        public override bool UsaTia => false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ProcesarCommand))]
        private string _rutaEntrada = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ProcesarCommand))]
        private string _nombreSalida = "";

        [ObservableProperty]
        private bool _traducirTextos = true;

        [ObservableProperty]
        private double _progreso;

        [ObservableProperty]
        private bool _hayProgreso;

        [RelayCommand(CanExecute = nameof(Libre))]
        private async Task ElegirEntrada()
        {
            string ruta = await Contexto.Dialogos.ElegirArchivo(L.T("Lista de textos exportada de TIA"), CarpetaInicial(), TipoArchivo.Excel);
            if (ruta == null)
                return;
            RutaEntrada = ruta;
            if (string.IsNullOrWhiteSpace(NombreSalida))
                NombreSalida = Path.GetFileNameWithoutExtension(ruta) + "_comentada.xlsx";
        }

        [RelayCommand(CanExecute = nameof(PuedeProcesar))]
        private async Task Procesar()
        {
            string salida = NombreSalida.Trim();
            if (!salida.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                salida += ".xlsx";
            string rutaSalida = Path.Combine(Path.GetDirectoryName(RutaEntrada), salida);
            bool traducir = TraducirTextos;

            Progreso = 0;
            HayProgreso = true;
            await Contexto.Trabajar(L.T("Leyendo la lista de textos…"), async () =>
            {
                // Progress<T> devuelve el avance al hilo de la ventana aunque el trabajo vaya en segundo plano.
                var avance = new Progress<(int completados, int total)>(p =>
                {
                    Progreso = p.total > 0 ? 100.0 * p.completados / p.total : 0;
                    Contexto.MostrarEstado(p.total == 0 ? L.T("Preparando…")
                        : traducir ? L.T("Traduciendo títulos: {0}/{1}", p.completados, p.total)
                        : L.T("Copiando títulos: {0}/{1}", p.completados, p.total));
                });

                // Mezcla lectura de Excel con llamadas de red: fuera del hilo de la ventana.
                await Task.Run(() => ExcelProcessor.ModificarTodosComentariosAsync(RutaEntrada, rutaSalida, traducir, avance));
                Contexto.MostrarEstado(L.T("Lista de textos guardada: {0}", rutaSalida));
            });
            HayProgreso = false;
        }

        private string CarpetaInicial() => string.IsNullOrEmpty(RutaEntrada) ? null : Path.GetDirectoryName(RutaEntrada);

        private bool PuedeProcesar() => Libre && File.Exists(RutaEntrada) && !string.IsNullOrWhiteSpace(NombreSalida);

        protected override void ActualizarComandos()
        {
            ElegirEntradaCommand.NotifyCanExecuteChanged();
            ProcesarCommand.NotifyCanExecuteChanged();
        }
    }
}
