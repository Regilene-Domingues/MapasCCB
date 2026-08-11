using Microsoft.Maui.Storage;
using System.IO;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using CCB_Mapas_App.Services;
using CCB_Mapas_App.Models;

namespace CCB_Mapas_App
{
	public partial class MainPage : ContentPage
	{
		private readonly ChurchService _churchService;
		private readonly PaisService _paisService;
		private readonly PreferenceService _preferenceService;
		private bool mapLoaded = false;
		private bool aplicativoInicializado;
		private static readonly HttpClient _httpClient = new HttpClient { DefaultRequestHeaders = { { "User-Agent", "CCBMapasApp/1.0" } } };
		private List<Pais> _paises = new();
		private Pais? PaisAtual;

		public MainPage(ChurchService churchService, PaisService paisService, PreferenceService preferenceService)
		{
			_churchService = churchService;
			_paisService = paisService;
			_preferenceService = preferenceService;

			InitializeComponent();

			MapWebView.Navigating += (s, e) =>
			{
				if (e.Url == null) return;

				// Priorizar sinais internos (app.local)
				if (e.Url.StartsWith("https://app.local/webviewReady", StringComparison.OrdinalIgnoreCase))
				{
					e.Cancel = true;
					if (!mapLoaded)
					{
						mapLoaded = true;
						Debug.WriteLine("✅ WebView e mapa prontos.");
						_ = EnviarDadosParaJS();
					}
					return;
				}

				if (e.Url.StartsWith("https://app.local/churchesLoaded", StringComparison.OrdinalIgnoreCase))
				{
					e.Cancel = true;
					try
					{
						var uri = new Uri(e.Url);
						int count = -1;
						var query = uri.Query.TrimStart('?');
						foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
						{
							var nv = part.Split('=');
							if (nv.Length >= 2)
							{
								var key = Uri.UnescapeDataString(nv[0]);
								var val = Uri.UnescapeDataString(nv[1]);
								if (key == "count" && int.TryParse(val, out var v)) count = v;
							}
						}

						Dispatcher.Dispatch(async () =>
						{
							HideLoading();
							Debug.WriteLine($"✅ JS reported churches loaded: {count}");
							if (count == 0)
							{
								await DisplayAlert("Nenhuma congregação", "Nenhuma congregação encontrada para o país selecionado.", "OK");
							}
						});
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"❌ Erro ao processar churchesLoaded: {ex.Message}");
						HideLoading();
					}
					return;
				}

				if (e.Url.StartsWith("https://app.local/pegarLocalizacao", StringComparison.OrdinalIgnoreCase))
				{
					e.Cancel = true;
					_ = ObterLocalizacaoEEnviarParaMapa();
					return;
				}

				if (e.Url.StartsWith("https://app.local/rota", StringComparison.OrdinalIgnoreCase))
				{
					e.Cancel = true;
					_ = TratarRota(e.Url);
					return;
				}

				// Links externos: ignore app.local
				if ((e.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || e.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
					!e.Url.StartsWith("https://app.local", StringComparison.OrdinalIgnoreCase) || e.Url.StartsWith("google.navigation:") || e.Url.StartsWith("waze://"))
				{
					e.Cancel = true;
					string url = e.Url;

					// Ajuste para busca, não rota
					if (url.StartsWith("google.navigation:", StringComparison.OrdinalIgnoreCase))
					{
						url = url.Replace("google.navigation:q=", "https://www.google.com/maps/search/?api=1&query=");
					}

					try
					{
						_ = Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(new Uri(url));
					}
					catch (Exception)
					{
						if (url.StartsWith("waze://", StringComparison.OrdinalIgnoreCase))
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
			};

			_ = CarregarDeRecursoEmbutidoAsync();
		}

		protected override void OnAppearing()
		{
			base.OnAppearing();

			if (!aplicativoInicializado)
			{
				aplicativoInicializado = true;
				_ = InicializarAplicativoAsync();
			}

			_ = ExecutarJavaScriptQuandoMapaProntoAsync("if (typeof atualizarCoresPinos === 'function') { atualizarCoresPinos(); }");
		}

		public void ForcarAtualizacao()
		{
			Dispatcher.Dispatch(async () =>
			{
				Debug.WriteLine("🔄 Forçando atualização dos pinos...");
				await ExecutarJavaScriptQuandoMapaProntoAsync("if (typeof atualizarCoresPinos === 'function') { atualizarCoresPinos(); }");
			});
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
					await ExecutarJavaScriptQuandoMapaProntoAsync($"centralizarNoUsuario({lat}, {lon})");
				}

				var request = new GeolocationRequest(GeolocationAccuracy.Default, TimeSpan.FromSeconds(30));
				var location = await Geolocation.Default.GetLocationAsync(request);
				
				if (location != null)
				{
					string lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
					string lon = location.Longitude.ToString(CultureInfo.InvariantCulture);
					await ExecutarJavaScriptQuandoMapaProntoAsync($"centralizarNoUsuario({lat}, {lon})");
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
			await ExecutarJavaScriptQuandoMapaProntoAsync($"setSearchLocation({lat}, {lon})");
		}

		private async Task LimparPesquisaNoMapaAsync()
		{
			await ExecutarJavaScriptQuandoMapaProntoAsync("clearSearchLocation()");
		}

		private async Task InicializarAplicativoAsync()
		{
            // Carrega países sempre que inicializamos
			await CarregarPaisesAsync();			

			// NOVA LÓGICA: sempre mostrar o menu inicial em cada abertura do aplicativo.
			// Não restauramos automaticamente a visualização anterior aqui.
			Debug.WriteLine("📋 Exibindo menu inicial (execução padrão a cada abertura).");
			await MostrarEscolhaInicialAsync();
			return;
		}

		// Método público para que o App (lifecycle) possa solicitar a exibição do menu ao retornar do background.
		public async Task MostrarEscolhaInicialPublicaAsync()
		{
			try
			{
				// Garantir que os países estejam carregados antes de exibir o menu
				if (_paises == null || _paises.Count == 0)
				{
					await CarregarPaisesAsync();
				}
				await MostrarEscolhaInicialAsync();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"❌ Erro ao requisitar menu inicial: {ex.Message}");
			}
		}

		private async Task MostrarEscolhaInicialAsync()
		{
			Debug.WriteLine("📋 Entrou em MostrarEscolhaInicialAsync");

			var opcao = await DisplayActionSheet(
			   "Visualizar Congregações",
			   "Cancelar",
			   null,
			   "📍 Minha localização",
			   "🌍 Escolher um país");

			Debug.WriteLine($"Opção escolhida: {opcao}");

			if (opcao == "📍 Minha localização")
			{
             // Mantém o comportamento de salvar a escolha do usuário
				_preferenceService.SalvarConfiguracaoLocalizacao();
				// Executa rotina segura: centraliza no usuário, tenta detectar país e pede confirmação antes de trocar
				await SelecionarPorLocalizacaoAsync();
			}
			else if (opcao == "🌍 Escolher um país")
			{
				await MostrarSeletorDePaisesAsync();
			}
		}

		private async Task SelecionarPorLocalizacaoAsync()
		{
			try
			{
          // Solicita permissão uma vez; se negada, oferece abrir configurações ou voltar ao menu
			var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
			if (status != PermissionStatus.Granted)
			{
				var escolha = await DisplayActionSheet("Permissão necessária", "Voltar ao menu", null, "Abrir configurações");
				if (escolha == "Abrir configurações")
				{
					try
					{
						Microsoft.Maui.ApplicationModel.AppInfo.ShowSettingsUI();
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"❌ Erro ao abrir configurações: {ex.Message}");
					}
					// Volta ao menu para o usuário escolher novamente após abrir configurações
					await MostrarEscolhaInicialAsync();
					return;
				}
				else
				{
					// Voltar ao menu inicial para o usuário escolher outra opção
					await MostrarEscolhaInicialAsync();
					return;
				}
			}

				Location? location = null;
				location = await Geolocation.Default.GetLastKnownLocationAsync();

				if (location == null)
				{
					location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(15)));
				}

