# CCB Mapas App - Instruções e Contexto

Este arquivo é a principal fonte de documentação para agentes de Inteligência Artificial que trabalham neste repositório.

Ele descreve os objetivos do projeto, arquitetura, convenções, problemas conhecidos e diretrizes de desenvolvimento. Antes de realizar qualquer alteração no código, o agente deve ler este documento por completo e considerar seu conteúdo como referência oficial do projeto.

---

# Objetivo do Projeto

O CCB Mapas App é um aplicativo desenvolvido em .NET MAUI cujo objetivo é disponibilizar um mapa interativo contendo as Casas de Oração da Congregação Cristã no Brasil (CCB).

O aplicativo deve proporcionar uma experiência simples, rápida e intuitiva ao usuário, sendo capaz de trabalhar com milhares de igrejas sem perda perceptível de desempenho.

Os principais objetivos da aplicação são:

- Exibir um mapa interativo que permita mover (arrastar), aproximar e afastar o mapa.
- Exibir todas as igrejas cadastradas no arquivo `igrejas.json` através de pinos no mapa.
- Utilizar as coordenadas geográficas armazenadas no arquivo JSON para posicionar corretamente cada igreja.
- Manter todas as informações das igrejas armazenadas localmente, sem depender de um banco de dados online.

## Comportamento dos pinos

Cada igreja é representada por um pino.

As cores dos pinos possuem os seguintes significados:

- Azul: estado padrão.
- Verde: existe culto na igreja no dia atual da semana, conforme informado na agenda existente no arquivo `igrejas.json`.
- Vermelho: o pino está selecionado pelo usuário.

Ao selecionar um pino, deverá ser exibido um balão contendo:

- Nome da Casa de Oração.
- Endereço completo (quando disponível).
- Agenda de cultos (dias e horários).
- Link para abrir o trajeto utilizando o Google Maps.
- Link para abrir o trajeto utilizando o Waze.

## Pesquisa

O aplicativo deverá possuir uma ferramenta de pesquisa de localidades.

Quando o usuário pesquisar um local:

- o mapa deverá ser centralizado na localização pesquisada;
- deverá ser desenhado um círculo vermelho indicando visualmente o ponto pesquisado.

Quando o texto da pesquisa for apagado:

- o círculo vermelho deverá desaparecer automaticamente.

## Onde Estou?

O aplicativo deverá possuir uma funcionalidade chamada **"Onde Estou?"**.

Ao ser acionada:

- obter a localização atual do dispositivo;
- centralizar o mapa na posição do usuário;
- funcionar tanto no Windows (durante o desenvolvimento) quanto em dispositivos móveis utilizando os serviços de localização da plataforma.

## Objetivos de desempenho

A aplicação deverá funcionar de forma eficiente mesmo contendo milhares de igrejas.

Ao implementar novas funcionalidades, priorizar sempre:

- baixo consumo de memória;
- renderização rápida do mapa;
- navegação fluida durante zoom e movimentação;
- comunicação eficiente entre .NET MAUI e JavaScript;
- evitar processamento desnecessário;
- evitar serializações repetidas;
- evitar recarregamentos completos do mapa quando uma atualização incremental for suficiente.

Toda nova funcionalidade deve preservar a responsividade da aplicação.

---

# Visão Geral do Projeto

CCB Mapas App é uma aplicação .NET MAUI multiplataforma destinada, atualmente, ao Windows (`net8.0-windows10.0.19041.0`).

Utiliza o Leaflet.js dentro de uma WebView para exibição do mapa.

## Tecnologias utilizadas

- Framework: .NET MAUI 8.0
- Plataforma alvo: Windows 10/11 (`net8.0-windows10.0.19041.0`)
- Biblioteca de mapas: Leaflet.js utilizando OpenStreetMap
- Armazenamento de dados: arquivos JSON locais

---

# Estrutura do Projeto

## MauiProgram.cs

Responsável pelo registro das dependências e configuração da aplicação.

- Registra `ChurchService` como Singleton.
- Registra `MainPage` como Transient.

## Models

### Church.cs

Modelo contendo as informações de uma igreja.

Principais propriedades:

- Nome
- Logradouro
- Cidade
- Latitude
- Longitude
- DiasDeCulto

## Services

### ChurchService.cs

Responsável por ler o arquivo `igrejas.json` e desserializar seu conteúdo.

## Resources/Raw

### igrejas.json

Arquivo contendo o banco de dados das igrejas.

### map.html

Página HTML responsável pela renderização do mapa Leaflet e pela comunicação JavaScript com a aplicação MAUI.

## Views

### MainPage.xaml / MainPage.xaml.cs

Contém a WebView (`MapWebView`) responsável por carregar o arquivo `map.html`.

---

# Observações Arquiteturais Importantes

## 1. Diferença entre o formato esperado e o JSON atual

O método `ChurchService.GetChurchesAsync()` espera que `igrejas.json` seja um Array JSON (`List<Church>`).

Entretanto, atualmente o arquivo contém apenas um Objeto JSON.

Como consequência, ocorre uma `JsonException`, capturada pelo bloco try/catch, retornando uma lista vazia.

Sempre que forem adicionadas novas igrejas, o arquivo deverá ser convertido para um Array JSON válido.

---

## 2. Injeção de Dependência

Embora `MainPage` esteja registrada no container de DI, `App.xaml.cs` ainda cria sua instância utilizando:

```csharp
new MainPage();
```

Assim, atualmente não existe injeção de dependência através do construtor da MainPage.

---

## 3. Recursos Embutidos

Os arquivos:

- igrejas.json
- map.html

são removidos da coleção padrão de MauiAssets e registrados como EmbeddedResource no projeto.

O arquivo `map.html` é carregado através do recurso incorporado `CCB_Mapas_App.map.html`.

---

# Compilação

Restaurar dependências

```powershell
dotnet restore
```

Compilar

```powershell
dotnet build -f net8.0-windows10.0.19041.0
```

Executar

```powershell
dotnet run -f net8.0-windows10.0.19041.0
```

---

# Convenções de Desenvolvimento

1. Nullable Reference Types estão habilitados.

2. Implicit Usings estão habilitados.

3. Os arquivos JSON utilizam caracteres Unicode escapados (`\u00E1`, `\u00E7`, etc.) para manter compatibilidade entre plataformas.

Sempre manter este padrão.

---

# Código Temporário de Debug

Existe um trecho temporário em `MainPage.xaml.cs`, dentro do evento `Navigated`, utilizado para verificar a existência das funções JavaScript `loadChurchesBase64` e `receiveDataFromMaui`.

Este código poderá ser removido quando não for mais necessário.