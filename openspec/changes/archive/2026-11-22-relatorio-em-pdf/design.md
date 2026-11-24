## Context

A Status-4 gera o relatório de status em PowerPoint no servidor (Open XML SDK), guarda o arquivo como documento na pasta dos relatórios pelo contrato `IStatusReportDocuments` (módulo `Documents`) e grava os textos e os riscos em `projects.status_report_notes` para o próximo rascunho. O ADR-0165 escolheu, para o PDF, a conversão pelo Microsoft Graph do `.pptx` guardado no SharePoint (P1), sem licença, sem pacote e igual ao PowerPoint, e deixou de fora o PDF sem o SharePoint.

O aplicativo da instalação tem só `Sites.Selected`, com o papel de escrita no site (ADR-0120). A documentação de "Download a file in another format" (`GET .../content?format=pdf`) cita, para aplicativo, só `Files.ReadWrite.All` e `Sites.ReadWrite.All`, e limita a conversão a arquivos de até 2 MB. Ela responde 302 com um endereço pré-autenticado do PDF, que vale por alguns minutos e não leva o token. O ADR-0165 pede um teste no tenant de desenvolvimento antes da Status-5. Em 21/11/2026, Marcelo decidiu começar a change e rodar o teste depois, antes de aprovar o PR (D8).

O `SharePointFileStorage` já segue o endereço que o Graph devolve ao abrir um arquivo e repete as chamadas com 429, 503 e 504 depois do `Retry-After`. O `FakeSharePoint` simula o site nos testes, e a implementação local não fala com o Microsoft 365.

## Goals / Non-Goals

**Goals:**

- O PDF do relatório, escolhido no diálogo, igual ao PowerPoint, guardado como documento na pasta dos relatórios.
- A conversão como capacidade do armazenamento, atrás de um contrato, testada pelo `FakeSharePoint`, sem o Microsoft 365 real nos testes (ADR-0056).
- O limite de 2 MB e a falha da conversão tratados antes de guardar qualquer coisa que fique.
- Sem o SharePoint, a tela diz por que só há o PowerPoint.

**Non-Goals:**

- O PDF sem o SharePoint (P2 e P3), o modelo da empresa (Status-6) e o envio por e-mail.
- Uma permissão nova do aplicativo no Entra ID.
- Converter outros documentos do projeto em PDF.

## Decisions

### D1. A conversão num contrato próprio do `Files.Contracts`

O `IFileConversion` tem `ConvertsToPdf` e `ToPdfAsync(fileId, cancellationToken)`, que devolve um `FileDownload` com o PDF, o nome com `.pdf`, `application/pdf` e o tamanho, ou nulo quando o arquivo não existe. O limite fica em `FileConversion.MaxBytes`, 2 MB. A falha é a `FileConversionException`, com o motivo (`TooLarge` ou `Refused`), o código HTTP e o `error.code` do Graph.

- O `SharePointFileStorage` implementa os dois contratos, registrado uma vez e entregue como `IFileStorage` e `IFileConversion`.
- A implementação local registra o `NoFileConversion`, com `ConvertsToPdf` falso e o `ToPdfAsync` que lança `NotSupportedException`.

Alternativa: um método a mais no `IFileStorage`. Recusada, porque obrigaria a implementação local e o `FileStorageContract`, que vale igual para as duas, a ter uma operação que só uma faz.

### D2. Só o PDF fica na pasta dos relatórios

Para o PDF, o `Documents` põe o PowerPoint no armazenamento da pasta dos relatórios com um nome provisório (`{GUID v7}.pptx`, `KeepBoth`), sem registro de documento. Em seguida, pede o PDF ao `IFileConversion` e guarda o PDF como documento, ou como nova versão do PDF de mesmo nome. O provisório sai num `finally`, também na falha. No SharePoint, ele vai para a lixeira do site, como toda remoção (ADR-0120).

