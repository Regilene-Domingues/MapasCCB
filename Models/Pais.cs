namespace CCB_Mapas_App.Models
{
	public class Pais
	{
		public string Codigo { get; set; } = string.Empty;
		public string Nome { get; set; } = string.Empty;
		public string Arquivo { get; set; } = string.Empty;
		public string Bandeira { get; set; } = string.Empty;
		public string Continente { get; set; } = string.Empty;
		public bool Ativo { get; set; }
	}
}