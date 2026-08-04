using System.Text.Json;
using CCB_Mapas_App.Models;

namespace CCB_Mapas_App.Services
{
	public class PaisService
	{
		public async Task<List<Pais>> GetPaisesAsync()
		{
			try
			{
				// Abre o fluxo de leitura do arquivo embutido no Resources/Raw
				Stream? stream = null;
				try
				{
					stream = await FileSystem.OpenAppPackageFileAsync("Resources/Raw/Paises.json");
				}
				catch (FileNotFoundException)
				{
					stream = await FileSystem.OpenAppPackageFileAsync("Paises.json");
				}

				using var selectedStream = stream;
				using var reader = new StreamReader(selectedStream);
				var jsonContents = await reader.ReadToEndAsync();

				var paises = JsonSerializer.Deserialize<List<Pais>>(jsonContents, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				return paises ?? new List<Pais>();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Erro ao ler Paises.json: {ex.Message}");
				return new List<Pais>();
			}
		}
	}
}