## Why

O relatório de status (Status-4) sai só em PowerPoint, e muitos clientes preferem receber o PDF, que abre em qualquer lugar e não se edita sem querer. O ADR-0165 decidiu que quem gera escolhe PowerPoint ou PDF e que o PDF sai do próprio PowerPoint, convertido pelo Microsoft Graph a partir do arquivo guardado no SharePoint (opção P1), para sair igual ao PowerPoint, sem licença e sem pacote novo.

É a Status-5 do grupo 18 (Status do projeto). O ADR-0165 pede, antes dela, um teste no tenant de desenvolvimento da conversão com a permissão que o aplicativo já tem (`Sites.Selected`), que a documentação da conversão não cita. Marcelo decidiu em 21/11/2026 começar a change e rodar o teste depois, antes de aprovar o PR. Se a conversão for recusada, a change para, e um ADR novo troca o PDF pela página de impressão (opção P3).

## What Changes

- **O formato do relatório:** o diálogo "Gerar relatório de status" ganha "Formato", com PowerPoint e PDF. Vem escolhido o formato do último relatório do projeto, e o PowerPoint na primeira vez.
- **O PDF pelo Microsoft 365:**
  - O GerenWare monta o PowerPoint como hoje e o põe no SharePoint com um nome provisório, na pasta dos relatórios.
  - O Graph o converte em PDF (`/content?format=pdf`), e o PowerPoint provisório sai em seguida.
  - Na pasta dos relatórios fica só o PDF, `{código} - Relatório de status - {aaaa-mm-dd}.pdf`. Gerar de novo no mesmo dia vira uma nova versão dele, como no PowerPoint.
- **Sem o SharePoint**, como na máquina de desenvolvimento, só o PowerPoint:
  - A opção PDF aparece desabilitada e diz por quê.
  - A API responde 400 em `format` a quem pedir o PDF.
- **O limite da conversão:** o Graph converte arquivos de até 2 MB. Um PowerPoint maior, o que só os dois logos grandes causam, recebe 409 antes de qualquer arquivo ser guardado, e a tela sugere o PowerPoint.
- **A conversão que falha** (o Graph fora do ar ou recusando) dá 502:
  - Nada fica guardado: nem o PDF, nem o PowerPoint provisório, nem os textos para o próximo rascunho.
  - A tela diz que o Microsoft 365 não converteu e sugere tentar de novo ou o PowerPoint.
- **O armazenamento ganha a conversão em PDF**, num contrato próprio do `Files.Contracts` (`IFileConversion`): o SharePoint converte pelo Graph, com as mesmas novas tentativas das outras chamadas, e a implementação local diz que não converte.
- **A escolha de opções** (`SegmentedControl`) ganha a opção desabilitada, com o token `text-disabled` do design system.

## Capabilities

### New Capabilities

Nenhuma.

### Modified Capabilities

- `relatorio-de-status`:
  - O relatório em PDF, com o limite e a falha da conversão.
  - O formato no rascunho e na tela.
  - O nome do arquivo com a extensão do formato.
- `armazenamento-de-arquivos`: a conversão de um arquivo guardado em PDF, no SharePoint, e a implementação local, que não converte.

## Non-goals

- O PDF sem o SharePoint, pela página de impressão (P3) ou pelo LibreOffice no servidor (P2); só se o teste da conversão falhar, por um ADR novo.
- Guardar o PowerPoint junto com o PDF (veja o design, D2).
- O modelo da empresa (Status-6).
- Mandar o relatório por e-mail ao cliente e gerá-lo toda semana sozinho (ADR-0165, "depois").
- Uma permissão nova do aplicativo no Entra ID. Se o `Sites.Selected` não bastar, a saída é o ADR novo, não ampliar a permissão (ADR-0030, ADR-0120).

## Impact

- `src/Modules/Files`:
  - O contrato `IFileConversion` e o limite da conversão no `Files.Contracts`.
  - A conversão pelo Graph no `SharePointFileStorage`, e a implementação sem conversão para a local.
- `src/Modules/Documents`: o `StatusReportDocuments` guarda o PDF convertido (o PowerPoint provisório, a conversão e a remoção).
- `src/Modules/Projects`:
  - O formato no domínio (`StatusReportFormat`), no rascunho e na geração.
  - O formato do último relatório em `projects.status_report_notes`, com uma migração.
  - O 409 e o 502 da rota `POST /api/projects/{id}/status-report`.
  - O nome do arquivo com a extensão.
- `src/Modules/Projects/GerenWare.Projects.Contracts`: o `IStatusReportDocuments` ganha guardar o PDF.
- `tests/GerenWare.Testing`: o `FakeSharePoint` simula a conversão em PDF e a falha dela.
- `web/src/projects/StatusReportDialog.tsx` e `statusReport.ts`: o formato, a opção desabilitada e os erros. `web/src/components/SegmentedControl.tsx`: a opção desabilitada.
- Textos em `pt-BR.json`, o contrato da API (`npm run api:update`), `CLAUDE.md`, `docs/glossario.md` e o roadmap.
- ADRs: 0030, 0031, 0062, 0065, 0120, 0165.
