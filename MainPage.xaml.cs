using Microsoft.Maui.Storage;
using System.IO;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using CCB_Mapas_App.Services;

namespace CCB_Mapas_App
{
	public partial class MainPage : ContentPage
	{
		private readonly ChurchService _churchService;
		private bool mapLoaded = false;

		public MainPage(ChurchService churchService)
		{
			_churchService = churchService;
			InitializeComponent();

			MapWebView.Navigating += (s, e) =>
			{
             Debug.WriteLine($"MapWebView.Navigating -> {e.Url}");
				if (e.Url != null && e.Url.StartsWith("app://pegarLocalizacao"))
				{
					e.Cancel = true;
					_ = ObterLocalizacaoEEnviarParaMapa();
				}
			};

			MapWebView.Navigated += async (s, e) =>
			{
				if (!mapLoaded)
				{
					mapLoaded = true;
                  await Task.Delay(500);
					await EnviarDadosParaJS();
				}
			};

			_ = CarregarDeRecursoEmbutidoAsync();
		}

		private async Task ObterLocalizacaoEEnviarParaMapa()
		{
			try
			{
				Debug.WriteLine("🔍 Solicitando localização...");
				var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(30));
				var location = await Geolocation.Default.GetLocationAsync(request);
				if (location != null)
				{
					string lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
					string lon = location.Longitude.ToString(CultureInfo.InvariantCulture);
					Debug.WriteLine($"✅ Localização: {lat},{lon}");
					await MapWebView.EvaluateJavaScriptAsync($"centralizarNoUsuario({lat}, {lon})");
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"❌ Erro GPS: {ex.Message}");
			}
		}

		private async Task CarregarDeRecursoEmbutidoAsync()
		{
			try
			{
				Debug.WriteLine("📄 Carregando HTML (map.html) do recurso...");
#if WINDOWS
				Stream? stream = null;
				try
				{
					stream = await FileSystem.OpenAppPackageFileAsync("Resources/Raw/map.html");
				}
				catch (FileNotFoundException)
				{
					stream = await FileSystem.OpenAppPackageFileAsync("map.html");
				}

				using (stream)
				using (var r = new StreamReader(stream))
				{
					var html = await r.ReadToEndAsync();
					MapWebView.Source = new HtmlWebViewSource { Html = html };
				}
				Debug.WriteLine("✅ Usando map.html como HtmlWebViewSource no Windows");
#else
				try
				{
					using var s = await FileSystem.OpenAppPackageFileAsync("map.html");
					using var r = new StreamReader(s);
					var html = await r.ReadToEndAsync();
					MapWebView.Source = new HtmlWebViewSource { Html = html };
				}
				catch
				{} catch (Exception ex) {
					try
					{
						using var s = await FileSystem.OpenAppPackageFileAsync("Resources/Raw/map.html");
						using var r = new StreamReader(s);
						var html = await r.ReadToEndAsync();
						MapWebView.Source = new HtmlWebViewSource { Html = html };
					}
					catch (Exception ex) { Debug.WriteLine($"Falha ao carregar map.html: {ex.Message}"); }
				}
#endif
			}
			catch (Exception ex) { Debug.WriteLine("❌ Erro ao carregar HTML: " + ex.Message); }
		}

		private async void LocationSearchBar_SearchButtonPressed(object sender, EventArgs e)
		{
			await PesquisarLocalidadeAsync();
		}

		private async void LocationSearchBar_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(e.NewTextValue))
			{
				await LimparPesquisaNoMapaAsync();
			}
		}

		private async Task PesquisarLocalidadeAsync()
		{
			var textoPesquisa = LocationSearchBar.Text?.Trim();

			if (string.IsNullOrWhiteSpace(textoPesquisa))
			{
				await LimparPesquisaNoMapaAsync();
				return;
			}

			try
			{
				var localidades = await Geocoding.Default.GetLocationsAsync(textoPesquisa);
				var localidade = localidades?.FirstOrDefault();

				if (localidade == null)
				{
					Debug.WriteLine($"Nenhuma localidade encontrada para: {textoPesquisa}");
					return;
				}

				await EnviarPesquisaParaMapaAsync(localidade.Latitude, localidade.Longitude);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Erro ao pesquisar localidade: {ex.Message}");
			}
		}

		private async Task EnviarPesquisaParaMapaAsync(double latitude, double longitude)
		{
			var lat = latitude.ToString(CultureInfo.InvariantCulture);
			var lon = longitude.ToString(CultureInfo.InvariantCulture);
			await MapWebView.EvaluateJavaScriptAsync($"setSearchLocation({lat}, {lon})");
		}

		private async Task LimparPesquisaNoMapaAsync()
		{
			if (!mapLoaded)
			{
				return;
			}

			await MapWebView.EvaluateJavaScriptAsync("clearSearchLocation()");
		}
		private async Task EnviarDadosParaJS()
		{
			try
			{
				Debug.WriteLine("📦 Preparando dados JSON para enviar ao JavaScript...");
				var churches = await _churchService.GetChurchesAsync();
				if (churches == null || churches.Count == 0) { Debug.WriteLine("⚠️ Nenhuma igreja retornada pelo ChurchService"); return; }
				var json = System.Text.Json.JsonSerializer.Serialize(churches);
				var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
				var script = $"(function(){{ try {{ if(typeof receiveDataFromMaui === 'function') {{ receiveDataFromMaui('{base64}'); return 'ok_receive'; }} if(typeof loadChurchesBase64 === 'function') {{ loadChurchesBase64('{base64}'); return 'ok_load'; }} return 'nofunc'; }} catch(e) {{ return 'err:' + e.message; }} }})();";
				var res = await MapWebView.EvaluateJavaScriptAsync(script);
				Debug.WriteLine("JS send result: " + (res ?? "(null)"));
			}
			catch (Exception ex) { Debug.WriteLine($"❌ Erro ao enviar dados: {ex.Message}"); }
		}
	}
}
