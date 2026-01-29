using ChatApp.Mobile.Views;

namespace ChatApp.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

        Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));
	}
}
