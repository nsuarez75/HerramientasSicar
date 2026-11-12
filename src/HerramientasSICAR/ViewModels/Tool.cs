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
    public abstract class Tool : ObservableObject
    {
        protected Tool(ToolContext context)
        {
            ToolContext = context;
            ToolContext.PropertyChanged += OnContextChanged;
            ToolContext.BusyChanged += UpdateCommands;
        }

        public ToolContext ToolContext { get; }

        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract string Icon { get; }

        /// <summary>Necesita estar conectado con un proyecto de TIA.</summary>
        public abstract bool UsesTia { get; }

        /// <summary>Se ve el aviso de «Conecta con un proyecto».</summary>
        public bool NeedsConnection => UsesTia && !ToolContext.Connected;

        /// <summary>Se puede lanzar un trabajo con TIA: conectado y sin otro trabajo en marcha.</summary>
        protected bool CanUseTia => ToolContext.Connected && !ToolContext.Busy;

        protected bool Idle => !ToolContext.Busy;

        /// <summary>Progreso que se ve en la barra de estado (vale desde cualquier hilo).</summary>
        protected IProgress<string> StatusProgress() => new Progress<string>(text => ToolContext.ShowStatus(text));

        /// <summary>
        /// El PLC con el que trabajar: si el proyecto conectado (o <paramref name="candidatos"/>) tiene uno, ese; si tiene
        /// varios, se pregunta. Null si no hay ninguno o se cancela.
        /// </summary>
        protected async Task<string> ChoosePlc(string title, string accept, IReadOnlyList<string> candidates = null)
        {
            var plcs = candidates ?? ToolContext.ProjectFile?.Devices.Where(d => d.Kind == DeviceKind.Plc).Select(d => d.Name).ToList()
                ?? new List<string>();
            if (plcs.Count == 0)
            {
                ToolContext.ShowStatus(L.T("No se encontró ningún PLC con software en el proyecto."), error: true);
                return null;
            }
            if (plcs.Count == 1)
                return plcs[0];

            var chosen = await ToolContext.Dialogs.ChooseMany(title, L.T("¿De qué PLC? Se usa el primero que marques."),
                plcs, plcs.Select((_, i) => i == 0).ToList(), accept, L.T("Cancelar"));
            return chosen == null || chosen.Count == 0 ? null : plcs[chosen[0]];
        }

        /// <summary>Vuelve a evaluar si los botones de la herramienta se pueden pulsar.</summary>
        protected abstract void UpdateCommands();

        private void OnContextChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ToolContext.ConnectedProject))
            {
                OnPropertyChanged(nameof(NeedsConnection));
                UpdateCommands();
            }
        }
    }
}
