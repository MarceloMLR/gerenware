import { z } from 'zod';
import type {
  RiskLevel,
  StatusReportDraft,
  StatusReportFormat,
  StatusReportTicket,
} from '../api/client';
import type { MultiSelectOption } from '../components/MultiSelect';

/** The limits of the status report (ADR-0165), the same as the server's. */
export const statusReportLimits = { text: 1000, attention: 500, risks: 10 } as const;

/** The levels a risk may have, from the most serious. */
export const riskLevels: readonly RiskLevel[] = ['High', 'Medium', 'Low'];

/** The formats the report comes out in (ADR-0165): the PowerPoint, or its PDF, only with the files in SharePoint. */
export const statusReportFormats: readonly StatusReportFormat[] = ['PowerPoint', 'Pdf'];

/** The keys of the texts of the errors of the form, checked by the types. */
export const statusReportErrors = {
  textTooLong: 'projects.statusReport.errors.textTooLong',
  attentionTooLong: 'projects.statusReport.errors.attentionTooLong',
  risks: 'projects.statusReport.errors.risks',
} as const;

export type StatusReportError = (typeof statusReportErrors)[keyof typeof statusReportErrors];

export function isStatusReportError(message: string | undefined): message is StatusReportError {
  return Object.values<string>(statusReportErrors).includes(message ?? '');
}

const text = z.string().max(statusReportLimits.text, statusReportErrors.textTooLong);

/**
 * What the person writes and chooses: the four texts, the risks with their level and point of attention, the format
 * and the hours.
 */
export const statusReportSchema = z.object({
  summary: text,
  focus: text,
  nextSteps: text,
  clientNeeds: text,
  risks: z
    .array(
      z.object({
        number: z.number(),
        level: z.enum(['High', 'Medium', 'Low']),
        attention: z
          .string()
          .max(statusReportLimits.attention, statusReportErrors.attentionTooLong),
      }),
    )
    .max(statusReportLimits.risks, statusReportErrors.risks),
  format: z.enum(['PowerPoint', 'Pdf']),
  showHours: z.boolean(),
});

export type StatusReportValues = z.infer<typeof statusReportSchema>;

/**
 * The form filled by the draft: the texts of the last report, the risks suggested and its format, the PowerPoint
 * when the PDF cannot be made; the hours never (ADR-0166).
 */
export function valuesOf(draft: StatusReportDraft): StatusReportValues {
  return {
    summary: draft.summary,
    focus: draft.focus,
    nextSteps: draft.nextSteps,
    clientNeeds: draft.clientNeeds,
    risks: draft.risks.map((risk) => ({ ...risk })),
    format: draft.pdfAvailable ? draft.format : 'PowerPoint',
    showHours: false,
  };
}

/** The problem types of the PDF that the dialog tells apart (ADR-0165). */
export const pdfProblems = {
  tooLarge: 'urn:gerenware:problem:status-report-too-large-for-pdf',
  failed: 'urn:gerenware:problem:status-report-pdf-failed',
} as const;

/**
 * The key of the text of a generation that failed: the PowerPoint too large for the PDF (409), the conversion that
 * Microsoft 365 did not do (502), or anything else.
 */
export function failureKey(
  error: unknown,
):
  | 'projects.statusReport.tooLargeForPdf'
  | 'projects.statusReport.pdfFailed'
  | 'projects.statusReport.failed' {
  const type =
    typeof error === 'object' && error !== null && 'type' in error ? error.type : undefined;
  return type === pdfProblems.tooLarge
    ? 'projects.statusReport.tooLargeForPdf'
    : type === pdfProblems.failed
      ? 'projects.statusReport.pdfFailed'
      : 'projects.statusReport.failed';
}

/** The open tickets to choose as risks, the ones that threaten milestones first, each as "#" and the number, then the title. */
export function ticketOptions(
  tickets: readonly StatusReportTicket[],
  groups: { threatens: string; others: string },
): MultiSelectOption[] {
  return [...tickets]
    .sort((a, b) => Number(b.threatens) - Number(a.threatens) || a.number - b.number)
    .map((ticket) => ({
      value: String(ticket.number),
      label: ticketLabel(ticket),
      group: ticket.threatens ? groups.threatens : groups.others,
    }));
}

export function ticketLabel(ticket: Pick<StatusReportTicket, 'number' | 'title'>): string {
  return `#${String(ticket.number)} · ${ticket.title}`;
}

/**
 * The risks after the person changed the tickets chosen: the ones kept stay as they were, in their order, and a new one
 * comes with the level Medium and no point of attention.
 */
export function risksAfter(
  risks: StatusReportValues['risks'],
  chosen: readonly string[],
): StatusReportValues['risks'] {
  const numbers = chosen.map(Number);
  const kept = risks.filter((risk) => numbers.includes(risk.number));
  const added = numbers
    .filter((number) => !kept.some((risk) => risk.number === number))
    .map((number) => ({ number, level: 'Medium' as const, attention: '' }));
  return [...kept, ...added];
}
