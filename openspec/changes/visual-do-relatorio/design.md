## Context

O relatório de status (Status-4) é montado em `Infrastructure/StatusReports` do `Projects`:
- o `DefaultStatusReportTemplate` desenha o modelo padrão por código (Open XML SDK) a cada relatório;
- o `StatusReportFiller` o preenche: os campos `{{...}}`, a linha-modelo das tabelas, o slide de risco repetido, o slide das horas, os logos, a barra do andamento e as duas etiquetas;
- o `PptxStatusReportRenderer` monta os valores a partir do `StatusReportData`.

O modelo atual é branco, com cartões de borda fina e tabelas. Marcelo aprovou em 22/11/2026 o desenho novo dos cinco slides, com a sombra leve nos cartões e as fases nos dois primeiros slides (ADR-0168). O desenho foi feito com os dados da demonstração e conferido no LibreOffice. As medidas abaixo são as dele, em polegadas, no slide de 13,333 por 7,5.

Teste feito para este design: o LibreOffice desenha a imagem SVG dentro do PowerPoint (a extensão `svgBlip`), como o PowerPoint 2016 ou mais novo e o PowerPoint na web. Os ícones e o logo da GerenWare podem ser SVG, com a cor trocada no texto do arquivo.

## Goals / Non-Goals

**Goals:**

- O modelo padrão no visual aprovado, com as cores da instalação e as de status de sempre.
- Os recursos do gerador (blocos, barras e marcas por item, etiquetas genéricas, o quadro do logo) gerais, documentados e testados, para o modelo da empresa (Status-6).
- O arquivo continua no validador do Open XML SDK e com textos editáveis.

**Non-Goals:**

- Mudar os dados do relatório, além da sequência das fases.
- O tema escuro do relatório, a fonte da marca no arquivo e o modelo da empresa.

## Decisions

### D1. Os slides

Comum a todos:
- o fundo `bg-subtle`;
- a faixa no alto, de 1,1 de altura (1,7 no slide 1);
- o pé, em 7,13, com "ERP-FIN · Relatório de status · 20/11/2026" à esquerda e o número do slide à direita, em `text-placeholder`;
- as margens de 0,6, com 0,25 entre os cartões.

| Slide | Conteúdo |
| --- | --- |
| Status do projeto | Na faixa: o quadro do cliente (1,35 por 0,92), "Relatório de status · 20 de novembro de 2026", o nome do projeto em 28 pt, "ERP-FIN · Gerente: … · Cliente: …", o logo da instalação e a etiqueta da situação. Embaixo, os quatro cartões (Andamento com a barra e a marca do previsto; Prazo final com a etiqueta do desvio; Próximo marco com a etiqueta do dia e do atraso; Chamados em aberto com a etiqueta dos vencidos), o cartão do resumo (7,55 de largura) e o dos próximos marcos (até 5 blocos), e a faixa escura do fim previsto com a sequência das fases (até 5 blocos lado a lado) |
| Cronograma | O cartão das fases (até 10 blocos: nome, período, barra com a marca do previsto e o percentual) e o das tarefas atrasadas (até 5 blocos: nome, etiqueta do atraso e o que empurra), com a quantidade numa etiqueta |
| Próximos passos | Três cartões, cada um com o ícone num círculo do tom de destaque, o título, um filete e o texto |
| Risco | "Risco 1 de 2" na faixa; o cartão do chamado (o número e o título, a etiqueta da classificação, os quadros do status, da prioridade e do fornecedor, e o ponto de atenção num quadro do tom de aviso com o ícone) e o dos marcos que dependem dele (até 5 blocos) |
| Horas do projeto | Um cartão com os quatro números em dois por dois, a etiqueta do estado e a barra do consumo, e o cartão das horas por tarefa (até 15 blocos: nome, barra das apontadas contra as previstas, previstas e apontadas) |

O slide das horas sai do desenho aprovado: com até 15 tarefas, os números vão para um cartão à esquerda, e as tarefas ganham a altura toda à direita.

### D2. As cores

As cores vêm de uma classe só, `ReportColors`, a partir dos tokens (`tokens.json`, embutido) e da instalação:

