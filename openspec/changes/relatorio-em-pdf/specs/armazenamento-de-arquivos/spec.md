## ADDED Requirements

### Requirement: Conversão em PDF

O armazenamento MUST dizer se converte arquivos em PDF. Com o SharePoint, ele MUST converter um arquivo guardado de até 2 MB pelo Microsoft Graph e devolver o PDF sem guardá-lo. Um arquivo maior MUST ser recusado sem chamar o Graph, e a recusa do Graph MUST ser informada com o código dela. A implementação local MUST NOT converter (ADR-0120, ADR-0165).

#### Scenario: Converter um PowerPoint no SharePoint
- **WHEN** um módulo pede o PDF de um PowerPoint de 300 KB guardado no SharePoint
- **THEN** recebe o conteúdo do PDF, e nada novo fica na pasta do PowerPoint

#### Scenario: Arquivo acima do limite da conversão
- **WHEN** um módulo pede o PDF de um arquivo de 3 MB guardado no SharePoint
- **THEN** o pedido é recusado como grande demais para a conversão, e o Graph não é chamado

#### Scenario: Conversão recusada pelo Graph
- **WHEN** o Graph recusa a conversão de um arquivo
- **THEN** o módulo recebe a falha com o código HTTP e o código do Graph, depois das novas tentativas de 429, 503 e 504

#### Scenario: Sem conversão na implementação local
- **WHEN** a instalação guarda os arquivos na pasta local, e um módulo pergunta se o armazenamento converte em PDF
- **THEN** a resposta é não, e pedir o PDF falha sem mudar nada
