using CommunityToolkit.Mvvm.ComponentModel;
using HerramientasSICAR.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>
    /// Lo que comparten la ventana y las herramientas: la conexión con TIA, los diálogos
    /// y la barra de estado de abajo.
    /// </summary>
    public partial class Contexto : ObservableObject, IDisposable
    {
        // Se crea al primer uso: si no hay TIA instalado, cargar Openness falla y se avisa en vez de no arrancar.
        private TiaOpennessService _tia;

        public Contexto(Ajustes ajustes)
        {
            Ajustes = ajustes;
            _estado = L.T("Abre el proyecto en TIA Portal, elígelo arriba y pulsa Conectar.");
        }

        public Ajustes Ajustes { get; }

        public IDialogos Dialogos { get; set; }

        public ObservableCollection<string> Fallos { get; } = new ObservableCollection<string>();

        /// <summary>Ruta del proyecto conectado, o null.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Conectado), nameof(SinConectar), nameof(NombreProyecto))]
        private string _proyectoConectado;

        [ObservableProperty]
        private string _estado;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EstadoNormal))]
        private bool _estadoError;

        [ObservableProperty]
        private bool _ocupado;

        public bool Conectado => ProyectoConectado != null;
        public bool SinConectar => !Conectado;
        public bool EstadoNormal => !EstadoError;
        public bool HayFallos => Fallos.Count > 0;
        public string NombreProyecto => Conectado ? Path.GetFileNameWithoutExtension(ProyectoConectado) : null;

        /// <summary>Ocupado cambió: las herramientas actualizan sus botones.</summary>
        public event Action OcupadoCambiado;

        partial void OnOcupadoChanged(bool value) => OcupadoCambiado?.Invoke();

        public TiaOpennessService Tia()
        {
            if (!Program.HayTia)
                throw new InvalidOperationException(L.T("No se encuentra TIA Portal V19 con Openness en este equipo."));
            return _tia ?? (_tia = new TiaOpennessService());
        }

        public void MostrarEstado(string texto, bool error = false)
        {
            Estado = texto;
            EstadoError = error;
        }

        public void PonerFallos(IEnumerable<string> fallos)
        {
            Fallos.Clear();
            foreach (var fallo in fallos)
                Fallos.Add(fallo);
            OnPropertyChanged(nameof(HayFallos));
        }

        /// <summary>
        /// Ejecuta un trabajo largo: marca la app como ocupada, muestra el mensaje abajo y,
        /// si algo falla, pone el error en la barra de estado.
        /// </summary>
        public async Task Trabajar(string mensaje, Func<Task> trabajo)
        {
            Ocupado = true;
            MostrarEstado(mensaje);
            PonerFallos(Enumerable.Empty<string>());
            try
            {
                await trabajo();
            }
            catch (Exception ex)
            {
                MostrarEstado(L.T("Error: {0}", ex.Message), error: true);
            }
            finally
            {
                Ocupado = false;
            }
        }

        public void Dispose() => _tia?.Dispose();
    }
}
