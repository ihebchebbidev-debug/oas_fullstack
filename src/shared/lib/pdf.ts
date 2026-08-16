/**
 * BL-043 / BL-044 — real downloadable PDF export (no more window.print()).
 *
 * Kept deliberately small: a title block, key/value summary rows and simple
 * tables. Everything is drawn with jsPDF primitives so the file downloads on
 * mobile (Android WebView) as well as on the console.
 */

import { jsPDF } from 'jspdf';

export interface PdfKeyValue { label: string; value: string }
export interface PdfTable { title: string; head: string[]; rows: (string | number)[][] }

export interface PdfDoc {
  title: string;
  subtitle?: string;
  summary?: PdfKeyValue[];
  tables?: PdfTable[];
  footer?: string;
  fileName: string;
}

const M = 14;

/** Latin transliteration guard: jsPDF core fonts are Latin-1 only. */
const safe = (s: string | number) =>
  String(s)
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^\x20-\x7E]/g, '');

export function exportPdf(doc: PdfDoc) {
  const pdf = new jsPDF({ unit: 'mm', format: 'a4' });
  const width = pdf.internal.pageSize.getWidth();
  const height = pdf.internal.pageSize.getHeight();
  let y = M;

  const nextPage = (need = 10) => {
    if (y + need > height - M) {
      pdf.addPage();
      y = M;
    }
  };

  pdf.setFont('helvetica', 'bold');
  pdf.setFontSize(16);
  pdf.text(safe(doc.title), M, y);
  y += 7;

  if (doc.subtitle) {
    pdf.setFont('helvetica', 'normal');
    pdf.setFontSize(10);
    pdf.setTextColor(110);
    pdf.text(safe(doc.subtitle), M, y);
    pdf.setTextColor(0);
    y += 6;
  }

  pdf.setDrawColor(200);
  pdf.line(M, y, width - M, y);
  y += 7;

  if (doc.summary?.length) {
    pdf.setFontSize(10);
    doc.summary.forEach((kv) => {
      nextPage();
      pdf.setFont('helvetica', 'normal');
      pdf.text(safe(kv.label), M, y);
      pdf.setFont('helvetica', 'bold');
      pdf.text(safe(kv.value), width - M, y, { align: 'right' });
      y += 5.5;
    });
    y += 4;
  }

  doc.tables?.forEach((table) => {
    if (!table.rows.length) return;
    nextPage(18);
    pdf.setFont('helvetica', 'bold');
    pdf.setFontSize(11);
    pdf.text(safe(table.title), M, y);
    y += 5;

    const cols = table.head.length;
    const colW = (width - 2 * M) / cols;
    const drawRow = (cells: (string | number)[], bold: boolean) => {
      pdf.setFont('helvetica', bold ? 'bold' : 'normal');
      pdf.setFontSize(8.5);
      cells.forEach((c, i) => {
        const text = pdf.splitTextToSize(safe(c), colW - 2)[0] ?? '';
        pdf.text(text, M + i * colW, y);
      });
      y += 4.6;
    };

    drawRow(table.head, true);
    pdf.setDrawColor(220);
    pdf.line(M, y - 3.2, width - M, y - 3.2);
    table.rows.forEach((r) => {
      nextPage();
      drawRow(r, false);
    });
    y += 6;
  });

  const stamp = doc.footer ?? new Date().toLocaleString();
  pdf.setFont('helvetica', 'normal');
  pdf.setFontSize(8);
  pdf.setTextColor(130);
  pdf.text(safe(stamp), M, height - 8);

  pdf.save(doc.fileName);
}
