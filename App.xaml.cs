using CCB_Mapas_App.Services;
using Microsoft.Maui;

namespace CCB_Mapas_App;

public partial class App : Application
{
	public App(MainPage mainPage)
	{
		InitializeComponent();

		MainPage = mainPage;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = base.CreateWindow(activationState);

		window.Resumed += (s, e) =>
		{
			if (MainPage is MainPage mainPage)
			{
				mainPage.ForcarAtualizacao();
			}
		};

		return window;
	}
}