| Papel | Sem cor da instalação | Com a cor principal |
| --- | --- | --- |
| Faixa e faixa do fim previsto (degradê) | `brand-panel-bg` a `brand-panel-bg-2` | A claridade desses dois tokens no matiz da cor principal, com parte da saturação dela |
| Formas da faixa (anéis e pontos) | `brand-panel-shape` | O mesmo |
| Textos da faixa | `text-inverse` e `brand-panel-muted` | O mesmo |
| Destaque (barras, filetes, dia do marco) | `action` | A cor principal |
| Fundo do destaque (dia do marco, círculo do ícone) | `bg-selected` | A claridade de `bg-selected` no matiz da cor principal |
| Destaque na faixa (percentual das fases) | `brand-panel-accent` | A claridade dele no matiz da cor principal |
| Etiquetas e barras de status | Os tokens de status, nunca a cor da instalação (ADR-0065, ADR-0146) | O mesmo |

O texto no tom de destaque sobre o fundo de destaque é escurecido até o contraste de 4,5 para 1. Assim, qualquer cor escolhida funciona, como nas telas.

As cores vão no tema do arquivo (`accent1` e `accent2`, como hoje) e direto nas formas. As cores da instalação são as mesmas das telas: a principal, que hoje já vem no `InstallationBrand`.

### D3. Blocos repetidos (`lista.{nome}`)

Um grupo de formas com o nome `lista.{nome}` é o bloco de um item da lista `{nome}`:
- O gerador o repete para cada item, deslocando cada cópia pela altura do grupo. Com o nome terminando em `.lado`, ele desloca pela largura.
- O modelo desenha o grupo já com o espaço até o próximo.
- Os campos dentro do grupo leem o item e, sem ele, o relatório.
- A lista vazia tira o grupo, e o campo `{nome}s.vazio` diz o que falta, como nas tabelas.
- As tabelas continuam valendo para quem preferir no modelo da empresa.

Alternativa: só tabelas. Recusada, porque a célula de uma tabela não tem barra nem etiqueta.

### D4. Barras, marcas e etiquetas

- **Barra** `barra.{nome}`, com o trilho `barra.{nome}.trilho` no mesmo bloco ou slide: a largura é a parte do trilho que o valor dá.
  - Com um tom no valor (a tarefa acima das horas previstas), ela toma a cor de gráfico do tom.
  - A paleta segue o trilho: clara num trilho claro e escura num trilho escuro.
- **Marca** `marca.{nome}`: o gerador a move até o ponto do trilho `barra.{nome}.trilho` que o valor dá, ou a tira quando não há valor (sem previsto).
- **Etiqueta** `etiqueta.{nome}`, com o ícone `etiqueta.{nome}.icone`:
  - o valor traz o tom (perigo, aviso, sucesso, informação, neutro ou destaque) e o ícone;
  - a paleta é a do tema claro ou a do escuro, pela claridade do preenchimento com que o modelo a desenhou;
  - o ícone é o mesmo das telas, um SVG com a cor do tom; o tom de destaque não tem ícone.
- As etiquetas de hoje (`risco.classificacao`, `horas.situacao`) passam a ser `etiqueta.classificacao` e `etiqueta.horas`. Nenhum modelo de empresa existe ainda.

Os ícones de cada valor são os das telas:

| Valor | Ícone |
| --- | --- |
| Situação do projeto | `projectStatus.ts` |
| Status do chamado | `TicketTags.tsx` |
| Prazo, próximo marco e vencidos | `DueDate.tsx` |
| Estado das horas | `projectHours.ts` |

Eles ficam em `Infrastructure/StatusReports/Icons`, com os caminhos do `@mui/icons-material` (MIT).

### D5. Alinhamento das etiquetas

O modelo desenha o ícone centrado na altura das minúsculas do texto, e o texto subido para o centro da etiqueta, como no desenho aprovado: a margem de baixo de 3 pt na Calibri. A regra fica num lugar só, no `Canvas` do modelo, para todas as etiquetas.

### D6. Logos

O `InstallationBrand` (contrato do `Administration`) passa a entregar o logo para fundo escuro. O logo da instalação na faixa, nesta ordem:
1. O logo para fundo escuro.
2. Sem logo nenhum e com o nome do produto (`Product.Name`), o logo da GerenWare para fundo escuro: o SVG de `docs/marca/`, copiado, com o PNG de 128 px de reserva. Um teste compara as cópias com os originais.
3. O logo claro, no quadro `logo.instalacao.quadro`, que o gerador só mantém nesse caso.
4. O nome escrito em branco.

