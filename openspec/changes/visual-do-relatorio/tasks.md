## 1. Roadmap

- [ ] 1.1 A Status-4.1 (`visual-do-relatorio`) no `docs/roadmap.md`, "Em andamento", com a contagem do grupo 18

## 2. As cores, os ícones e os logos

- [x] 2.1 O `ReportColors`: os papéis de D2 a partir dos tokens e da cor principal, com o texto de destaque ajustado ao contraste de 4,5 para 1. Testes sem cor, com uma cor escura e com uma cor clara
- [x] 2.2 Os ícones das telas (D4) em `Infrastructure/StatusReports/Icons`, com a troca de cor, e o logo da GerenWare para fundo escuro embutido de `docs/marca/`, com o teste que confere os arquivos da marca
- [x] 2.3 O logo para fundo escuro no `InstallationBrand` (`Administration.Contracts`), com o teste do módulo, e nos dados do relatório (`StatusReportBrand`)

## 3. O gerador

- [x] 3.1 Os blocos repetidos `lista.{nome}`, para baixo e para o lado (D3), com a lista vazia. Testes com modelos feitos no teste, incluindo o cenário "Lista em blocos"
- [x] 3.2 As barras e as marcas de cada item, com a paleta pelo trilho e o tom do valor (D4). Testes do cenário "Barra e marca de um item"
- [x] 3.3 As etiquetas `etiqueta.*`: o tom e o ícone do valor e a paleta pelo fundo desenhado (D4, D5), no lugar das duas etiquetas de hoje. Testes dos cenários "Etiqueta pelo tom do valor" e "Etiqueta na faixa escura"
- [x] 3.4 O quadro do logo da instalação e a ordem dos logos (D6). Testes dos cenários dos logos de "Modelo padrão do relatório"
- [x] 3.5 A escala dos textos longos (D8), com o teste do resumo de 1.000 caracteres

## 4. O modelo padrão e os dados

- [x] 4.1 A sequência das fases no domínio (`StatusReportPhases.SequenceOf`), com os testes do cenário "Sequência das fases" e dos casos de D9
- [x] 4.2 Os campos novos de D9 no `PptxStatusReportRenderer`, com os textos no `pt-BR.json` do relatório e os cortes de D8
- [x] 4.3 O `DefaultStatusReportTemplate` redesenhado (D1, D2, D5, D7): a faixa, os cartões com sombra, os blocos e as etiquetas dos cinco slides. Testes:
  - o validador do Open XML SDK;
  - as formas com nome que o preenchimento espera;
  - os cenários de cores de "Modelo padrão do relatório";
  - o cenário "Relatório abre sem reparo".

## 5. Documentação e verificação

- [x] 5.1 O relatório da demonstração gerado com e sem cores da instalação, aberto no LibreOffice e conferido slide a slide, com os textos no limite
- [x] 5.2 Documentação e specs:
  - `CLAUDE.md`, na seção do mapa;
  - `docs/glossario.md`: bloco, marca e etiqueta do modelo;
  - `openspec validate --all --strict` limpo.
- [ ] 5.3 Verificação: `dotnet build -warnaserror`, `dotnet test` e `dotnet format`

## Workflow follow-up

- PR com a aprovação de Marcelo, archive na mesma branch antes do merge, roadmap "Concluída" e o merge por Marcelo.
- Depois do merge, o PowerPoint e o PDF gerados pela tela no `dev`; o que precisar de ajuste vira a Status-4.2.
