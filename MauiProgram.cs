using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using CCB_Mapas_App.Services; // Adiciona o mapeamento para encontrar o ChurchService

namespace CCB_Mapas_App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// ======= REGISTRO DE DEPENDÊNCIAS (Adicione estas linhas) =======
		builder.Services.AddSingleton<ChurchService>();
		builder.Services.AddTransient<MainPage>();
		// ================================================================

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}