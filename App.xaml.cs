using CCB_Mapas_App.Services;

namespace CCB_Mapas_App;

public partial class App : Application
{
	public App(MainPage mainPage)
	{
		InitializeComponent();

		MainPage = mainPage;
	}
}