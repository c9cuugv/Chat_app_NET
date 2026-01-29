using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatApp.Mobile.Services;
using ChatApp.Mobile.Views;

namespace ChatApp.Mobile.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly AuthService _authService;

    [ObservableProperty]
    string email;

    [ObservableProperty]
    string password;

    [ObservableProperty]
    string errorMessage;

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    async Task LoginAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var (success, message) = await _authService.LoginAsync(Email, Password);
            if (success)
            {
                // Navigate to Main Page (Chat Rooms)
                await Shell.Current.GoToAsync($"//{nameof(ChatRoomsPage)}");
            }
            else
            {
                ErrorMessage = message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
