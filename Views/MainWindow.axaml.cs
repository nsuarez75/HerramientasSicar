using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HerramientasSICAR.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HerramientasSICAR.Views
{
    /// <summary>Ventana principal. También hace de IDialogos: los diálogos se abren sobre ella.</summary>
    public partial class MainWindow : Window, IDialogos
    {
        // Por debajo de este ancho los botones de la barra superior se quedan solo con el icono.
        private const double AnchoEstrecho = 1100;

        public MainWindow()
        {
            InitializeComponent();
            SizeChanged += (_, e) => PonerEstrecha(e.NewSize.Width < AnchoEstrecho);
        }

        public void Enlazar(MainViewModel principal)
        {
            principal.Dialogos = this;
            DataContext = principal;
        }

        // Ventana estrecha: botones de la barra solo con icono y lateral más estrecho. El lateral solo cambia
        // al cruzar el umbral.
        private void PonerEstrecha(bool estrecha)
        {
            if (Classes.Contains("narrow") == estrecha)
                return;
            Classes.Set("narrow", estrecha);
            Cuerpo.ColumnDefinitions[0].Width = new GridLength(estrecha ? 190 : 230);
        }

        // ---------------------------------------------------------------- IDialogos

        public Task Mensaje(string titulo, string texto) => DialogWindow.Mensaje(this, titulo, texto);

        public Task AcercaDe(string titulo, string texto, string enlace) => DialogWindow.AcercaDe(this, titulo, texto, enlace);

        public Task<bool> Confirmar(string titulo, string texto, string aceptar) => DialogWindow.Confirmar(this, titulo, texto, aceptar);

        public async Task<string> ElegirCarpeta(string titulo, string inicial)
        {
            var carpetas = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = titulo,
                AllowMultiple = false,
                SuggestedStartLocation = await Inicio(inicial),
            });
            return carpetas.Count == 0 ? null : carpetas[0].TryGetLocalPath();
        }

        public async Task<string> ElegirArchivo(string titulo, string inicial, params TipoArchivo[] tipos) =>
            (await Abrir(titulo, inicial, tipos, multiple: false)).FirstOrDefault();

        public Task<IReadOnlyList<string>> ElegirArchivos(string titulo, string inicial, params TipoArchivo[] tipos) =>
            Abrir(titulo, inicial, tipos, multiple: true);

        public async Task<string> GuardarArchivo(string titulo, string sugerido, TipoArchivo tipo)
        {
            var archivo = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = titulo,
                SuggestedFileName = sugerido == null ? null : Path.GetFileName(sugerido),
                SuggestedStartLocation = await Inicio(sugerido == null ? null : Path.GetDirectoryName(sugerido)),
                DefaultExtension = tipo.Patrones[0].TrimStart('*', '.'),
                ShowOverwritePrompt = true,
                FileTypeChoices = new[] { Filtro(tipo) },
            });
            return archivo?.TryGetLocalPath();
        }

        private async Task<IReadOnlyList<string>> Abrir(string titulo, string inicial, TipoArchivo[] tipos, bool multiple)
        {
            var archivos = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = titulo,
                AllowMultiple = multiple,
                SuggestedStartLocation = await Inicio(inicial),
                FileTypeFilter = tipos.Select(Filtro).ToList(),
            });
            return archivos.Select(a => a.TryGetLocalPath()).Where(r => r != null).ToList();
        }

        private static FilePickerFileType Filtro(TipoArchivo tipo) => new FilePickerFileType(tipo.Nombre) { Patterns = tipo.Patrones.ToList() };

        private async Task<IStorageFolder> Inicio(string carpeta) =>
            string.IsNullOrEmpty(carpeta) || !Directory.Exists(carpeta) ? null : await StorageProvider.TryGetFolderFromPathAsync(carpeta);
    }
}
