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
    public partial class MainWindow : Window, IDialogs
    {
        // Por debajo de este ancho los botones de la barra superior se quedan solo con el icono.
        private const double NarrowWidth = 1100;

        public MainWindow()
        {
            InitializeComponent();
            SizeChanged += (_, e) => SetNarrow(e.NewSize.Width < NarrowWidth);
        }

        public void Bind(MainViewModel main)
        {
            main.Dialogs = this;
            DataContext = main;
        }

        // Ventana estrecha: botones de la barra solo con icono y lateral más estrecho. El lateral solo cambia
        // al cruzar el umbral.
        private void SetNarrow(bool narrow)
        {
            if (Classes.Contains("narrow") == narrow)
                return;
            Classes.Set("narrow", narrow);
            Body.ColumnDefinitions[0].Width = new GridLength(narrow ? 190 : 230);
        }

        // ---------------------------------------------------------------- IDialogos

        public Task MessageNetwork(string title, string text) => DialogWindow.MessageNetwork(this, title, text);

        public Task About(string title, string text, string link) => DialogWindow.About(this, title, text, link);

        public Task<bool> Confirm(string title, string text, string accept) => DialogWindow.Confirm(this, title, text, accept);

        public Task<IReadOnlyList<int>> ChooseMany(string title, string text, IReadOnlyList<string> options, IReadOnlyList<bool> checkedIndexes,
            string accept, string cancel) => DialogWindow.ChooseMany(this, title, text, options, checkedIndexes, accept, cancel);

        public async Task<string> ChooseFolder(string title, string initial)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                SuggestedStartLocation = await Start(initial),
            });
            return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
        }

        public async Task<string> ChooseFile(string title, string initial, params FileKind[] types) =>
            (await Open(title, initial, types, multiple: false)).FirstOrDefault();

        public Task<IReadOnlyList<string>> ChooseFiles(string title, string initial, params FileKind[] types) =>
            Open(title, initial, types, multiple: true);

        public async Task<string> SaveFile(string title, string suggested, FileKind kind)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggested == null ? null : Path.GetFileName(suggested),
                SuggestedStartLocation = await Start(suggested == null ? null : Path.GetDirectoryName(suggested)),
                DefaultExtension = kind.Patterns[0].TrimStart('*', '.'),
                ShowOverwritePrompt = true,
                FileTypeChoices = new[] { Filter(kind) },
            });
            return file?.TryGetLocalPath();
        }

        private async Task<IReadOnlyList<string>> Open(string title, string initial, FileKind[] types, bool multiple)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = multiple,
                SuggestedStartLocation = await Start(initial),
                FileTypeFilter = types.Select(Filter).ToList(),
            });
            return files.Select(a => a.TryGetLocalPath()).Where(r => r != null).ToList();
        }

        private static FilePickerFileType Filter(FileKind kind) => new FilePickerFileType(kind.Name) { Patterns = kind.Patterns.ToList() };

        private async Task<IStorageFolder> Start(string folder) =>
            string.IsNullOrEmpty(folder) || !Directory.Exists(folder) ? null : await StorageProvider.TryGetFolderFromPathAsync(folder);
    }
}