				if (location == null)
				{
					await DisplayAlert("Localização", "Não foi possível obter sua localização.", "OK");
					return;
				}

				double lat = location.Latitude;
				double lon = location.Longitude;

				// Centraliza e desenha o marcador no mapa (reaproveita JS existente)
				await ExecutarJavaScriptQuandoMapaProntoAsync($"centralizarNoUsuario({lat.ToString(CultureInfo.InvariantCulture)}, {lon.ToString(CultureInfo.InvariantCulture)})");

				try
				{
					var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lon.ToString(CultureInfo.InvariantCulture)}&zoom=3&addressdetails=1";
					var json = await _httpClient.GetStringAsync(url);
					using var doc = System.Text.Json.JsonDocument.Parse(json);
					if (doc.RootElement.TryGetProperty("address", out var address))
					{
						if (address.TryGetProperty("country_code", out var countryCodeProp))
						{
							var countryCode = countryCodeProp.GetString();
							if (!string.IsNullOrWhiteSpace(countryCode))
							{
								countryCode = countryCode.ToUpperInvariant();
								var pais = _paises.FirstOrDefault(p => p.Ativo && string.Equals(p.Codigo, countryCode, StringComparison.OrdinalIgnoreCase));
								if (pais == null)
								{
									await DisplayAlert("País não suportado", "Nenhum dado disponível para o país detectado.", "OK");
									return;
								}

								var confirmar = await DisplayAlert("País detectado", $"Detectamos {pais.Bandeira} {pais.Nome}. Deseja carregar as congregações deste país?", "Sim", "Não");
								if (confirmar)
								{
									await TrocarPaisAsync(pais);
								}
								return;
							}
						}
					}
					await DisplayAlert("País não detectado", "Não foi possível identificar o país a partir da sua localização.", "OK");
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"❌ Erro no reverse geocoding: {ex.Message}");
					await DisplayAlert("Erro", "Não foi possível identificar o país. Tente novamente mais tarde.", "OK");
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"❌ Erro ao selecionar por localização: {ex.Message}");
				await DisplayAlert("Erro", "Ocorreu um erro ao obter a localização.", "OK");
			}
		}

		private async Task CarregarPaisesAsync()
		{
			try
			{
				_paises = await _paisService.GetPaisesAsync();

				Debug.WriteLine($"🌍 Países carregados: {_paises.Count}");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"❌ Erro ao carregar países: {ex.Message}");
			}
		}

		private async Task TrocarPaisAsync(Pais pais)
		{
           PaisAtual = pais;

			_preferenceService.SalvarConfiguracaoPais(PaisAtual.Codigo);

			CountryButton.Text = $"{PaisAtual.Bandeira} {PaisAtual.Nome} ▾";

			// Mostrar feedback visual enquanto carregamos as congregações
			ShowLoading("Carregando congregações...");
			try
			{
				await EnviarDadosParaJS();
			}
			finally
			{
				HideLoading();
			}

			Debug.WriteLine($"🌍 País alterado para: {PaisAtual.Nome}");
		}

		private void ShowLoading(string message)
		{
			try
			{
				Dispatcher.Dispatch(() =>
				{
					LoadingLabel.Text = message;
					LoadingIndicator.IsRunning = true;
					LoadingOverlay.IsVisible = true;
				});
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"❌ Erro ao mostrar overlay de carregamento: {ex.Message}");
			}
		}

		private void HideLoading()
		{
			try
			{
				Dispatcher.Dispatch(() =>
				{
					LoadingIndicator.IsRunning = false;
					LoadingOverlay.IsVisible = false;
				});
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"❌ Erro ao esconder overlay de carregamento: {ex.Message}");
			}
		}

		private async Task MostrarSeletorDePaisesAsync()
		{
			Debug.WriteLine("📋 Exibindo menu inicial...");

			var opcoes = _paises
				.Where(p => p.Ativo)
				.Select(p => $"{p.Bandeira} {p.Nome}")
				.ToArray();

			var opcao = await DisplayActionSheet(
				"Selecionar país",
				"Cancelar",
				null,
				opcoes);
			
			Debug.WriteLine($"Opção escolhida: {opcao}");

			var pais = _paises.FirstOrDefault(p =>
				p.Ativo && $"{p.Bandeira} {p.Nome}" == opcao);

			if (pais != null)
			{
				await TrocarPaisAsync(pais);
			}
		}

		private async void CountryButton_Clicked(object sender, EventArgs e)
		{
			await MostrarSeletorDePaisesAsync();
		}

		private async Task EnviarDadosParaJS()
		{
			try
			{
				if (!mapLoaded)
				{
					Debug.WriteLine("🗺️ Aguardando a WebView ficar pronta para enviar as congregações.");
					return;
				}

				if (PaisAtual == null)
				{
					Debug.WriteLine("🌍 Aguardando a seleção de um país para enviar as congregações.");
					return;
				}
				Debug.WriteLine("📦 Preparando dados JSON para enviar ao JavaScript...");
				var churches = await _churchService.GetChurchesAsync(PaisAtual!.Arquivo);
				if (churches == null || churches.Count == 0) { Debug.WriteLine("⚠️ Nenhuma igreja retornada pelo ChurchService"); return; }
				var json = System.Text.Json.JsonSerializer.Serialize(churches);
				var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
				var script = $"(function(){{ try {{ if(typeof receiveDataFromMaui === 'function') {{ receiveDataFromMaui('{base64}'); return 'ok_receive'; }} if(typeof loadChurchesBase64 === 'function') {{ loadChurchesBase64('{base64}'); return 'ok_load'; }} return 'nofunc'; }} catch(e) {{ return 'err:' + e.message; }} }})();";
                var res = await ExecutarJavaScriptQuandoMapaProntoAsync(script);
				Debug.WriteLine("JS send result: " + (res ?? "(null)"));
			}
			catch (Exception ex) { Debug.WriteLine($"❌ Erro ao enviar dados: {ex.Message}"); }
		}

		private async Task<string?> ExecutarJavaScriptQuandoMapaProntoAsync(string script)
		{
			if (!mapLoaded)
			{
				Debug.WriteLine("🗺️ Aguardando a WebView ficar pronta para executar JavaScript.");
				return null;
			}

			try
			{
				return await MapWebView.EvaluateJavaScriptAsync(script);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"❌ Erro ao executar JavaScript: {ex.Message}");
				return null;
			}
		}

		private async Task TratarRota(string url)
		{
			try
			{
				var uri = new Uri(url);
				string lat = "";
				string lon = "";
				string nome = "";

				var query = uri.Query.TrimStart('?');
				foreach (var part in query.Split('&'))
				{
					var nv = part.Split('=');
					if (nv.Length >= 2)
					{
						string key = Uri.UnescapeDataString(nv[0]);
						string val = Uri.UnescapeDataString(nv[1]);
						if (key == "lat") lat = val;
						else if (key == "lon") lon = val;
						else if (key == "nome") nome = val;
					}
				}

				if (string.IsNullOrEmpty(lat) || string.IsNullOrEmpty(lon))
				{
					return;
				}

				if (DeviceInfo.Platform == DevicePlatform.WinUI)
				{
					string googleMapsUrl = $"https://www.google.com/maps/search/?api=1&query={lat},{lon}";
					await Launcher.Default.OpenAsync(new Uri(googleMapsUrl));
				}
				else if (DeviceInfo.Platform == DevicePlatform.Android)
				{
					string geoUri = $"geo:{lat},{lon}?q={lat},{lon}";
					if (!string.IsNullOrEmpty(nome))
					{
						geoUri += $"({Uri.EscapeDataString(nome)})";
					}
					await Launcher.Default.OpenAsync(new Uri(geoUri));
				}
				else if (DeviceInfo.Platform == DevicePlatform.iOS)
				{
					await MostrarSeletorRotaiOS(lat, lon, nome);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"❌ Erro ao tratar rota: {ex.Message}");
			}
		}

		private async Task MostrarSeletorRotaiOS(string lat, string lon, string nome)
		{
			try
			{
				var escolha = await Shell.Current.DisplayActionSheet(
					"Como Chegar",
					"Cancelar",
					null,
					"Apple Maps",
					"Google Maps",
					"Waze"
				);

				string? url = escolha switch
				{
					"Apple Maps" => $"maps://?q={lat},{lon}",
					"Google Maps" => $"comgooglemaps://?q={lat},{lon}",
					"Waze" => $"waze://?ll={lat},{lon}&navigate=yes",
					_ => null
				};

				if (url != null)
				{
					await Launcher.Default.OpenAsync(new Uri(url));
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"❌ Erro ao exibir seletor iOS: {ex.Message}");
			}
		}
	}
}
