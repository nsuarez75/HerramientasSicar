using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Services;

namespace HerramientasSICAR.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly NavigationService _navigationService;
        private readonly DiagExpectedViewModel _diagExpectedViewModel;
        private readonly NumeradorViewModel _numeradorViewModel;
        private readonly ComentadorViewModel _comentadorViewModel;
        private readonly RenombrarArrayViewModel _renombrarArrayViewModel;

        [ObservableProperty]
        private object _currentView;

        [ObservableProperty]
        private string _title = "Herramientas SICAR";

        public MainViewModel(
            NavigationService navigationService,
            DiagExpectedViewModel diagExpectedViewModel,
            NumeradorViewModel numeradorViewModel,
            ComentadorViewModel comentadorViewModel,
            RenombrarArrayViewModel renomrarArrayViewModel)
        {
            _navigationService = navigationService;
            _diagExpectedViewModel = diagExpectedViewModel;
            _numeradorViewModel = numeradorViewModel;
            _comentadorViewModel = comentadorViewModel;
            _renombrarArrayViewModel = renomrarArrayViewModel;

            // Subscribe to navigation changes
            _navigationService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(NavigationService.CurrentViewModel))
                {
                    CurrentView = _navigationService.CurrentViewModel;
                }
            };

            // Navigate to DiagExpected by default
            NavigateToDiagExpectedCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToDiagExpected()
        {
            _navigationService.NavigateTo(_diagExpectedViewModel);
        }

        [RelayCommand]
        private void NavigateToNumerador()
        {
            _navigationService.NavigateTo(_numeradorViewModel);
        }

        [RelayCommand]
        private void NavigateToComentador()
        {
            _navigationService.NavigateTo(_comentadorViewModel);
        }

        [RelayCommand]
        private void NavigateToRenombrarArray()
        {
            _navigationService.NavigateTo(_renombrarArrayViewModel);
        }
    }
}
