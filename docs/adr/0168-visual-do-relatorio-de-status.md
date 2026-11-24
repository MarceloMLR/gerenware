---
status: aceita
date: 2026-11-22
decision-makers: Marcelo Ribeiro
---

# ADR-0168: Visual do modelo padrão do relatório de status

Completa, no ADR-0165, a linha "Modelo padrão": o relatório deixa a página branca e passa a ter o visual da marca GerenWare (ADR-0159), com as cores da instalação (ADR-0146). O resto do ADR-0165 continua. Aprovado por Marcelo em 22/11/2026, pelo desenho dos cinco slides com os dados da demonstração.

## Contexto e problema

O modelo padrão da Status-4 é uma página branca com títulos, cartões de borda fina e tabelas. Marcelo gerou o relatório e o PDF no `dev` e achou o resultado cru: o padrão precisa ser bonito, elegante, com as cores da marca, e não só uma página em branco. O exemplo de status report trazido por ele em 19/11/2026 tem faixa escura, números grandes, barras por frente de trabalho, marcos em destaque e uma faixa com a previsão do fim. Como o relatório fica com a identidade da GerenWare e da empresa, sem perder a regra de status por ícone, cor e texto (ADR-0065), e continua editável pela empresa na Status-6?

## Fatores de decisão

- A identidade da marca: grafite, esmeralda e as formas do painel da página de entrada (ADR-0159, ADR-0160).
- As cores da instalação, quando ela tem (ADR-0146), e as de status, que nunca mudam (ADR-0065).
- O mesmo desenho no PowerPoint, no PowerPoint na web e no PDF convertido pelo Microsoft 365 (ADR-0165).
- O modelo continua feito de formas com nome e campos entre chaves, para a empresa editar o dela (Status-6).

## Opções consideradas

- D1. Manter a página branca, com ajustes de espaço e fonte.
- D2. O modelo desenhado por código com o visual da marca: a faixa escura, os cartões sobre fundo claro, as barras e as etiquetas, com as cores da instalação.
- D3. Os slides como imagens prontas, desenhadas no servidor.
- D4. Um modelo desenhado no PowerPoint e guardado como arquivo no sistema.

## Resultado da decisão

Opção escolhida: **D2**, pela decisão de Marcelo. D1 não resolve a crítica; D3 tira a edição e os textos do arquivo; D4 separa as cores e as medidas dos tokens do design system, que o código usa direto.

Regras:

| Item | Definição |
| --- | --- |
| Fundo | O slide em `bg-subtle`, com o conteúdo em cartões brancos, de borda `border`, canto de 10 px e uma sombra leve. A sombra, que nas telas fica só no que flutua, vale no relatório, que é um documento e não uma tela |
| Faixa | No alto de cada slide, um degradê escuro com os anéis e os pontos do painel da página de entrada. Sem cor da instalação, o grafite do painel (`brand-panel-bg` a `brand-panel-bg-2`); com ela, um tom escuro do matiz da cor principal. O slide 1 tem a faixa maior, com o nome do projeto |
| Destaque | A cor principal da instalação, ou a esmeralda, nas barras de andamento, nos filetes e nos dias em destaque; na faixa, um tom claro do mesmo matiz |
| Status | Etiquetas com ícone, cor e texto nos tokens de status (ADR-0065), nas cores do tema claro sobre o fundo claro e nas do tema escuro sobre a faixa; a cor da instalação nunca entra nelas |
| Logo da instalação | Na faixa, o logo para fundo escuro; sem ele, o logo da GerenWare para fundo escuro, quando a instalação tem o nome do produto e não tem logo; o logo claro num quadro branco; ou o nome escrito em branco |
| Logo do cliente | Num quadro branco no começo da faixa do slide 1, ou o nome do cliente escrito nele |
| Slide 1, Status do projeto | Os quatro números em cartões, o resumo do período, os próximos marcos com o dia em destaque e uma faixa escura com o fim previsto e a sequência das fases |
| Slide 2, Cronograma | As fases em barras de andamento com a marca do previsto e as tarefas atrasadas, com o atraso e o que empurram |
| Slide 3, Próximos passos | Três cartões com ícone: o foco atual, os próximos passos e o que precisamos do cliente |
| Slides de risco | "Risco 1 de 2", com a classificação em etiqueta, o status, a prioridade, o fornecedor, o ponto de atenção em destaque e os marcos que dependem do chamado |
| Slide das horas | Os quatro números, o consumo das horas numa barra e as horas de cada tarefa com uma barra das apontadas contra as previstas |
| Fonte | Calibri, como no ADR-0165 |
| Modelo editável | As listas também saem em blocos repetidos (a forma agrupada `lista.{nome}`), com barras, marcas e etiquetas por item; as tabelas continuam valendo, para o modelo da empresa |

### Consequências

- Bom, porque o relatório que vai ao cliente tem a cara da marca e da empresa, sem perder a leitura do status por ícone, cor e texto.
- Bom, porque o modelo da empresa (Status-6) ganha os blocos, as barras e as etiquetas que o padrão usa.
- Ruim, porque o gerador do relatório fica maior: os blocos repetidos, as marcas e as etiquetas no fundo escuro são código novo, com os testes dele.
- Ruim, porque a aparência no PDF convertido pelo Microsoft 365 só pode ser conferida no `dev`.

### Confirmação

Testes do preenchimento (os blocos, as barras, as marcas, as etiquetas nos dois fundos e os logos), o arquivo no validador do Open XML SDK e o relatório da demonstração aberto no LibreOffice e conferido slide a slide; no `dev`, o PowerPoint e o PDF gerados pela tela (ADR-0056).

## Prós e contras das opções

### D1. Página branca com ajustes

- Bom, porque não muda o gerador.
- Ruim, porque continua sem a cara da marca, que foi a crítica.

### D2. Modelo desenhado por código com a marca

- Bom, porque usa os tokens do design system e as cores da instalação direto.
- Bom, porque o arquivo continua editável e com textos de verdade.
- Ruim, porque pede blocos repetidos, marcas e etiquetas no gerador.

### D3. Slides como imagens

- Bom, porque o desenho fica livre.
- Ruim, porque o cliente não edita nem copia o texto, e o arquivo fica pesado.

### D4. Modelo desenhado no PowerPoint

- Bom, porque o desenho é feito numa ferramenta de desenho.
- Ruim, porque as cores, os cantos e as medidas deixam de vir dos tokens, e a cor da instalação teria de ser trocada no arquivo.
