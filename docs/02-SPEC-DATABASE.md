# OAS — Schéma PostgreSQL

> Modèle de données complet pour le Digital Production Assistant.
> Multi-tenant, offline-tolerant, auditable IATF 16949.
> Conventions : `snake_case`, PK `uuid` (`gen_random_uuid()`), `timestamptz` partout (UTC), soft-delete via `archived_at`.

---

## 0. Principes du modèle

| # | Règle | Conséquence |
|---|---|---|
| 1 | **Multi-tenant** | chaque table métier porte `tenant_id`, isolée par RLS |
| 2 | **Append-only pour les déclarations** | on ne modifie jamais une déclaration : on en crée une correction liée (`corrects_id`) → conformité audit |
| 3 | **Double horodatage** | `occurred_at` (saisie, éventuellement hors-ligne) + `received_at` (arrivée serveur) |
| 4 | **Idempotence** | `client_event_id UUID UNIQUE` sur toute table écrite par le mobile |
| 5 | **L'état du poste est dérivé** | jamais écrit par le client ; recalculé à partir des événements ouverts |
| 6 | **Aucune donnée de géolocalisation** | décision D1-10 — aucune colonne lat/lon nulle part |
| 7 | **Rôles hors table utilisateur** | table `user_roles` séparée (anti-élévation de privilèges) |
| 8 | **Rétention 2 ans minimum** | partitionnement mensuel des tables d'événements à fort volume |

---

## 1. Types énumérés

```sql
create type app_role as enum (
  'operator','team_lead','maintenance','quality',
  'prod_manager','director','process_engineer','admin'
);

create type post_state as enum (
  'production','material_wait','changeover',
  'technical_stop','quality_stop','unassigned'
);

create type event_type as enum (
  'technical_stop','quality_stop','material_wait','changeover','other_stop'
);

create type event_status as enum (
  'declared','notified','acknowledged','on_site',
  'resolved','closed','cancelled'
);

create type closure_type as enum ('resolved','palliative','no_fault','cancelled');

create type escalation_level as enum ('none','level_1','level_2');

create type declaration_kind as enum ('production','scrap','rework');

create type shift_code as enum ('morning','afternoon','night','custom');

create type criticality as enum ('low','medium','high','critical');
```

---

## 2. Socle multi-tenant & identité

### `tenants`
| colonne | type | notes |
|---|---|---|
| id | uuid PK | |
| name | text not null | |
| slug | text unique not null | |
| locale_default | text default 'fr' | 'fr' \| 'ar' |
| timezone | text default 'Africa/Tunis' | |
| settings | jsonb default '{}' | SLA, seuils, options TRS |
| created_at | timestamptz default now() | |

### `profiles`
Miroir applicatif de `auth.users`.
| colonne | type | notes |
|---|---|---|
| id | uuid PK | = auth.users.id |
| tenant_id | uuid → tenants | |
| full_name | text | |
| employee_code | text | matricule, unique par tenant |
| phone | text | |
| locale | text default 'fr' | |
| pin_hash | text | repli tablette partagée |
| biometric_enrolled | boolean default false | |
| is_active | boolean default true | |
| created_at, updated_at | timestamptz | |

`unique (tenant_id, employee_code)`

### `user_roles`
**Table séparée obligatoire.**
| colonne | type |
|---|---|
| id | uuid PK |
| user_id | uuid → profiles on delete cascade |
| tenant_id | uuid → tenants |
| role | app_role |
| scope_site_id | uuid null → sites |
| scope_zone_id | uuid null → zones |

`unique (user_id, role, scope_site_id, scope_zone_id)`

Fonction `has_role(_user_id uuid, _role app_role) returns boolean` en `security definer`, utilisée par toutes les policies.

---

## 3. Référentiel hiérarchique

```
tenant → site → zone → line → post → equipment
```

### `sites`
`id, tenant_id, code, name, timezone, address, archived_at` — `unique (tenant_id, code)`

### `zones`
`id, tenant_id, site_id, code, name, sort_order, archived_at`

### `lines`
`id, tenant_id, zone_id, code, name, sort_order, target_oee numeric(5,2), archived_at`

