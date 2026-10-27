using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

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

        /// <summary>En la lista lateral se puede seleccionar (los rótulos de grupo no).</summary>
        public bool EsSeleccionable => true;

        /// <summary>Necesita estar conectado con un proyecto de TIA.</summary>
        public abstract bool UsaTia { get; }

        /// <summary>Se ve el aviso de «Conecta con un proyecto».</summary>
        public bool FaltaConexion => UsaTia && !Contexto.Conectado;

        /// <summary>Se puede lanzar un trabajo con TIA: conectado y sin otro trabajo en marcha.</summary>
        protected bool PuedeUsarTia => Contexto.Conectado && !Contexto.Ocupado;

        protected bool Libre => !Contexto.Ocupado;

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
