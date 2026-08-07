using Microsoft.Maui.Storage;

namespace CCB_Mapas_App.Services
{
	public class PreferenceService
	{
		public const string ModoPais = "Pais";
		public const string ModoLocalizacao = "Localizacao";

		private const string ChaveModoVisualizacao = "ModoVisualizacao";
		private const string ChavePaisSelecionado = "PaisSelecionado";

		public string? GetModoVisualizacao()
		{
			return Preferences.Get(ChaveModoVisualizacao, string.Empty);
		}

		public void SalvarConfiguracaoLocalizacao()
		{
			Preferences.Set(ChaveModoVisualizacao, ModoLocalizacao);
			Preferences.Remove(ChavePaisSelecionado);
		}

		public string? GetPaisSelecionado()
		{
			return Preferences.Get(ChavePaisSelecionado, string.Empty);
		}

		public void SalvarConfiguracaoPais(string codigoPais)
		{
			Preferences.Set(ChavePaisSelecionado, codigoPais);
			Preferences.Set(ChaveModoVisualizacao, ModoPais);
		}

		public bool JaConfigurado()
		{
			var modo = GetModoVisualizacao();

			return modo == ModoLocalizacao ||
				(modo == ModoPais && !string.IsNullOrWhiteSpace(GetPaisSelecionado()));
		}

		public void Limpar()
		{
			Preferences.Remove(ChaveModoVisualizacao);
			Preferences.Remove(ChavePaisSelecionado);
		}
	}
}