### `posts`
| colonne | type | notes |
|---|---|---|
| id | uuid PK | |
| tenant_id | uuid | |
| line_id | uuid → lines | |
| code | text | « A1 », « B4 » |
| name | text | |
| qr_token | text unique not null | jeton imprimé sur l'étiquette QR |
| qr_rotated_at | timestamptz | |
| sort_order | int | |
| is_active | boolean default true | |
| archived_at | timestamptz | |

`unique (tenant_id, code)` · index sur `qr_token`

### `equipments`
`id, tenant_id, post_id, code, name, serial_number, manufacturer, commissioned_at, criticality, archived_at`
→ support MTBF / MTTR.

---

## 4. Produits, gammes, ordres de fabrication

### `products`
`id, tenant_id, reference, name, customer, unit, archived_at` — `unique (tenant_id, reference)`

### `routings` (gammes)
| colonne | type | notes |
|---|---|---|
| id, tenant_id | | |
| product_id | uuid → products | |
| post_id | uuid → posts | |
| cycle_time_sec | numeric(10,3) **nullable** | **null ⇒ mode TRS-lite** |
| theoretical_rate_per_hour | numeric(10,2) nullable | |
| changeover_target_min | int nullable | SMED cible |
| operators_required | int default 1 | |

`unique (tenant_id, product_id, post_id)`

### `production_orders` (OF)
| colonne | type |
|---|---|
| id, tenant_id | |
| order_number | text, `unique (tenant_id, order_number)` |
| product_id | uuid → products |
| line_id | uuid → lines |
| quantity_planned | numeric(12,2) |
| quantity_produced | numeric(12,2) default 0 (dérivé) |
| due_date | date |
| status | text ('planned','in_progress','done','cancelled') |
| priority | int default 0 |

---

## 5. Équipes, shifts, affectations, sessions

### `shift_templates`
`id, tenant_id, site_id, code shift_code, name, start_time time, end_time time, crosses_midnight boolean, break_minutes int`

### `shift_calendar`
`id, tenant_id, site_id, shift_template_id, work_date date, is_working_day boolean`
`unique (tenant_id, site_id, shift_template_id, work_date)`

### `teams`
`id, tenant_id, site_id, code, name, lead_user_id uuid → profiles`

### `team_members`
`id, tenant_id, team_id, user_id, valid_from date, valid_to date null`

### `assignments`
Affectation d'un opérateur à un poste pour un shift donné (fait par le chef d'équipe).
| colonne | type |
|---|---|
| id, tenant_id | |
| work_date | date |
| shift_template_id | uuid |
| post_id | uuid → posts |
| user_id | uuid → profiles |
| production_order_id | uuid null → production_orders |
| assigned_by | uuid → profiles |
| created_at | timestamptz |

`unique (tenant_id, work_date, shift_template_id, post_id, user_id)`
Index : `(tenant_id, user_id, work_date)`, `(tenant_id, post_id, work_date)`

### `post_sessions`
Prise de poste réelle (scan QR → fin de poste).
| colonne | type | notes |
|---|---|---|
| id, tenant_id | | |
| client_event_id | uuid unique | idempotence |
| post_id, user_id | uuid | |
| assignment_id | uuid null | |
| production_order_id | uuid null | |
| started_at | timestamptz | `occurred_at` client |
| ended_at | timestamptz null | |
| started_via | text | 'qr' \| 'manual' \| 'biometric' |
| received_at | timestamptz default now() | |

Contrainte : une seule session ouverte par `(post_id)` et par `(user_id)`
→ `create unique index on post_sessions (tenant_id, post_id) where ended_at is null;`

---

## 6. Déclarations (append-only)

