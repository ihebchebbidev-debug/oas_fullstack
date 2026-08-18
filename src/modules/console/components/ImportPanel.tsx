import { useRef, useState } from 'react';
import * as XLSX from 'xlsx';
import { Upload, FileSpreadsheet, CheckCircle2, AlertTriangle, X } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { applyImport } from '@/oas/refStore';
import { useT } from '@/i18n/I18nProvider';

/** BL-010 — Excel/CSV import with real parsing + validation report. */

type Row = Record<string, string>;

/**
 * Only `products`/`causes`/`equipment` are real, importable datasets — the
 * server's commit step only knows how to write those three tables
 * (`OasImportService.SupportedKinds`). `required`/`optional` are the exact
 * field names the server reads off each row (spec's `CommitProductRow` etc.),
 * matched fuzzily against the file's own headers via `pick()` below, then
 * re-emitted with these exact keys before the row is sent.
 */
interface Dataset {
  key: 'products' | 'causes' | 'equipment';
  required: string[];
  optional: string[];
  keyColumn: string;
}

const DATASETS: Dataset[] = [
  { key: 'products', required: ['reference', 'name'], optional: ['customer', 'unit'], keyColumn: 'reference' },
  { key: 'causes', required: ['domain', 'code', 'labelFr'], optional: ['labelAr'], keyColumn: 'code' },
  { key: 'equipment', required: ['code', 'name'], optional: ['serialNumber', 'manufacturer'], keyColumn: 'code' },
];

interface Report {
  fileName: string;
  sheet: string;
  dataset: Dataset | null;
  headers: string[];
  rows: Row[];
  errors: { row: number; message: string }[];
  duplicates: string[];
}

const norm = (s: string) => s.trim().toLowerCase().replace(/[\s_-]+/g, '');

function detect(headers: string[]): Dataset | null {
  const set = new Set(headers.map(norm));
  return (
    DATASETS.find((d) => d.required.every((c) => set.has(norm(c)))) ?? null
  );
}

function pick(row: Row, column: string): string {
  const found = Object.keys(row).find((k) => norm(k) === norm(column));
  return found ? String(row[found] ?? '').trim() : '';
}

/** Re-emits a row with the exact field names the server expects, regardless of the file's original header casing/spacing. */
function normalizeRow(dataset: Dataset, row: Row): Row {
  const out: Row = {};
  [...dataset.required, ...dataset.optional].forEach((col) => {
    const v = pick(row, col);
    if (v) out[col] = v;
  });
  return out;
}

function analyse(fileName: string, sheet: string, raw: Row[]): Report {
  const headers = raw.length ? Object.keys(raw[0]) : [];
  const dataset = detect(headers);
  const errors: Report['errors'] = [];
  const seen = new Map<string, number>();
  const duplicates: string[] = [];

  raw.forEach((row, i) => {
    if (!dataset) return;
    dataset.required.forEach((col) => {
      if (!pick(row, col)) errors.push({ row: i + 2, message: `${col} — missing` });
    });
    const id = pick(row, dataset.keyColumn);
    if (id) {
      if (seen.has(id)) duplicates.push(id);
      else seen.set(id, i);
    }
  });

  return { fileName, sheet, dataset, headers, rows: raw, errors, duplicates };
}

