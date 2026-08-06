using Microsoft.Maui.Storage;

namespace CCB_Mapas_App.Services
{
	public class PreferenceService
	{
		private const string ChaveModoVisualizacao = "ModoVisualizacao";
		private const string ChavePaisSelecionado = "PaisSelecionado";

		public string? GetModoVisualizacao()
		{
			return Preferences.Get(ChaveModoVisualizacao, string.Empty);
		}

		public void SetModoVisualizacao(string modo)
		{
			Preferences.Set(ChaveModoVisualizacao, modo);
		}

		public string? GetPaisSelecionado()
		{
			return Preferences.Get(ChavePaisSelecionado, string.Empty);
		}

		public void SetPaisSelecionado(string codigoPais)
		{
			Preferences.Set(ChavePaisSelecionado, codigoPais);
		}

		public bool JaConfigurado()
		{
			return !string.IsNullOrWhiteSpace(GetModoVisualizacao());
		}

		public void Limpar()
		{
			Preferences.Remove(ChaveModoVisualizacao);
			Preferences.Remove(ChavePaisSelecionado);
		}
	}
}