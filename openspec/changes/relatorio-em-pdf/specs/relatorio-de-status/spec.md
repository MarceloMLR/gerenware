## ADDED Requirements

### Requirement: Relatório em PDF

Quem gera MUST escolher o PowerPoint ou o PDF. Com o SharePoint, o PDF MUST sair do PowerPoint, convertido pelo Microsoft 365, e só o PDF MUST ficar na pasta dos relatórios. O rascunho MUST trazer o formato do último relatório, o PowerPoint na primeira vez, e se o PDF está disponível. Sem o SharePoint, pedir o PDF MUST receber 400 em `format` (ADR-0120, ADR-0165).

#### Scenario: PDF guardado na pasta dos relatórios
- **WHEN** a gerente gera o relatório do ERP-FIN em PDF em 21/11/2026, numa instalação com o SharePoint
- **THEN** a pasta "Relatórios de status" tem o documento "ERP-FIN - Relatório de status - 2026-11-21.pdf", enviado por ela, e nenhum PowerPoint do relatório fica no armazenamento

#### Scenario: PDF gerado de novo no mesmo dia
- **WHEN** a gerente gera o relatório do ERP-FIN em PDF duas vezes em 21/11/2026
- **THEN** a pasta tem um PDF só, na versão 2

#### Scenario: Formato do último relatório
- **WHEN** a gerente gerou o último relatório do ERP-FIN em PDF e abre o rascunho de novo
- **THEN** o rascunho traz o PDF como formato, e o de um projeto sem relatório traz o PowerPoint

#### Scenario: PDF sem o SharePoint
- **WHEN** a instalação guarda os arquivos na pasta local, e a gerente pede o relatório em PDF
- **THEN** ela recebe 400 em `format`, o rascunho diz que o PDF não está disponível, e nada é guardado

### Requirement: Limite e falha da conversão

Um PowerPoint acima de 2 MB, o limite da conversão, MUST receber 409 `status-report-too-large-for-pdf` antes de qualquer arquivo ser guardado. Quando a conversão falha, a API MUST responder 502 `status-report-pdf-failed`, e nada MUST ficar guardado: nem o PDF, nem o PowerPoint usado na conversão, nem os textos para o próximo rascunho (ADR-0165).

#### Scenario: PowerPoint grande demais para o PDF
- **WHEN** os logos da instalação e do cliente deixam o PowerPoint do ERP-FIN acima de 2 MB, e a gerente pede o PDF
- **THEN** ela recebe 409 `status-report-too-large-for-pdf`, e a pasta dos relatórios não muda

#### Scenario: Conversão que falha
- **WHEN** o Microsoft 365 recusa a conversão do relatório do ERP-FIN
- **THEN** a gerente recebe 502 `status-report-pdf-failed`, a pasta dos relatórios não muda, o PowerPoint usado na conversão sai do armazenamento, e o rascunho continua com os textos do relatório anterior

## MODIFIED Requirements

### Requirement: Relatório guardado nos documentos

O relatório gerado MUST ser guardado como documento do projeto na pasta dos relatórios, com o nome `{código} - Relatório de status - {aaaa-mm-dd}` e a extensão do formato, `.pptx` ou `.pdf`. Gerar de novo no mesmo dia, no mesmo formato, MUST guardar uma nova versão do mesmo documento. Os textos e os riscos MUST ficar guardados para o próximo rascunho. O documento entra na auditoria como qualquer documento (ADR-0031, ADR-0039, ADR-0165).

#### Scenario: Relatório guardado na pasta dos relatórios
- **WHEN** a gerente gera o relatório do ERP-FIN em 20/11/2026
- **THEN** a pasta "Relatórios de status" do projeto tem o documento "ERP-FIN - Relatório de status - 2026-11-20.pptx", enviado por ela

#### Scenario: Relatório gerado de novo no mesmo dia
- **WHEN** a gerente gera o relatório do ERP-FIN duas vezes em 20/11/2026
- **THEN** a pasta tem um documento só, na versão 2

### Requirement: Gerar o relatório pela tela

"Gerar relatório de status" MUST aparecer em "Ações do projeto" só para quem gera e abrir o diálogo com (ADR-0133, ADR-0165, ADR-0166):
- Os quatro textos, pelo rascunho.
- Os chamados dos riscos, com os sugeridos escolhidos.
- A classificação e o ponto de atenção de cada risco.
- O formato do rascunho, e o PDF desabilitado, com o motivo, quando indisponível.
- "Mostrar horas", desmarcado.

Ao gerar, MUST baixar o arquivo e avisar em que pasta ele ficou. O erro de um campo volta ao campo.

#### Scenario: Gerar relatório pela tela
- **WHEN** a gerente abre "Gerar relatório de status", escreve o resumo, marca "Mostrar horas" e clica em "Gerar relatório"
- **THEN** a tela manda os textos, os riscos, as horas e o formato, baixa o arquivo e avisa "Relatório guardado em Relatórios de status"

#### Scenario: Ação só para quem gera
- **WHEN** o líder técnico abre "Ações do projeto"
- **THEN** a ação "Gerar relatório de status" não aparece

#### Scenario: Riscos sugeridos na tela
- **WHEN** a gerente abre o diálogo, e o rascunho sugere o chamado da rejeição 539
- **THEN** o chamado já vem escolhido, com a classificação Média e o campo do ponto de atenção

#### Scenario: Erro de um campo na tela
- **WHEN** a API responde 400 no campo dos riscos
- **THEN** o diálogo continua aberto, com o erro no campo dos riscos, e nada é baixado

#### Scenario: PDF pela tela
- **WHEN** a gerente escolhe "PDF" no formato e clica em "Gerar relatório"
- **THEN** a tela manda o formato PDF, baixa o PDF e avisa em que pasta ele foi guardado

#### Scenario: PDF indisponível na tela
- **WHEN** o rascunho diz que o PDF não está disponível
- **THEN** a opção PDF aparece desabilitada, com o motivo embaixo, e o PowerPoint vem escolhido

#### Scenario: Erro da conversão na tela
- **WHEN** a API responde 409 ou 502 à geração do PDF
- **THEN** o diálogo continua aberto, com o motivo e a sugestão do PowerPoint, e nada é baixado