O logo do cliente fica sempre no quadro branco do slide 1, ou o nome dele.

### D7. Sombra, faixa e formas

- **Cartões:** retângulos de canto `radius-lg` (10 px), borda `border` e a sombra externa do token `shadow-menu` (4 px para baixo, 12 px de desfoque, `#1C1F24` com 12%).
- **Faixa:** um `gradFill` linear e o brilho em `signin-glow`, num degradê radial com transparência.
- **Anéis:** o preset `arc`, com os ângulos calculados para cada anel ficar dentro da faixa.
- **Pontos:** círculos pequenos.

Tudo em formas nativas, sem imagem, para a empresa editar.

### D8. Textos longos

O resumo e os textos dos próximos passos têm até 1.000 caracteres (Status-4). A caixa de texto vai com o ajuste "Reduzir o texto ao transbordar" (`normAutofit`). O gerador grava a escala da fonte pela estimativa das linhas que o texto ocupa na largura da caixa, de 100% a 80%, porque o PowerPoint só recalcula a escala quando o texto é editado.

Os nomes nos blocos são cortados no tamanho que cabe numa linha, com "…":

| Nome | Caracteres |
| --- | --- |
| Fase | 38 |
| Fase na faixa | 22 |
| Marco | 30 |
| Tarefa atrasada | 45 |
| Tarefa nas horas | 60 |

### D9. Os dados novos

| Campo ou lista | Valor |
| --- | --- |
| `relatorio.dataExtensa` | "20 de novembro de 2026" |
| `andamento.previsto` | "previsto 60%" (antes, "Previsto para hoje: 60%"), com a marca `marca.andamento` |
| Lista `etapa` | A sequência das fases: até 5, da primeira em aberto em diante. Com menos de 5 dali em diante, as anteriores completam; com todas concluídas, as 5 últimas. Cada uma com o nome, o período curto ("03/11 a 13/11"), o percentual e a barra no tom do estado: concluída em sucesso, em andamento em informação, sem começar neutra |
| `marco.diaCurto` | "26/11", na etiqueta `etiqueta.marco` (perigo no atrasado, destaque nos outros) |
| `risco.ordem`, `riscos.total` | "Risco 1 de 2" |
| `horas.consumo` e `barra.horas` | "212,5 de 360 h · 59%" |
| `barra.horasTarefa` | As apontadas contra as previstas, no tom de perigo quando passam delas |

A sequência das fases sai do domínio (`StatusReportPhases.SequenceOf`), testada sem o PowerPoint.

### D10. Verificação

- **Testes do preenchimento:** com modelos pequenos feitos nos próprios testes:
  - o bloco repetido para baixo e para o lado;
  - a lista vazia;
  - a barra e a marca de um item;
  - a etiqueta nos dois fundos e nos seis tons;
  - o quadro do logo;
  - o logo da GerenWare;
  - a escala dos textos longos.
- **Testes do modelo padrão:** o arquivo no validador e as formas com nome que o preenchimento espera.
- **Relatório da demonstração:** gerado com e sem cores da instalação, aberto no LibreOffice e conferido slide a slide.
- **Depois do merge:** o PowerPoint e o PDF gerados pela tela no `dev`.

## Risks / Trade-offs

- [O PowerPoint mostra a escala gravada do texto longo, e a estimativa pode errar] → Estimativa conservadora e o limite de 80%; o texto que ainda passar fica cortado pela borda da caixa, e a gerente o encurta no próximo rascunho.
- [O brilho radial com transparência sai diferente entre os programas] → É decoração; se ficar ruim no LibreOffice ou no PDF, ele sai.
- [O PowerPoint 2013 ou mais antigo não desenha SVG] → Os ícones mostram o PNG de reserva, um ponto transparente, e o texto da etiqueta continua dizendo o status.
- [O gerador cresce] → Os recursos são gerais e testados um a um, e o modelo da empresa os reaproveita.

## Migration Plan

Sem migração de banco nem mudança de API: o relatório novo sai no próximo que for gerado. Os relatórios já guardados ficam como estão.

## Open Questions

Nenhuma.