### `declarations`
Production, rebut, retouche.
| colonne | type | notes |
|---|---|---|
| id | uuid PK | |
| tenant_id | uuid | |
| client_event_id | uuid **unique not null** | idempotence offline |
| kind | declaration_kind | |
| post_session_id | uuid → post_sessions | |
| post_id, user_id | uuid | dénormalisé pour requêtes rapides |
| production_order_id | uuid null | |
| product_id | uuid null | |
| quantity_ok | numeric(12,2) default 0 | |
| quantity_nok | numeric(12,2) default 0 | |
| scrap_cause_id | uuid null → causes | |
| photo_path | text null | storage |
| note | text null | |
| occurred_at | timestamptz not null | **horodatage à la saisie** |
| received_at | timestamptz default now() | |
| corrects_id | uuid null → declarations | correction d'une déclaration |
| is_corrected | boolean default false | posé sur l'original |
| created_by | uuid | |

Index : `(tenant_id, post_id, occurred_at desc)`, `(tenant_id, production_order_id)`, `(tenant_id, kind, occurred_at)`
Partition possible par mois sur `occurred_at`.

> **Jamais d'UPDATE sur les quantités.** Une correction insère une nouvelle ligne avec `corrects_id` et marque l'originale `is_corrected = true`. L'UI affiche « saisie corrigée ».

---

## 7. Causes & règles de routage

### `causes`
Arbre de causes (motifs d'arrêt, causes de rebut, causes racines).
| colonne | type |
|---|---|
| id, tenant_id | |
| parent_id | uuid null → causes |
| domain | text ('stop','scrap','root_cause') |
| code, label_fr, label_ar | text |
| icon | text (nom lucide) |
| event_type | event_type null (pour domain='stop') |
| default_criticality | criticality |
| sort_order | int |
| is_active | boolean |

`unique (tenant_id, domain, code)`

### `routing_rules`
Qui est notifié pour quel motif, dans quelle zone, avec quel SLA.
| colonne | type |
|---|---|
| id, tenant_id | |
| event_type | event_type |
| cause_id | uuid null |
| zone_id | uuid null (null = toutes) |
| line_id | uuid null |
| target_role | app_role |
| target_team_id | uuid null |
| sla_minutes | int |
| escalate_1_after_min | int |
| escalate_1_role | app_role |
| escalate_2_after_min | int |
| escalate_2_role | app_role |
| priority | int |
| is_active | boolean |

Défauts : technique 10 min · qualité 5 min · matière 15 min.

---

## 8. Moteur d'événements

### `events`
| colonne | type | notes |
|---|---|---|
| id | uuid PK | |
| tenant_id | uuid | |
| client_event_id | uuid unique not null | |
| event_type | event_type | |
| status | event_status default 'declared' | |
| post_id, line_id, zone_id, site_id | uuid | dénormalisé |
| post_session_id | uuid null | |
| production_order_id, product_id | uuid null | |
| equipment_id | uuid null | |
| cause_id | uuid null → causes | motif déclaré |
| root_cause_id | uuid null → causes | renseigné à la clôture |
| criticality | criticality | |
| declared_by | uuid | |
| declared_at | timestamptz not null | = occurred_at |
| notified_at | timestamptz null | |
| acknowledged_at | timestamptz null | |
| acknowledged_by | uuid null | |
| eta_minutes | int null | |
| on_site_at | timestamptz null | scan QR d'arrivée |
| resolved_at | timestamptz null | |
| resolved_by | uuid null | |
| closure_type | closure_type null | |
| closed_at | timestamptz null | |
| closed_by | uuid null | |
| cancelled_at | timestamptz null | |
| sla_minutes | int | figé à la déclaration |
| sla_due_at | timestamptz | |
| sla_breached | boolean default false | |
| escalation_level | escalation_level default 'none' | |
| duration_sec | int null | généré à la clôture |
| response_sec | int null | declared → on_site |
| repair_sec | int null | on_site → resolved |
| note | text | |
| received_at | timestamptz default now() | |

Index : `(tenant_id, status) where status not in ('closed','cancelled')`, `(tenant_id, post_id, declared_at desc)`, `(tenant_id, event_type, declared_at)`, `(tenant_id, sla_due_at) where sla_breached = false`

### `event_transitions`
Journal immuable de chaque changement d'état (piste d'audit du moteur).
`id, tenant_id, event_id, from_status, to_status, actor_id, actor_role, payload jsonb, occurred_at, received_at`

