using Avalonia.Markup.Xaml;
using System;

namespace HerramientasSICAR
{
    /// <summary>
    /// {l:T 'Texto'} en las vistas: el texto traducido al idioma de la interfaz.
    /// Al cambiar de idioma la ventana se vuelve a crear, así que basta con traducir al cargarla.
    /// </summary>
    public sealed class TExtension : MarkupExtension
    {
        private readonly string _texto;

        public TExtension(string texto)
        {
            _texto = texto;
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => L.T(_texto);
    }
}
