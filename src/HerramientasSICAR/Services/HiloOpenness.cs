using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace HerramientasSICAR.Services
{
    /// <summary>
    /// Ejecuta todas las llamadas a Openness en un único hilo propio, una detrás de otra.
    /// Openness no admite llamadas simultáneas y así la ventana no se bloquea mientras TIA trabaja.
    /// </summary>
    public sealed class HiloOpenness : IDisposable
    {
        private readonly BlockingCollection<Action> _cola = new BlockingCollection<Action>();
        private readonly Thread _hilo;

        public HiloOpenness()
        {
            _hilo = new Thread(Bucle) { IsBackground = true, Name = "Openness" };
            _hilo.SetApartmentState(ApartmentState.STA);
            _hilo.Start();
        }

        public Task<T> Ejecutar<T>(Func<T> trabajo)
        {
            var tarea = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _cola.Add(() =>
            {
                try { tarea.SetResult(trabajo()); }
                catch (Exception ex) { tarea.SetException(ex); }
            });
            return tarea.Task;
        }

        public Task Ejecutar(Action trabajo)
        {
            return Ejecutar(() => { trabajo(); return true; });
        }

        private void Bucle()
        {
            foreach (var trabajo in _cola.GetConsumingEnumerable())
                trabajo();
        }

        public void Dispose()
        {
            _cola.CompleteAdding();
        }
    }
}