export default function ImportPanel() {
  const t = useT();
  const fileRef = useRef<HTMLInputElement>(null);
  /** BL-010 — one report per worksheet: a workbook can carry several datasets. */
  const [reports, setReports] = useState<Report[]>([]);
  const [fileName, setFileName] = useState<string | null>(null);
  const [imported, setImported] = useState<Record<string, number>>({});
  const [parseError, setParseError] = useState<string | null>(null);

  async function handleFile(file: File) {
    setImported({});
    setParseError(null);
    try {
      const buffer = await file.arrayBuffer();
      const wb = XLSX.read(buffer, { type: 'array' });
      const parsed = wb.SheetNames.map((name) => {
        const raw = XLSX.utils.sheet_to_json<Row>(wb.Sheets[name], { defval: '', raw: false });
        return analyse(file.name, name, raw);
      }).filter((r) => r.rows.length > 0);
      setFileName(file.name);
      setReports(parsed);
      if (!parsed.length) setParseError(t('web.admin.import.parseError'));
    } catch {
      setReports([]);
      setParseError(t('web.admin.import.parseError'));
    }
  }

  const reset = () => { setReports([]); setImported({}); setFileName(null); };
  // Duplicates-within-sheet were only ever shown as a warning badge, never
  // actually blocked — the backend has no per-row transaction (one
  // SaveChangesAsync for the whole batch), so a duplicate key column hits
  // the DB's unique constraint, throws uncaught, and rolls back every row
  // in the sheet including the genuinely valid ones, with no way to tell
  // afterwards why an import silently landed at "0 rows imported".
  const importable = reports.filter((r) => r.dataset && r.errors.length === 0 && r.duplicates.length === 0);

  async function runAll() {
    const done: Record<string, number> = { ...imported };
    for (const r of importable) {
      const dataset = r.dataset!;
      done[r.sheet] = await applyImport(dataset.key, r.rows.map((row) => normalizeRow(dataset, row)));
    }
    setImported(done);
  }


  return (
    <Card data-demo="import-panel">
      <CardHeader>
        <CardTitle>{t('web.admin.import.title')}</CardTitle>
        <CardDescription>{t('web.admin.import.desc')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <button
          type="button"
          onClick={() => fileRef.current?.click()}
          className="flex w-full flex-col items-center gap-2 rounded-lg border border-dashed border-border p-8 text-muted-foreground transition-colors hover:bg-accent"
        >
          <Upload className="h-6 w-6" />
          <span className="text-body-sm">{fileName ?? t('web.admin.import.drop')}</span>
        </button>
        <input
          ref={fileRef}
          type="file"
          accept=".xlsx,.xls,.csv"
          className="hidden"
          onChange={(e) => {
            const f = e.target.files?.[0];
            if (f) void handleFile(f);
            e.target.value = '';
          }}
        />

        {!reports.length && !parseError && (
          <ul className="space-y-1.5 text-body-sm text-muted-foreground">
            {DATASETS.map((d) => (
              <li key={d.key} className="flex items-center gap-2">
                <FileSpreadsheet className="h-3.5 w-3.5" /> {t(`web.admin.import.${d.key}`)}
                <span className="font-mono text-caption">({d.required.join(', ')})</span>
              </li>
            ))}
          </ul>
        )}

        {parseError && (
          <p className="flex items-center gap-2 text-body-sm text-destructive">
            <AlertTriangle className="h-4 w-4" /> {parseError}
          </p>
        )}

        {reports.length > 0 && (
          <div className="space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="ghost">{t('web.admin.import.sheets', { count: reports.length })}</Badge>
              <Button size="sm" variant="ghost" onClick={reset}>
                <X className="me-1.5 h-3.5 w-3.5" /> {t('common.cancel')}
              </Button>
            </div>

            {reports.map((report) => (
              <div key={report.sheet} className="space-y-3 rounded-md border border-border p-3">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant="ghost" className="font-mono">{report.sheet}</Badge>
                  <Badge variant="ghost">
                    {t('web.admin.import.rows', { count: report.rows.length })}
                  </Badge>
                  {report.dataset ? (
                    <Badge variant="ghost" className="text-state-production">
                      {t(`web.admin.import.${report.dataset.key}`)}
                    </Badge>
                  ) : (
                    <Badge variant="ghost" className="text-destructive">
                      {t('web.admin.import.unknown')}
                    </Badge>
                  )}
                </div>

                {report.duplicates.length > 0 && (
                  <p className="flex items-center gap-2 text-body-sm text-state-quality">
                    <AlertTriangle className="h-4 w-4" />
                    {t('web.admin.import.duplicates', { list: [...new Set(report.duplicates)].join(', ') })}
                  </p>
                )}

                {report.errors.length > 0 && (
                  <div className="rounded-md border border-destructive/40 bg-destructive/5 p-3">
                    <p className="mb-1.5 flex items-center gap-2 text-body-sm font-medium text-destructive">
                      <AlertTriangle className="h-4 w-4" />
                      {t('web.admin.import.errors', { count: report.errors.length })}
                    </p>
                    <ul className="space-y-0.5 text-caption text-muted-foreground">
                      {report.errors.slice(0, 8).map((e, i) => (
                        <li key={i} className="font-mono">
                          {t('web.admin.import.line', { n: e.row })} · {e.message}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}

                <div className="overflow-x-auto rounded-md border border-border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        {report.headers.map((h) => (
                          <TableHead key={h}>{h}</TableHead>
                        ))}
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {report.rows.slice(0, 5).map((r, i) => (
                        <TableRow key={i}>
                          {report.headers.map((h) => (
                            <TableCell key={h} className="font-mono text-caption">{String(r[h] ?? '')}</TableCell>
                          ))}
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>

                {imported[report.sheet] !== undefined && (
                  <p className="text-body-sm text-state-production">
                    {t('web.admin.import.done', { count: imported[report.sheet] })}
                  </p>
                )}
              </div>
            ))}

            <Button size="sm" disabled={!importable.length} onClick={() => void runAll()}>
              <CheckCircle2 className="me-1.5 h-3.5 w-3.5" />
              {t('web.admin.import.run')}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
