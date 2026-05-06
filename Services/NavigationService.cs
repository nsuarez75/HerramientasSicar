using System;
using System.ComponentModel;

namespace HerramientasSICAR.Services
{
    /// <summary>
    /// Service to handle navigation between different views
    /// </summary>
    public class NavigationService : INotifyPropertyChanged
    {
        private object _currentViewModel;

        public object CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                if (_currentViewModel != value)
                {
                    _currentViewModel = value;
                    OnPropertyChanged(nameof(CurrentViewModel));
                }
            }
        }

        public void NavigateTo(object viewModel)
        {
            CurrentViewModel = viewModel;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
