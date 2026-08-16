import { useState } from 'react';
import { AlertOctagon, Archive, ArchiveRestore, Pencil, Plus } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useT } from '@/i18n/I18nProvider';
import {
  addLine, addPost, addSite, addZone,
  archiveLine, archivePost, archiveSite, archiveZone,
  renameLine, renamePost, renameSite, renameZone,
  setPostType, togglePostCritical, useHierarchyState,
  type PostItem, type PostType,
} from '@/oas/hierarchyStore';

/** Small inline "add child" form, reused at every hierarchy level. */
function AddRow({ placeholder, onAdd }: { placeholder: string; onAdd: (name: string) => void }) {
  const [value, setValue] = useState('');
  return (
    <form
      className="flex items-center gap-1.5"
      onSubmit={(e) => {
        e.preventDefault();
        if (!value.trim()) return;
        onAdd(value);
        setValue('');
      }}
    >
      <Input value={value} onChange={(e) => setValue(e.target.value)} placeholder={placeholder} className="h-8 text-body-sm" />
      <Button type="submit" size="icon" variant="outline" className="h-8 w-8 shrink-0">
        <Plus className="h-3.5 w-3.5" />
      </Button>
    </form>
  );
}

/** Rename-in-place + archive/restore controls shared by every row. */
function EntityRow({
  code, name, archived, critical, onRename, onArchive, onToggleCritical,
}: {
  code: string; name: string; archived: boolean; critical?: boolean;
  onRename: (name: string) => void; onArchive: () => void; onToggleCritical?: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(name);
  const t = useT();
  return (
    <div className={`flex items-center gap-2 rounded-md border border-border px-2 py-1.5 ${archived ? 'opacity-50' : ''}`}>
      <Badge variant="ghost" className="font-mono shrink-0">{code}</Badge>
      {editing ? (
        <form
          className="flex flex-1 items-center gap-1.5"
          onSubmit={(e) => { e.preventDefault(); onRename(draft); setEditing(false); }}
        >
          <Input value={draft} onChange={(e) => setDraft(e.target.value)} className="h-7 flex-1 text-body-sm" autoFocus />
          <Button type="submit" size="sm" variant="outline" className="h-7">{t('common.validate')}</Button>
        </form>
      ) : (
        <span className="flex-1 truncate text-body-sm">{name}</span>
      )}
      {onToggleCritical && (
        <button type="button" onClick={onToggleCritical} title={t('web.ref.criticalToggle')} className="shrink-0">
          <Badge variant={critical ? 'destructive' : 'ghost'} className="font-mono">
            {t('web.ref.critical')}
            {critical && <AlertOctagon className="ms-1 h-3 w-3" />}
          </Badge>
        </button>
      )}
      {!editing && (
        <Button size="icon" variant="ghost" className="h-7 w-7 shrink-0" onClick={() => { setDraft(name); setEditing(true); }}>
          <Pencil className="h-3.5 w-3.5" />
        </Button>
      )}
      <Button size="icon" variant="ghost" className="h-7 w-7 shrink-0" onClick={onArchive} title={archived ? t('web.ref.restore') : t('web.ref.archive')}>
        {archived ? <ArchiveRestore className="h-3.5 w-3.5" /> : <Archive className="h-3.5 w-3.5" />}
      </Button>
    </div>
  );
}

const POST_TYPES: PostType[] = ['production', 'assembly', 'control', 'packaging'];

/** BL-001 — a post row plus its nature; capacity is a server-derived stat, shown read-only elsewhere. */
function PostRow({ post }: { post: PostItem }) {
  const t = useT();
  return (
    <div className="space-y-1">
      <EntityRow
        code={post.code}
        name={post.name}
        archived={post.archived}
        critical={post.isCritical}
        onRename={(name) => void renamePost(post.id, name)}
        onArchive={() => void archivePost(post.id)}
        onToggleCritical={() => void togglePostCritical(post.id)}
      />
      <div className="flex flex-wrap items-center gap-2 ps-2 text-caption text-muted-foreground">
        <label className="flex items-center gap-1.5">
          {t('web.ref.postType')}
          <select
            value={post.type}
            onChange={(e) => void setPostType(post.id, e.target.value as PostType)}
            className="h-7 rounded-md border border-border bg-background px-1.5 text-body-sm text-foreground"
          >
            {POST_TYPES.map((type) => (
              <option key={type} value={type}>{t(`web.ref.postType.${type}`)}</option>
            ))}
          </select>
        </label>
      </div>
    </div>
  );
}

/** BL-001 — full Site → Zone → Line → Post CRUD, backed by the real hierarchy API. */
export function HierarchyManager() {
  const t = useT();
  const hierarchy = useHierarchyState();

  return (
    <div data-demo="hierarchy" className="space-y-3">
      {hierarchy.sites.map((site) => (
        <div key={site.id} className="space-y-2 rounded-md border border-border p-2.5">
          <EntityRow
            code={site.code}
            name={site.name}
            archived={site.archived}
            onRename={(name) => void renameSite(site.id, name)}
            onArchive={() => void archiveSite(site.id)}
          />

          <div className="ms-1 space-y-2 border-s border-border ps-2 sm:ms-3 sm:ps-3">
            {hierarchy.zones.filter((z) => z.siteId === site.id).map((zone) => (
              <div key={zone.id} className="space-y-2">
                <EntityRow
                  code={zone.code}
                  name={zone.name}
                  archived={zone.archived}
                  onRename={(name) => void renameZone(zone.id, name)}
                  onArchive={() => void archiveZone(zone.id)}
                />
                <div className="ms-1 space-y-2 border-s border-border ps-2 sm:ms-3 sm:ps-3">
                  {hierarchy.lines.filter((l) => l.zoneId === zone.id).map((line) => (
                    <div key={line.id} className="space-y-1.5">
                      <EntityRow
                        code={line.code}
                        name={line.name}
                        archived={line.archived}
                        onRename={(name) => void renameLine(line.id, name)}
                        onArchive={() => void archiveLine(line.id)}
                      />
                      <div className="ms-1 space-y-1.5 border-s border-border ps-2 sm:ms-3 sm:ps-3">
                        {hierarchy.posts.filter((p) => p.lineId === line.id).map((post) => (
                          <PostRow key={post.id} post={post} />
                        ))}
                        <AddRow placeholder={t('web.ref.addPost')} onAdd={(name) => void addPost(line.id, name)} />
                      </div>
                    </div>
                  ))}
                  <AddRow placeholder={t('web.ref.addLine')} onAdd={(name) => void addLine(zone.id, name)} />
                </div>
              </div>
            ))}
            <AddRow placeholder={t('web.ref.addZone')} onAdd={(name) => void addZone(site.id, name)} />
          </div>
        </div>
      ))}
      <AddRow placeholder={t('web.ref.addSite')} onAdd={(name) => void addSite(name)} />
    </div>
  );
}
