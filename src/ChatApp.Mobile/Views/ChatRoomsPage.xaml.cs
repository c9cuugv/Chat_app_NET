using ChatApp.Mobile.ViewModels;

namespace ChatApp.Mobile.Views;

public partial class ChatRoomsPage : ContentPage
{
    private readonly ChatRoomsViewModel _viewModel;

	public ChatRoomsPage(ChatRoomsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.GetRoomsCommand.Execute(null);
    }
}
