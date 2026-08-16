/**
 * Excel export — HTML table wrapped as an Excel workbook (no SheetJS
 * dependency, no backend). Opens natively in Excel/LibreOffice/Numbers.
 */
export function excelExport(rows: (string | number)[][], name: string, sheetName = 'Sheet1') {
  const escape = (v: string | number) =>
    String(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  const table = rows
    .map((r) => `<tr>${r.map((c) => `<td>${escape(c)}</td>`).join('')}</tr>`)
    .join('');
  const html = `<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">
<head><meta charset="utf-8" />
<!--[if gte mso 9]><xml><x:ExcelWorkbook><x:ExcelWorksheets><x:ExcelWorksheet><x:Name>${sheetName}</x:Name>
<x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions></x:ExcelWorksheet></x:ExcelWorksheets></x:ExcelWorkbook></xml><![endif]-->
</head><body><table>${table}</table></body></html>`;
  const url = URL.createObjectURL(new Blob([html], { type: 'application/vnd.ms-excel' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = /\.xlsx?$/.test(name) ? name : `${name}.xls`;
  a.click();
  URL.revokeObjectURL(url);
}