### `event_notifications`
`id, tenant_id, event_id, recipient_user_id, recipient_role, channel ('push','sms','in_app'), sent_at, delivered_at, read_at, responded_at, response ('coming','busy'), escalation_level`

---

## 9. Changements de série

### `changeovers`
| colonne | type |
|---|---|
| id, tenant_id, client_event_id | |
| post_id | uuid |
| from_product_id | uuid null |
| to_product_id | uuid |
| production_order_id | uuid null |
| started_at | timestamptz |
| first_good_part_at | timestamptz null |
| ended_at | timestamptz null |
| duration_sec | int null (généré) |
| target_min | int null (depuis routings) |
| started_by, validated_by | uuid |
| event_id | uuid null → events |

---

## 10. Contrôle qualité

### `quality_checks`
`id, tenant_id, client_event_id, post_id, production_order_id, product_id, check_type ('first_part','in_process','final'), result ('ok','rework','scrap'), quantity_checked, quantity_rejected, cause_id, inspector_id, photo_path, occurred_at, received_at, event_id`

Le contrôle 1ère pièce est obligatoire après un `changeover` — contrainte applicative + vérifiée en reporting.

---

## 11. État dérivé & temps réel

### `post_states`
Une ligne par poste : état courant, mis à jour par trigger sur `events` / `post_sessions` / `changeovers`.
| colonne | type |
|---|---|
| post_id | uuid PK → posts |
| tenant_id | uuid |
| state | post_state not null default 'unassigned' |
| since | timestamptz not null |
| active_event_id | uuid null |
| active_session_id | uuid null |
| current_user_id | uuid null |
| current_product_id | uuid null |
| current_order_id | uuid null |
| updated_at | timestamptz |

Priorité de dérivation : `technical_stop > quality_stop > material_wait > changeover > production > unassigned`.
→ Alimente directement `GET /shopfloor/map` et l'Andon.

### `post_state_history`
`id, tenant_id, post_id, state, started_at, ended_at, duration_sec, event_id, session_id`
→ base de calcul de la disponibilité (TRS) sans rejouer tous les événements.

---

## 12. KPI

### `kpi_daily` (table d'agrégat, rafraîchie par job)
| colonne | type |
|---|---|
| id, tenant_id | |
| scope_type | text ('post','line','zone','site') |
| scope_id | uuid |
| work_date | date |
| shift_template_id | uuid null |
| planned_time_sec, run_time_sec, downtime_sec, changeover_sec | int |
| qty_ok, qty_nok, qty_scrap | numeric(12,2) |
| theoretical_qty | numeric(12,2) null |
| availability, performance, quality, oee | numeric(5,2) null |
| oee_mode | text ('full','lite') |
| stops_count, mtbf_sec, mttr_sec | |

`unique (tenant_id, scope_type, scope_id, work_date, shift_template_id)`

**TRS-lite** : si `theoretical_qty is null` (pas de cadence en gamme), `performance` est `null`, `oee = availability × quality` et `oee_mode = 'lite'`. Le frontend affiche le badge correspondant.

Vues utiles : `v_open_events`, `v_shopfloor_map`, `v_pareto_causes`, `v_equipment_reliability`.

---

## 13. Audit, offline, notifications

### `audit_log`
`id, tenant_id, entity_table, entity_id, action ('insert','update','delete','correct'), actor_id, actor_role, before jsonb, after jsonb, reason text, occurred_at, ip inet, user_agent text`
Rempli par trigger générique sur toutes les tables sensibles. **Immuable** : aucune policy UPDATE/DELETE.

### `sync_receipts`
Traçabilité du rejeu hors-ligne (diagnostic, pas source de vérité).
`id, tenant_id, client_event_id, device_id, entity, occurred_at, received_at, latency_sec (généré), attempts, status`

### `device_tokens`
`id, tenant_id, user_id, platform ('android','ios','web'), token, app_version, os_version, last_seen_at`

### `attachments`
`id, tenant_id, bucket, path, mime_type, size_bytes, entity_table, entity_id, uploaded_by, created_at`

### `imports`
Traçage des imports Excel du référentiel (P7).
`id, tenant_id, kind, file_path, rows_total, rows_ok, rows_error, report jsonb, imported_by, created_at`

