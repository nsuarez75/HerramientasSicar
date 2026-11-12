using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>
    /// Una herramienta de la lista lateral. Las que usan TIA trabajan sobre el proyecto conectado
    /// en la barra superior y, sin conexión, muestran el aviso y desactivan sus botones.
    /// </summary>
    public abstract class Herramienta : ObservableObject
    {
        protected Herramienta(Contexto contexto)
        {
            Contexto = contexto;
            Contexto.PropertyChanged += AlCambiarContexto;
            Contexto.OcupadoCambiado += ActualizarComandos;
        }

        public Contexto Contexto { get; }

        public abstract string Nombre { get; }
        public abstract string Descripcion { get; }
        public abstract string Icono { get; }

        /// <summary>Necesita estar conectado con un proyecto de TIA.</summary>
        public abstract bool UsaTia { get; }

        /// <summary>Se ve el aviso de «Conecta con un proyecto».</summary>
        public bool FaltaConexion => UsaTia && !Contexto.Conectado;

        /// <summary>Se puede lanzar un trabajo con TIA: conectado y sin otro trabajo en marcha.</summary>
        protected bool PuedeUsarTia => Contexto.Conectado && !Contexto.Ocupado;

        protected bool Libre => !Contexto.Ocupado;

        /// <summary>Progreso que se ve en la barra de estado (vale desde cualquier hilo).</summary>
        protected IProgress<string> ProgresoEnEstado() => new Progress<string>(texto => Contexto.MostrarEstado(texto));

        /// <summary>
        /// El PLC con el que trabajar: si el proyecto conectado (o <paramref name="candidatos"/>) tiene uno, ese; si tiene
        /// varios, se pregunta. Null si no hay ninguno o se cancela.
        /// </summary>
        protected async Task<string> ElegirPlc(string titulo, string aceptar, IReadOnlyList<string> candidatos = null)
        {
            var plcs = candidatos ?? Contexto.Proyecto?.Devices.Where(d => d.Kind == DeviceKind.Plc).Select(d => d.Name).ToList()
                ?? new List<string>();
            if (plcs.Count == 0)
            {
                Contexto.MostrarEstado(L.T("No se encontró ningún PLC con software en el proyecto."), error: true);
                return null;
            }
            if (plcs.Count == 1)
                return plcs[0];

            var elegidos = await Contexto.Dialogos.ElegirVarios(titulo, L.T("¿De qué PLC? Se usa el primero que marques."),
                plcs, plcs.Select((_, i) => i == 0).ToList(), aceptar, L.T("Cancelar"));
            return elegidos == null || elegidos.Count == 0 ? null : plcs[elegidos[0]];
        }

        /// <summary>Vuelve a evaluar si los botones de la herramienta se pueden pulsar.</summary>
        protected abstract void ActualizarComandos();

        private void AlCambiarContexto(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Contexto.ProyectoConectado))
            {
                OnPropertyChanged(nameof(FaltaConexion));
                ActualizarComandos();
            }
        }
    }
}
