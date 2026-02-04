using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm;
using ChatApp.Mobile.Services;
using ChatApp.Mobile.ViewModels;
using ChatApp.Mobile.Views;

namespace ChatApp.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				// fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

        // Services
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<ChatService>();

        // ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<ChatRoomsViewModel>();
        builder.Services.AddTransient<ChatViewModel>();

        // Views
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ChatRoomsPage>();
        builder.Services.AddTransient<ChatPage>();

		return builder.Build();
	}
}
