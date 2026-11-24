## Why

O relatório de status saiu no `dev` em PowerPoint e em PDF, mas o modelo padrão da Status-4 é uma página branca, com títulos, cartões de borda fina e tabelas. Marcelo achou o resultado cru: o padrão precisa ser bonito, elegante, com as cores da marca, e não só uma página em branco. Em 22/11/2026, ele aprovou o desenho dos cinco slides com os dados da demonstração, a sombra leve nos cartões e as fases nos dois primeiros slides (ADR-0168).

É uma correção da Status-4, arquivada, e por isso a Status-4.1. Vem antes da Status-6, porque o modelo da empresa parte do padrão e usa os mesmos recursos do gerador.

## What Changes

- **O modelo padrão com o visual da marca (ADR-0168):**
  - o fundo claro (`bg-subtle`), com o conteúdo em cartões brancos de canto de 10 px e uma sombra leve;
  - a faixa escura no alto de cada slide, em degradê, com os anéis e os pontos do painel da página de entrada;
  - os números grandes, as barras de andamento com a marca do previsto e as etiquetas de status com ícone, cor e texto.
- **Os slides:**
  - "Status do projeto": a faixa maior, com o quadro do logo do cliente, o nome do projeto, a situação e o logo da instalação. Embaixo dela ficam os quatro números, o resumo do período, os próximos marcos com o dia em destaque e uma faixa escura com o fim previsto e a sequência das fases.
  - "Cronograma": as fases em barras contra o previsto, e as tarefas atrasadas com o atraso e o que empurram.
  - "Próximos passos": três cartões com ícone.
  - Riscos: "Risco 1 de 2", com o ponto de atenção em destaque e os marcos que dependem do chamado.
  - "Horas do projeto": os quatro números, o consumo numa barra e as horas de cada tarefa com barra.
- **As cores:**
  - sem cor da instalação, o grafite e a esmeralda da GerenWare;
  - com a cor principal, a faixa num tom escuro do matiz dela e o destaque na própria cor;
  - as cores de status nunca mudam, nas versões do tema claro e do tema escuro conforme o fundo.
- **Os logos:**
  - o da instalação na faixa: o logo para fundo escuro; sem ele, o da GerenWare para fundo escuro, quando a instalação tem o nome do produto e não tem logo; o logo claro num quadro branco; ou o nome escrito;
  - o do cliente num quadro branco, ou o nome dele.
- **O gerador ganha recursos que o modelo da empresa (Status-6) também vai usar:**
  - as listas em blocos repetidos (a forma agrupada `lista.{nome}`), além das tabelas;
  - as barras e as marcas do previsto de cada item;
  - as etiquetas `etiqueta.*` com o tom de cada valor e a paleta clara ou escura pelo fundo em que o modelo as desenhou;
  - o quadro de um logo que só fica quando o logo precisa dele.
- **Os dados:** a sequência das fases da faixa, até 5 fases da primeira em aberto em diante, e os campos novos das datas curtas e por extenso, da ordem do risco e do consumo das horas.
- **O logo para fundo escuro:** o contrato `IInstallationBrand` passa a entregar também o logo para fundo escuro da instalação.

## Capabilities

### New Capabilities

Nenhuma.

### Modified Capabilities

- `relatorio-de-status`:
  - o modelo padrão com o visual da marca, as cores e os logos;
  - os campos do modelo com os blocos, as barras, as marcas e as etiquetas;
  - a sequência das fases no que vem do sistema.

## Non-goals

- O modelo da empresa, que ela edita e sobe (Status-6).
- Mudar o que o relatório diz: os textos, os riscos, as horas, os limites das listas e as regras dos dados continuam os da Status-4, menos a sequência das fases.
- A fonte da marca (IBM Plex Sans) no arquivo; continua a Calibri (ADR-0165).
- O tema escuro do relatório.

## Impact

- `src/Modules/Projects/GerenWare.Projects/Infrastructure/StatusReports`:
  - o `DefaultStatusReportTemplate` redesenhado;
  - o `StatusReportFiller` com os blocos, as marcas, as etiquetas genéricas e o quadro do logo;
  - os valores novos no `PptxStatusReportRenderer`;
  - as cores derivadas da instalação;
  - os ícones e o logo da GerenWare para fundo escuro, copiados de `docs/marca/` e dos ícones do design system;
  - os textos novos no `pt-BR.json` do relatório.
- `src/Modules/Projects/GerenWare.Projects/Application/StatusReportUseCases.cs`: a sequência das fases e o logo para fundo escuro nos dados.
- `src/Modules/Administration`: o logo para fundo escuro no `InstallationBrand`.
- Testes do `Projects` (o preenchimento e o modelo) e do `Administration`; o relatório da demonstração conferido no LibreOffice.
- `CLAUDE.md`, `docs/glossario.md`, o roadmap (a Status-4.1) e o ADR-0168.
- ADRs: 0065, 0146, 0159, 0160, 0165, 0168.
