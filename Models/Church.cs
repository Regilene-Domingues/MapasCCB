using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CCB_Mapas_App.Models
{
	public class Church
	{
		public string? CodigoId { get; set; }
		public string? Nome { get; set; }
		public string? Logradouro { get; set; }
		public string? CEP { get; set; }
		public string? Cidade { get; set; }
		public Coordenadas? Coordenadas { get; set; }
		public Agenda? Agenda { get; set; }
	}

	public class Coordenadas
	{
		[JsonPropertyName("lat")]
		public double? Lat { get; set; }

		[JsonPropertyName("lon")]
		public double? Lon { get; set; }
	}

	public class Agenda
	{
		public List<string>? DiasDeCulto { get; set; }
		public Dictionary<string, string>? Horarios { get; set; }
		public string? Obs { get; set; }
	}
}
