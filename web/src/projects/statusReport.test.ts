import { describe, expect, it } from 'vitest';
import {
  failureKey,
  pdfProblems,
  risksAfter,
  statusReportSchema,
  ticketOptions,
  valuesOf,
} from './statusReport';

const ticket = (number: number) => `#${String(number)}`;

const tickets = [
  {
    number: 545,
    title: 'Dúvida sobre o relatório fiscal',
    status: 'Open' as const,
    threatens: false,
  },
  { number: 539, title: 'Rejeição na emissão', status: 'InProgress' as const, threatens: true },
];

// Spec: relatorio-de-status, requirements "Rascunho do relatório", "Relatório em PDF" and "Gerar o relatório pela
// tela" (ADR-0165).
describe('Rascunho do relatório na tela', () => {
  it('fills the form with the draft and never with the hours', () => {
    const values = valuesOf({
      last: null,
      summary: 'Cadastros validados',
      focus: '',
      nextSteps: '',
      clientNeeds: '',
      tickets,
      risks: [{ number: 539, level: 'High', attention: 'Depende do fornecedor' }],
      format: 'Pdf',
      pdfAvailable: true,
    });

    expect(values).toEqual({
      summary: 'Cadastros validados',
      focus: '',
      nextSteps: '',
      clientNeeds: '',
      risks: [{ number: 539, level: 'High', attention: 'Depende do fornecedor' }],
      format: 'Pdf',
      showHours: false,
    });
  });

  it('comes in PowerPoint when the PDF cannot be made', () => {
    const draft = {
      last: null,
      summary: '',
      focus: '',
      nextSteps: '',
      clientNeeds: '',
      tickets: [],
      risks: [],
      format: 'Pdf' as const,
      pdfAvailable: false,
    };

    expect(valuesOf(draft).format).toBe('PowerPoint');
  });

  it('tells the PDF too large and the conversion that failed apart', () => {
    expect(failureKey({ type: pdfProblems.tooLarge, status: 409 })).toBe(
      'projects.statusReport.tooLargeForPdf',
    );
    expect(failureKey({ type: pdfProblems.failed, status: 502 })).toBe(
      'projects.statusReport.pdfFailed',
    );
    expect(failureKey({ status: 500 })).toBe('projects.statusReport.failed');
    expect(failureKey(undefined)).toBe('projects.statusReport.failed');
  });

  it('lists the tickets that threaten milestones first', () => {
    expect(ticketOptions(tickets, { threatens: 'Ameaçam', others: 'Outros' })).toEqual([
      { value: '539', label: `${ticket(539)} · Rejeição na emissão`, group: 'Ameaçam' },
      { value: '545', label: `${ticket(545)} · Dúvida sobre o relatório fiscal`, group: 'Outros' },
    ]);
  });

  it('keeps the risks chosen and adds a new one as Medium', () => {
    const risks = [{ number: 539, level: 'High' as const, attention: 'Depende do fornecedor' }];

    expect(risksAfter(risks, ['539', '545'])).toEqual([
      ...risks,
      { number: 545, level: 'Medium', attention: '' },
    ]);
    expect(risksAfter(risks, ['545'])).toEqual([{ number: 545, level: 'Medium', attention: '' }]);
  });

  it('refuses a text over the limit and more than ten risks', () => {
    const values = {
      summary: '',
      focus: '',
      nextSteps: '',
      clientNeeds: '',
      risks: [],
      format: 'PowerPoint',
      showHours: false,
    };

    expect(statusReportSchema.safeParse({ ...values, summary: 'a'.repeat(1001) }).success).toBe(
      false,
    );
    expect(
      statusReportSchema.safeParse({
        ...values,
        risks: Array.from({ length: 11 }, (_, index) => ({
          number: index,
          level: 'Low',
          attention: '',
        })),
      }).success,
    ).toBe(false);
    expect(statusReportSchema.safeParse(values).success).toBe(true);
  });
});