Alternativas:
- Guardar os dois documentos, o PowerPoint e o PDF. Recusada por proposta: o PowerPoint não foi pedido, ela dobra a pasta, e a pasta é o histórico do que foi mandado ao cliente. Marcelo pode trocar por esta na aprovação; a mudança seria só não remover o provisório e registrá-lo como documento.
- O provisório fora da pasta do projeto, na raiz. Recusada, porque a pasta do projeto pode ser uma pasta escolhida (ADR-0122) e a raiz pode ter outros usos.

### D3. O formato no domínio, no rascunho e no pedido

- **No domínio:** `StatusReportFormat` (`PowerPoint = 1`, `Pdf = 2`). O `StatusReportNotes` guarda o formato do último relatório, na coluna `format` de `projects.status_report_notes`, com 1 nas linhas que já existem (migração).
- **No rascunho:** `format` traz o do último relatório, ou o PowerPoint na primeira vez. `pdfAvailable` vem do `IFileConversion.ConvertsToPdf`. Sem o PDF disponível, o rascunho traz o PowerPoint, mesmo que o último tenha sido em PDF.
- **No pedido:** `format` é opcional; sem ele, vale o PowerPoint, como na carga da demonstração. O PDF sem conversão dá 400 em `format` (`StatusReportField.Format`).
- **O nome do arquivo:** o `IStatusReportRenderer.FileNameOf` passa a receber o formato e dá a extensão.

### D4. A ordem da geração em PDF

1. A conferência de quem gera e do formulário, como hoje; o PDF sem conversão é um campo inválido.
2. Os dados e o PowerPoint, como hoje.
3. Acima de `FileConversion.MaxBytes`, 409 `status-report-too-large-for-pdf`, antes de qualquer envio.
4. O `IStatusReportDocuments.SavePdfAsync(projectId, name, powerPoint, userId)`, no `Documents`: a pasta dos relatórios (criada como no PowerPoint), o provisório, a conversão, o PDF como documento e a remoção do provisório. A procura e a criação da pasta passam a ser um método só, usado pelo PowerPoint e pelo PDF.
5. Os textos e o formato para o próximo rascunho.

Uma `FileConversionException` vira o resultado `PdfFailed` e a resposta 502 `status-report-pdf-failed`, sem gravar os textos. O `TooLarge` que o Graph ainda devolva, com um tamanho que mudou no meio, vira o 409. Uma falha ao gravar o documento depois da conversão faz o que o PowerPoint já faz: o arquivo novo sai do armazenamento.

### D5. A conversão no SharePoint

- `GET drives/{drive}/items/{id}/content?format=pdf`, pelo mesmo `SendAsync`, com as novas tentativas e o endereço da resposta seguido sem o token. O `HttpClient` não leva o `Authorization` no redirecionamento para outro host; o abrir de hoje já depende disso.
- Antes, ela confere que o item está dentro da raiz (o de fora conta como inexistente, e a resposta é nula, como no abrir) e o tamanho dele: acima do limite, `TooLarge`, sem chamar o Graph.
- Resposta sem sucesso vira `Refused`, com o código HTTP e o `error.code`, sem o nome do arquivo nem o token, e vai para o log como aviso na rota, que tem o registro de log (os casos de uso não têm).
- O PDF é lido para a memória antes de devolver, porque o endereço pré-autenticado vale poucos minutos e o PDF tem no máximo alguns megabytes.

### D6. As respostas de erro

- **409** `status-report-too-large-for-pdf`, com `maxBytes`.
- **502** `status-report-pdf-failed`.
- **400** em `format` sem conversão, pelo `StatusReportField`, como os outros campos.

Os três em Problem Details (ADR-0061). A tela mostra o motivo no aviso do diálogo e sugere o PowerPoint; o diálogo continua aberto, e nada é baixado.

### D7. A tela

