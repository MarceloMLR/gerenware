## 1. Roadmap

- [x] 1.1 Status-5 "Em andamento" no `docs/roadmap.md`

## 2. A conversão no armazenamento

- [x] 2.1 O `IFileConversion`, o `FileConversion.MaxBytes` e a `FileConversionException` no `Files.Contracts`, com o `NoFileConversion` para a implementação local e o registro dos dois contratos no `FilesModule` (D1)
- [x] 2.2 A conversão pelo Graph no `SharePointFileStorage` (D5) e, no `FakeSharePoint`, o `format=pdf` e o `ConversionFailure` (D9). Testes dos cenários de "Conversão em PDF"

## 3. O PDF nos documentos

- [ ] 3.1 O `SavePdfAsync` no `IStatusReportDocuments` e no `StatusReportDocuments`, com a pasta dos relatórios num método só para os dois formatos (D2, D4). Testes do módulo:
  - o PDF guardado e a nova versão no mesmo dia;
  - o provisório removido no sucesso e na falha;
  - nada guardado quando a conversão falha.

## 4. O formato no relatório

- [ ] 4.1 O `StatusReportFormat` no domínio, o `Format` do `StatusReportNotes` e o `StatusReportField.Format`, com os testes do domínio (D3)
- [ ] 4.2 A coluna `format` em `projects.status_report_notes` (migração do `Projects`)
- [ ] 4.3 O formato no `GetStatusReportDraft` (`format`, `pdfAvailable`) e no `GenerateStatusReport`, com o 409 antes de guardar e o 502 sem gravar os textos (D4), e o `FileNameOf` com a extensão
- [ ] 4.4 As rotas com o `format` no rascunho e no pedido e o 409 e o 502 em Problem Details (D6), com o contrato da API atualizado (`npm run api:update`). Testes da API dos cenários de "Relatório em PDF", "Limite e falha da conversão" e "Relatório guardado nos documentos", com o `FakeSharePoint`

## 5. A tela

- [ ] 5.1 A opção desabilitada no `SegmentedControl` (D7), com o teste do componente
- [ ] 5.2 "Formato" no `StatusReportDialog`, o motivo sem o PDF, os erros 409 e 502 e os textos em `pt-BR.json` (D7). Testes de tela dos cenários de "Gerar o relatório pela tela"
- [ ] 5.3 Telas conferidas nos temas claro e escuro e no celular; `npm run lint`, `npm run typecheck` e `npm test` limpos

## 6. Documentação e verificação

- [ ] 6.1 Documentação e specs:
  - `CLAUDE.md`, nas seções do mapa e da tela;
  - `docs/glossario.md`;
  - `openspec validate --all --strict` limpo.
- [ ] 6.2 Verificação: `dotnet build -warnaserror`, `dotnet test`, `dotnet format` e os testes do frontend

## Workflow follow-up

- Antes da aprovação do PR, Marcelo roda o teste da conversão no `dev` (D8); o resultado vai para o design e para a descrição do PR. Se o Graph recusar, a change para, e um ADR novo da página de impressão (P3) substitui o ADR-0165.
- PR com a aprovação de Marcelo, archive na mesma branch antes do merge, roadmap "Concluída" e o merge por Marcelo.
- Depois do merge, a aparência do PDF é conferida no `dev`; o que precisar de ajuste vira a Status-5.1.
