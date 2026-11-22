## MODIFIED Requirements

### Requirement: O que vem do sistema

O relatório MUST trazer, pelas regras da visão geral e do cronograma, no dia de hoje (ADR-0073, ADR-0164, ADR-0165):
- O cliente e o logo, o projeto, a situação, o gerente e a data.
- O andamento total e de cada fase, contra o previsto.
- O fim com o desvio contra a linha de base.
- Até 5 próximos marcos não atingidos.
- As fases com os períodos e a sequência: até 5, da primeira em aberto em diante.
- Até 5 tarefas atrasadas, com o que empurram.
- Os chamados em aberto e os vencidos.

#### Scenario: Andamento da fase contra o previsto
- **WHEN** a fase Configuração tem uma tarefa que deveria estar inteira até hoje e outra que ainda não começou, as duas com a mesma duração, e está em 30%
- **THEN** o relatório mostra a fase com 30% de andamento e 50% previsto

#### Scenario: Próximos marcos no relatório
- **WHEN** o projeto tem 7 marcos não atingidos
- **THEN** o relatório mostra os 5 mais próximos pelo dia, cada um com o dia e os dias úteis que faltam ou o atraso

#### Scenario: Tarefas atrasadas além do limite
- **WHEN** o projeto tem 7 tarefas atrasadas
- **THEN** o relatório mostra as 5 mais atrasadas, com o que cada uma empurra, e "e mais 2 tarefas atrasadas"

#### Scenario: Projeto sem cronograma
- **WHEN** a gerente gera o relatório de um projeto sem tarefas nem marcos
- **THEN** o relatório é gerado, e as partes do andamento, das fases e dos marcos dizem "Sem cronograma"

#### Scenario: Sequência das fases
- **WHEN** o projeto tem 7 fases, e as duas primeiras estão concluídas
- **THEN** a faixa do slide "Status do projeto" mostra da terceira à sétima fase, cada uma com o período e o andamento

### Requirement: Modelo padrão do relatório

O relatório MUST sair num PowerPoint 16:9 que abre sem reparo, no modelo padrão do GerenWare, nesta ordem: "Status do projeto", "Cronograma", "Próximos passos", um slide por risco e "Horas do projeto", este só com "Mostrar horas". O visual MUST ser o da marca: a faixa escura no alto, os cartões brancos sobre fundo claro e o status por ícone, cor e texto. Nenhum nome de empresa fica no código (ADR-0003, ADR-0065, ADR-0165, ADR-0168).

#### Scenario: Relatório abre sem reparo
- **WHEN** a gerente gera o relatório com dois riscos e "Mostrar horas"
- **THEN** o arquivo passa no validador do formato do PowerPoint e tem seis slides, na ordem do modelo

#### Scenario: Cores da GerenWare no relatório
- **WHEN** a instalação não tem cores
- **THEN** a faixa sai no degradê grafite do painel da página de entrada, e as barras de andamento na esmeralda `#047857`

#### Scenario: Cores da instalação no relatório
- **WHEN** a instalação tem a cor principal `#0F766E`
- **THEN** as barras de andamento saem em `#0F766E`, a faixa num tom escuro do mesmo matiz, e as etiquetas de status nas cores de sempre

#### Scenario: Etiqueta na faixa escura
- **WHEN** o projeto está Em andamento
- **THEN** a faixa do slide "Status do projeto" mostra a situação numa etiqueta com o ícone e o texto nas cores de "em andamento" do tema escuro

#### Scenario: Logo da instalação para fundo escuro
- **WHEN** a instalação tem o logo para fundo escuro
- **THEN** a faixa mostra esse logo, sem quadro

#### Scenario: Só o logo claro da instalação
- **WHEN** a instalação tem só o logo para fundo claro
- **THEN** a faixa mostra o logo num quadro branco

#### Scenario: Instalação com o nome do produto e sem logo
- **WHEN** a instalação se chama GerenWare e não tem logo
- **THEN** a faixa mostra o logo da GerenWare para fundo escuro

#### Scenario: Instalação e cliente sem logo
- **WHEN** a instalação tem outro nome e nenhum logo, e o cliente do projeto não tem logo
- **THEN** a faixa mostra o nome da instalação escrito em branco, e o quadro do cliente mostra o nome dele

### Requirement: Campos do modelo

O modelo MUST receber os valores por campos entre chaves duplas (como `{{projeto.nome}}`), que mantêm o formato de quem os escreveu, mesmo quando o PowerPoint guardou o campo em pedaços. Uma lista MUST repetir a linha-modelo da sua tabela ou o bloco `lista.{nome}` para cada item; a lista vazia sai do slide. O slide de risco MUST se repetir para cada risco. Os logos, as barras, as marcas e as etiquetas MUST ser formas pelo nome (ADR-0165, ADR-0168).

#### Scenario: Campo quebrado em pedaços
- **WHEN** o modelo tem `{{projeto.nome}}` guardado em três pedaços de texto, o primeiro em negrito
- **THEN** o relatório mostra o nome do projeto inteiro, em negrito

#### Scenario: Lista numa tabela
- **WHEN** o projeto tem quatro fases, e o modelo tem a tabela das fases
- **THEN** a tabela das fases tem o cabeçalho e uma linha para cada fase, no formato da linha-modelo

#### Scenario: Lista em blocos
- **WHEN** o projeto tem quatro fases, e o modelo tem o bloco `lista.fase`
- **THEN** o bloco se repete quatro vezes, um embaixo do outro, cada um com os dados da fase

#### Scenario: Barra e marca de um item
- **WHEN** a fase Configuração está em 35% com 70% previsto
- **THEN** a barra do bloco dela cobre 35% do trilho, e a marca do previsto fica em 70% dele

#### Scenario: Etiqueta pelo tom do valor
- **WHEN** um próximo marco está atrasado e outro está em dia
- **THEN** a etiqueta do dia do atrasado sai no tom de perigo, com o ícone dele, e a do outro no tom de destaque

#### Scenario: Lista vazia
- **WHEN** o projeto não tem tarefas atrasadas
- **THEN** a lista das tarefas atrasadas sai do slide, e o slide diz "Nenhuma tarefa atrasada"
