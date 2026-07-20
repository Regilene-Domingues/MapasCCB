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
		private static readonly HttpClient _httpClient = new HttpClient { DefaultRequestHeaders = { { "User-Agent", "CCBMapasApp/1.0" } } };

		public MainPage(ChurchService churchService)
		{
			_churchService = churchService;
			InitializeComponent();

			MapWebView.Navigating += (s, e) =>
			{
				if (e.Url != null)
				{
					if (e.Url.StartsWith("https://app.local/pegarLocalizacao"))
					{
						e.Cancel = true;
						_ = ObterLocalizacaoEEnviarParaMapa();
					}
					else if (e.Url.StartsWith("http://") || e.Url.StartsWith("https://") || e.Url.StartsWith("google.navigation:") || e.Url.StartsWith("waze://"))
					{
						e.Cancel = true;
						
						string url = e.Url;

						// Ajuste para busca, não rota
						if (url.StartsWith("google.navigation:"))
						{
							url = url.Replace("google.navigation:q=", "https://www.google.com/maps/search/?api=1&query=");
						}

						try
						{
							_ = Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(new Uri(url));
						}
						catch (Exception)
						{
                            if (url.StartsWith("waze://"))
                            {
                                var latLonMatch = System.Text.RegularExpressions.Regex.Match(url, @"ll=([^&]+)");
                                if (latLonMatch.Success)
                                {
                                    string latLon = latLonMatch.Groups[1].Value;
                                    string fallbackUrl = $"https://www.google.com/maps/search/?api=1&query={latLon}";
                                    _ = Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(new Uri(fallbackUrl));
                                }
                            }
						}
					}
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
			var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
			if (status != PermissionStatus.Granted)
			{
				return;
			}

			try
			{
				var cachedLocation = await Geolocation.Default.GetLastKnownLocationAsync();
				
				if (cachedLocation != null)
				{
					string lat = cachedLocation.Latitude.ToString(CultureInfo.InvariantCulture);
					string lon = cachedLocation.Longitude.ToString(CultureInfo.InvariantCulture);
					await MapWebView.EvaluateJavaScriptAsync($"centralizarNoUsuario({lat}, {lon})");
				}

				var request = new GeolocationRequest(GeolocationAccuracy.Default, TimeSpan.FromSeconds(30));
				var location = await Geolocation.Default.GetLocationAsync(request);
				
				if (location != null)
				{
					string lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
					string lon = location.Longitude.ToString(CultureInfo.InvariantCulture);
					await MapWebView.EvaluateJavaScriptAsync($"centralizarNoUsuario({lat}, {lon})");
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"❌ Erro inicialização localização: {ex.Message}");
			}
		}

		private async Task< (double lat, double lon) > ObterLocalizacaoInicialAsync()
		{
			double lat = 39.5;
			double lon = -8.0;

			try {
				var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
				if (status == PermissionStatus.Granted)
				{
					Debug.WriteLine("📍 Tentando obter localização (cache)...");
					var location = await Geolocation.Default.GetLastKnownLocationAsync();
					
					if (location == null) {
						Debug.WriteLine("📍 Cache nulo. Tentando GPS (timeout 15s)...");
						location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(15)));
					}
					
					if (location != null)
					{
						Debug.WriteLine($"✅ Localização obtida: {location.Latitude}, {location.Longitude}");
						lat = location.Latitude;
						lon = location.Longitude;
					} else {
						Debug.WriteLine("⚠️ Nenhuma localização obtida.");
					}
				} else {
					Debug.WriteLine("⚠️ Permissão de localização negada.");
				}
			} catch (Exception ex) {
				Debug.WriteLine($"❌ Erro ao obter localização inicial: {ex.Message}");
			}
			return (lat, lon);
		}

		private async Task CarregarDeRecursoEmbutidoAsync()
		{
			try
			{
				Debug.WriteLine("📄 Carregando HTML (map.html) e assets do FileSystem...");
				
				// 1. Obtém localização inicial (sem bloquear UI)
				var coords = await ObterLocalizacaoInicialAsync();
				Debug.WriteLine($"📄 Debug Injeção: Lat={coords.lat}, Lon={coords.lon}, Padrão={coords.lat == 39.5 && coords.lon == -8.0}");
				
				using var htmlStream = await FileSystem.OpenAppPackageFileAsync("Resources/Raw/map.html");
				using var cssStream = await FileSystem.OpenAppPackageFileAsync("Resources/Raw/leaflet.css");
				using var jsStream = await FileSystem.OpenAppPackageFileAsync("Resources/Raw/leaflet.js");
				
				using var htmlReader = new StreamReader(htmlStream);
				using var cssReader = new StreamReader(cssStream);
				using var jsReader = new StreamReader(jsStream);
				
				var html = await htmlReader.ReadToEndAsync();
				var css = await cssReader.ReadToEndAsync();
				var js = await jsReader.ReadToEndAsync();
				
				// 2. Injeta os assets e a localização inicial no HTML
				html = html.Replace("<link rel=\"stylesheet\" href=\"https://unpkg.com/leaflet@1.9.4/dist/leaflet.css\" />", $"<style>{css}</style>");
				html = html.Replace("<script src=\"https://unpkg.com/leaflet@1.9.4/dist/leaflet.js\"></script>", $"<script>{js}</script>");
				
				// Injeta variáveis de configuração para o initMap ler
				var jsConfig = $@"
					<script>
						var initialLat = {coords.lat.ToString(CultureInfo.InvariantCulture)};
						var initialLon = {coords.lon.ToString(CultureInfo.InvariantCulture)};
						var hasUserLocation = {(coords.lat != 39.5 || coords.lon != -8.0 ? "true" : "false")};
						var isWindows = {(DeviceInfo.Platform == DevicePlatform.WinUI ? "true" : "false")};
					</script>";
				
				html = html.Replace("</head>", jsConfig + "</head>");
				
				MapWebView.Source = new HtmlWebViewSource { Html = html };
				Debug.WriteLine($"✅ Mapa inicializado em: {coords.lat}, {coords.lon}");
			}
			catch (Exception ex) { Debug.WriteLine("❌ Erro ao carregar recursos via FileSystem: " + ex.Message); }
		}
		
		private async void LocationButton_Clicked(object sender, EventArgs e)
		{
			await ObterLocalizacaoEEnviarParaMapa();
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
				var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(textoPesquisa)}&format=json&limit=1";
				var json = await _httpClient.GetStringAsync(url);
				var resultados = System.Text.Json.JsonSerializer.Deserialize<List<NominatimResult>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
				var localidade = resultados?.FirstOrDefault();

				if (localidade == null || !double.TryParse(localidade.Lat, CultureInfo.InvariantCulture, out var lat) || !double.TryParse(localidade.Lon, CultureInfo.InvariantCulture, out var lon))
				{
					Debug.WriteLine($"Nenhuma localidade encontrada para: {textoPesquisa}");
					return;
				}

				await EnviarPesquisaParaMapaAsync(lat, lon);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Erro ao pesquisar localidade: {ex.Message}");
			}
		}

		private class NominatimResult
		{
			public string? Lat { get; set; }
			public string? Lon { get; set; }
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
