/** Shared CSV export helper — used by reports, shift report and the audit trail. */
export function csvExport(rows: (string | number)[][], name: string) {
  const csv = rows.map((r) => r.map((c) => String(c).replace(/;/g, ',')).join(';')).join('\n');
  const url = URL.createObjectURL(new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
}