- "Formato" (`SegmentedControl` com "PowerPoint" e "PDF") fica logo antes de "Mostrar horas", porque os dois dizem como o relatório sai.
- Sem o PDF disponível, a opção PDF fica desabilitada, com o motivo embaixo, no tamanho e na cor da ajuda de "Mostrar horas": "O PDF é feito pelo Microsoft 365 com os arquivos no SharePoint, que esta instalação não usa."
- O `SegmentedOption` ganha `disabled`: `aria-disabled`, a cor `--text-disabled`, o cursor `not-allowed`, e fica fora do clique e das setas. É o mesmo token que o `Checkbox` usa desabilitado (ADR-0062, ADR-0065).
- O texto de abertura deixa de dizer "sai em PowerPoint".
- O aviso depois de gerar continua "Relatório guardado em {pasta}", com "Abrir documentos".

### D8. O teste da conversão antes da aprovação

Marcelo roda no SSH do App Service do `dev` um script que só lê, entregue no chat e fora do repositório. O script:
1. Pede o token do aplicativo pela identidade gerenciada, como a aplicação faz.
2. Acha o relatório de status gerado no ERP-FIN pela tela.
3. Pede o PDF dele ao Graph.
4. Escreve só o resultado, sem token.

O resultado vai para este design e para a descrição do PR.

Resultado: em 22/11/2026, Marcelo gerou pela tela, no `dev`, o relatório em PowerPoint e em PDF. A conversão pelo Graph funcionou com o `Sites.Selected`, sem permissão nova, e o ADR-0165 vale como está.
- Se o Graph recusar com o `Sites.Selected`, a change para antes do merge. Um ADR novo substitui o ADR-0165 com a página de impressão (P3), e esta change é refeita por ele.
- A aparência do PDF convertido só pode ser conferida no `dev` depois do merge, porque o `deploy-dev` publica só a `main`; o que precisar de ajuste vira a Status-5.1.

### D9. O `FakeSharePoint`

- O `/content?format=pdf` de um arquivo responde 200 com um PDF mínimo, que começa com `%PDF-` e diz o nome e o tamanho do original, para os testes conferirem o que foi convertido.
- `ConversionFailure` faz as próximas conversões responderem com o código HTTP e o `error.code` escolhidos pelo teste.
- O `Requests` já registra as chamadas, para o teste conferir que o arquivo grande não chega ao Graph.

## Risks / Trade-offs

- [O Graph recusa a conversão com o `Sites.Selected`] → O teste de D8 antes da aprovação; a saída é um ADR novo, não ampliar a permissão do aplicativo.
- [O PowerPoint provisório fica na lixeira do site] → Ela se esvazia sozinha (93 dias no SharePoint Online), e o arquivo tem algumas centenas de kilobytes. A alternativa de D2 evita isso, ao custo de dois documentos por relatório.
- [Logos grandes passam dos 2 MB] → O 409 antes de guardar, com a sugestão do PowerPoint. Os dois logos têm até 1 MB cada (`LogoFile.MaxBytes`, `ClientLogo.MaxBytes`), então só os dois no limite chegam lá.
- [A conversão demora alguns segundos] → O botão fica em andamento até a resposta (ADR-0131). O `HttpClient` do SharePoint tem 2 minutos.
- [A fidelidade do PDF: fontes e logos SVG] → A Calibri e o SVG são do próprio Office; a conferência visual fica para o `dev` depois do merge (D8).

## Migration Plan

- Migração do `Projects`: a coluna `format` (int, não nula, padrão 1) em `projects.status_report_notes`. As linhas que já existem ficam com o PowerPoint.
- Sem mudança no Terraform, no Entra ID nem nos scripts de instalação.
- Para voltar atrás: a migração tira a coluna, e o relatório volta a sair só em PowerPoint.

## Open Questions

- D2 (só o PDF, ou o PDF e o PowerPoint) fica para Marcelo confirmar na aprovação da proposta.
