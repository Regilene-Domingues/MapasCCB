using System.Text.Json;
using CCB_Mapas_App.Models;

namespace CCB_Mapas_App.Services
{
	public class ChurchService
	{
		public async Task<List<Church>> GetChurchesAsync()
		{
			try
			{
				// Abre o fluxo de leitura do arquivo embutido no Resources/Raw
				Stream? stream = null;
				try
				{
					stream = await FileSystem.OpenAppPackageFileAsync("Resources/Raw/igrejas.json");
				}
				catch (FileNotFoundException)
				{
					stream = await FileSystem.OpenAppPackageFileAsync("igrejas.json");
				}

				using var selectedStream = stream;
				using var reader = new StreamReader(selectedStream);
				var jsonContents = await reader.ReadToEndAsync();

				// Converte o texto JSON na nossa lista de objetos baseada no Model
				var churches = JsonSerializer.Deserialize<List<Church>>(jsonContents, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				return churches ?? new List<Church>();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Erro ao ler JSON: {ex.Message}");
				return new List<Church>();
			}
		}
	}
}