---

## 14. Sécurité (RLS)

Chaque table publique suit **exactement** cet ordre dans la migration :

```sql
create table public.<t> (...);

grant select, insert, update, delete on public.<t> to authenticated;
grant all on public.<t> to service_role;
-- pas de grant anon : aucune donnée n'est publique

alter table public.<t> enable row level security;

create policy "tenant_read" on public.<t> for select to authenticated
  using (tenant_id = public.current_tenant_id());
```

Fonctions `security definer` requises :
- `public.current_tenant_id() returns uuid` — lit le tenant du profil courant.
- `public.has_role(_user_id uuid, _role app_role) returns boolean`.
- `public.can_access_scope(_user_id uuid, _site_id uuid, _zone_id uuid) returns boolean`.

Règles de policy notables :
| Table | Lecture | Écriture |
|---|---|---|
| `declarations` | tenant + scope | insert par l'auteur de la session ; **aucun update/delete** |
| `events` | tenant + scope | insert opérateur ; update restreint aux transitions autorisées par rôle |
| `audit_log` | admin / director | insert par trigger uniquement ; jamais update/delete |
| `kpi_daily` | tous rôles **sauf** `admin` RH (P8) | service_role uniquement |
| `profiles` | soi-même + team_lead + admin | soi-même (champs limités) |
| `user_roles` | admin | admin |

> **P8 (admin RH) n'a aucun accès aux tables KPI ni aux déclarations nominatives** — exigence explicite du client.

---

## 15. Triggers & fonctions

| Nom | Déclencheur | Rôle |
|---|---|---|
| `trg_set_updated_at` | before update | horodatage |
| `trg_derive_post_state` | after insert/update sur `events`, `post_sessions`, `changeovers` | recalcule `post_states` + clôture la ligne courante de `post_state_history` |
| `trg_event_sla` | before insert sur `events` | résout `routing_rules` → `sla_minutes`, `sla_due_at`, `criticality` |
| `trg_event_transition` | after update sur `events.status` | insère dans `event_transitions` |
| `trg_event_durations` | before update | calcule `response_sec`, `repair_sec`, `duration_sec` |
| `trg_audit` | after insert/update/delete | remplit `audit_log` |
| `trg_declaration_immutable` | before update/delete sur `declarations` | lève une exception (append-only) |
| `trg_order_progress` | after insert sur `declarations` | met à jour `production_orders.quantity_produced` |
| `job_sla_escalation` | cron 1 min | passe `sla_breached`, incrémente `escalation_level`, crée les notifications |
| `job_kpi_daily` | cron 15 min + nuit | recalcule `kpi_daily` |

---

## 16. Ordre de création des migrations

1. extensions (`pgcrypto`), enums, fonctions `security definer`
2. `tenants`, `profiles`, `user_roles`
3. `sites`, `zones`, `lines`, `posts`, `equipments`
4. `products`, `routings`, `production_orders`
5. `shift_templates`, `shift_calendar`, `teams`, `team_members`
6. `causes`, `routing_rules`
7. `assignments`, `post_sessions`
8. `declarations`, `events`, `event_transitions`, `event_notifications`
9. `changeovers`, `quality_checks`
10. `post_states`, `post_state_history`
11. `kpi_daily` + vues
12. `audit_log`, `sync_receipts`, `device_tokens`, `attachments`, `imports`
13. triggers, jobs, seeds (causes par défaut FR/AR, shifts 3×8, règles de routage)

---

## 17. Jeu de données de démonstration

1 tenant · 1 site · 2 zones · 2 lignes (Assemblage A, Injection B) · 11 postes (A1–A6, B1–B6 dont B5 non affecté) · 3 produits (REF-4021, REF-5510, REF-773) · 3 shifts · 12 opérateurs · 2 chefs · 2 mainteneurs · 1 qualité · ~20 causes bilingues · 3 événements ouverts (technique A4, matière A5/B6, qualité A6) · 1 changement de série en cours (B2) · 7 jours d'historique pour alimenter TRS, Pareto et MTBF.
