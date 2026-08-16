# OAS — Digital Production Assistant
## Spécification Frontend complète (100 %)

> Document de référence unique pour la construction du frontend.
> Cible : Ionic React + Capacitor (mobile opérateur/technicien) + Web React (superviseur/manager/admin).
> Version : 1.0 — dérivée des documents client (charter, personas, backlog 49 stories, registre de décisions, stratégie de test, maquette v1.3).

---

## 0. TL;DR pour un développeur qui arrive

| Question | Réponse |
|---|---|
| Que fait l'app ? | L'opérateur scanne le QR de son poste, déclare production / arrêt / rebut en **< 5 s**. L'usine voit l'état en direct. |
| Qui l'utilise ? | 8 personas. 4 sur mobile (opérateur, chef d'équipe, maintenance, qualité), 4 sur web. |
| Contrainte n°1 | Android 8+, BYOD, **< 50 Mo**, offline-first, FR + AR **RTL dès le jour 1**. |
| Règle éthique | **Assistant, pas mouchard.** Pas de GPS continu, pas de score de discipline, pas de flux RH. |
| Couleur = ? | La couleur ne sert **qu'à** signifier l'état machine. 6 états, 6 couleurs. Rien d'autre n'est coloré. |
| Périmètre prototype | 12 stories « P » : socle data → parcours opérateur → plan atelier + affectation. |

---

## 1. Principes de conception (non négociables)

### PC-1 — Automatique par défaut
Le frontend ne demande **jamais** une donnée que le backend peut déduire.
- Quantité théorique, durées, TRS, temps de présence : **calculés**, jamais saisis.
- Tout champ de formulaire doit être justifié : « le système ne peut pas le savoir ».
- Corollaire UI : les écrans de déclaration sont **pré-remplis** (poste, OF, référence, équipe, shift) ; l'opérateur confirme.

### PC-2 — Assistant, pas superviseur
- Aucun écran ne classe/note les opérateurs.
- Aucune permission de géolocalisation (**décision D1-10**).
- Permissions demandées : **caméra** (QR) + **notifications**. Rien d'autre.
- Vocabulaire UI : « déclarer », « signaler », « j'arrive » — jamais « contrôler », « surveiller », « justifier ».

### PC-3 — Marche au jour 1, s'enrichit ensuite
- Mode **TRS-lite** : si la cadence théorique n'est pas renseignée, on affiche Dispo × Qualité et un badge « TRS-lite » au lieu d'une erreur.
- Tout widget KPI doit gérer 3 états : `donnée complète` / `donnée dégradée` / `donnée absente`.

### RD-01 — Densité opérateur
**Maximum 5 éléments interactifs par écran opérateur.** Contrainte auditable en revue de design.

---

## 2. Personas → surfaces frontend

| Code | Persona | Device | Surface | Écrans clés |
|---|---|---|---|---|
| P1 | Opérateur | Android perso (BYOD) / tablette partagée | **Ionic mobile** | Prise de poste, Home poste, Déclaration arrêt, Déclaration production, Rebut, Changement de série |
| P2 | Chef d'équipe | Téléphone + PC | **Ionic mobile + Web** | Plan atelier, Affectation, File d'alertes, Validation clôtures |
| P5 | Technicien maintenance | Téléphone | **Ionic mobile** | Inbox interventions, « J'arrive + ETA », Scan arrivée, Clôture technique |
| P6 | Technicien qualité | Téléphone + PC | **Ionic mobile + Web** | Inbox qualité, Contrôle 1ère pièce, Décision (OK / retouche / rebut) |
| P3 | Responsable production | PC + mobile alertes | **Web** | Dashboard TRS, Top causes, Pareto, Comparatif lignes |
| P4 | Directeur / DG | PC | **Web** | Vue direction, tendance mensuelle, export |
| P7 | Ingénieur process / méthodes | PC | **Web admin** | Référentiels, import Excel, gammes, cadences, causes |
| P8 | Admin RH / paie | PC | **Web admin** | Comptes, rôles, calendrier d'équipes — **aucun accès KPI** |

---

## 3. Design system

### 3.1 Origine
Le design system **est** la maquette client `maquette-v1_3.html`. On ne l'invente pas, on l'extrait.

### 3.2 Tokens de couleur

**Surfaces (thème graphite sombre — web/manager/Andon)**
```
--bg-app          #0B1015   fond application
--bg-surface      #121A21   carte / panneau
--bg-surface-2    #182129   carte surélevée / hover
--bg-inset        #0E151B   champ, zone creuse
--border          #22303B   séparateur
--border-strong   #2E3F4C   bordure de carte
--text-primary    #E6EDF3
--text-secondary  #9BAAB8
--text-muted      #6B7C8A
```

**Surfaces (thème clair — écrans opérateur mobile, lisibilité atelier/lumière)**
```
--bg-app          #F5F7F9
--bg-surface      #FFFFFF
--border          #E1E7EC
--text-primary    #0B1015
--text-secondary  #56656F
```

**Les 6 couleurs d'état machine — l'unique système chromatique produit**
| État | Token | Hex | Usage |
|---|---|---|---|
| Production | `--state-production` | `#1FA85C` | vert |
| Attente matière | `--state-material` | `#E0A800` | jaune |
| Changement de série | `--state-changeover` | `#E8641C` | orange |
| Arrêt technique | `--state-technical` | `#E23B3B` | rouge |
| Arrêt qualité | `--state-quality` | `#2C7BF2` | bleu |
| Non affecté | `--state-idle` | `#6B7C8A` | gris hachuré |

> Règle stricte : **aucune** couleur d'accent décorative. Un bouton primaire est neutre (graphite/blanc) ; seule la sémantique d'état est colorée. Cela évite qu'un opérateur confonde une couleur de marque avec un état machine.

**Accessibilité daltonisme (obligatoire, cf. stratégie de test)** : chaque état porte en plus **une icône + un libellé texte**. Jamais couleur seule.

### 3.3 Typographie
```
--font-sans   'IBM Plex Sans', system-ui, sans-serif
--font-mono   'IBM Plex Mono', monospace     → références, OF, horodatages, compteurs
--font-arabic 'IBM Plex Sans Arabic'         → activée sur [dir="rtl"]
```

Échelle (root 16 px sur mobile opérateur — **pas** 13 px : lisibilité gants/atelier ; 13 px conservé sur les écrans web denses) :

| Rôle | Taille | Poids | Contexte |
|---|---|---|---|
| Display poste | 32 px | 700 | nom du poste sur home opérateur |
| Titre écran | 22 px | 600 | |
| Titre carte | 16 px | 600 | |
| Corps | 15 px | 400 | mobile |
| Corps web | 13 px | 400 | tableaux, admin |
| Label / caption | 12 px | 500, `letter-spacing .04em`, uppercase | |
| Mono data | 13 px | 500 | REF-4021, 10:32 |

### 3.4 Espacement, rayons, ombres
```
espacement : 4 / 8 / 12 / 16 / 20 / 24 / 32 / 48
radius     : --r-sm 8px  --r-md 14px  --r-lg 20px  --r-full 999px
ombres     : --sh-1 0 1px 2px rgba(0,0,0,.35)
             --sh-2 0 8px 24px rgba(0,0,0,.28)
```

### 3.5 Cibles tactiles — « test du gant »
| Élément | Min |
|---|---|
| Bouton d'action opérateur | **72 × 72 px** |
| Bouton secondaire | 56 px de haut |
| Élément de liste tappable | 56 px |
| Espace entre 2 cibles | 12 px |
| Zone de scan QR | plein écran |

Critère de validation : utilisable avec des gants de manutention, une seule main, écran fissuré, luminosité extérieure.

### 3.6 Bi-langue et RTL
- `i18next` + `react-i18next`, namespaces `common | operator | events | kpi | admin`.
- Locales : `fr` (défaut), `ar` (RTL).
- **Aucune** valeur `margin-left` / `left` / `text-align: left` en dur → uniquement les propriétés logiques (`margin-inline-start`, `inset-inline-start`, `text-align: start`).
- `dir` posé sur `<html>` ; Ionic gère le mirroring natif.
- Les chiffres restent en chiffres latins (convention industrielle tunisienne).
- Test obligatoire : chaque écran passe en `ar` sans débordement ni icône inversée à tort (les icônes de sens — flèches — se retournent, les icônes de logo/objet non).

---

## 4. Inventaire des composants frontend

### 4.1 Primitives (`src/components/ui/`)
`Button` (variants: primary/secondary/ghost/danger, sizes: sm/md/lg/xl-glove) · `IconButton` · `Input` · `NumberStepper` (gros +/- pour saisie quantité au gant) · `Select` · `SearchField` · `Textarea` · `Checkbox` · `RadioGroup` · `Switch` · `Segment` · `Chip` · `Badge` · `Avatar` · `Card` · `Sheet` (bottom sheet) · `Modal` · `Toast` · `Skeleton` · `EmptyState` · `Spinner` · `ProgressBar` · `Tooltip` · `Divider`

### 4.2 Composants métier (`src/components/domain/`)

| Composant | Rôle |
|---|---|
| `StateBadge` | couleur + icône + libellé d'un des 6 états |
| `PostTile` | tuile poste du plan atelier (état, code, référence, badge « VOUS ») |
| `LineGroup` | groupe de tuiles d'une ligne |
| `ShopFloorMap` | plan atelier temps réel + légende |
| `StateLegend` | légende des 6 états |
| `QrScanner` | scanner plein écran + saisie manuelle de secours |
| `BiometricGate` | prise de poste biométrique / PIN de repli |
| `AssignmentCard` | affectation du jour (poste, OF, équipe, horaire) |
| `DeclareStopGrid` | grille 2 taps des motifs d'arrêt (icônes d'abord) |
| `DeclareProductionForm` | quantité OK / NOK pré-remplie |
| `ScrapForm` | rebut : quantité + cause + photo optionnelle |
| `ChangeoverPanel` | changement de série chronométré jusqu'à 1ère pièce bonne |
| `EventTimeline` | circuit d'un événement (déclaré → notifié → en route → sur place → résolu → clôturé) |
| `AlertFeedItem` | ligne du fil d'alertes + SLA restant |
| `SlaCountdown` | compte à rebours SLA, vire au rouge à l'échéance |
| `InterventionInbox` | inbox maintenance/qualité |
| `EtaPicker` | « j'arrive dans 5 / 10 / 15 min » |
| `ClosureForm` | clôture typée (3 types : résolu / palliatif / annulé) |
| `ShiftHeader` | shift, équipe, heure, statut connexion |
| `OfflineBanner` | bandeau hors-ligne + nombre d'éléments en file |
| `SyncQueueSheet` | détail de la file de synchronisation |
| `KpiTile` | TRS, dispo, perf, qualité + badge « TRS-lite » |
| `TrsGauge` | jauge TRS |
| `ParetoChart` | top causes d'arrêt |
| `TrendChart` | tendance TRS |
| `MtbfMttrCard` | fiabilité équipement |
| `DataTable` | tableau web : tri, pagination, filtres, recherche, densité |
| `FilterBar` | filtres site/zone/ligne/poste/équipe/période |
| `ExportButton` | export CSV/Excel |
| `AuditTrailList` | journal d'audit, mentions « saisie corrigée » |
| `AndonBoard` | affichage TV plein écran, auto-refresh, gros caractères |

### 4.3 Layouts
`OperatorShell` (pas de sidebar, header minimal, 1 action dominante) · `TechnicianShell` (tabs bas : Inbox / En cours / Historique) · `SupervisorShell` (tabs + plan) · `WebShell` (sidebar + topbar + contenu) · `AndonShell` (plein écran, sans chrome).

---

## 5. Écrans — carte complète

### 5.1 Mobile opérateur (P1)
| # | Écran | Route | Objectif temps | Contenu |
|---|---|---|---|---|
| O-1 | Prise de poste | `/op/login` | < 30 s total | biométrie / PIN, logo, 1 seule action |
| O-2 | Scan poste | `/op/scan` | | caméra plein écran + « saisir le code » |
| O-3 | Confirmation affectation | `/op/assignment` | | poste, ligne, OF, référence, shift → « Démarrer » |
| O-4 | **Home poste** | `/op/home` | | état courant en grand, compteur du shift, **4 actions max** : Production · Arrêt · Rebut · Changement de série |
| O-5 | Déclarer un arrêt | `/op/stop` | **< 5 s / 2 taps** | grille d'icônes de motifs (6–8 max), pas de champ texte obligatoire |
| O-6 | Arrêt en cours | `/op/stop/active` | | chrono, qui est notifié, ETA, « annuler » |
| O-7 | Déclarer production | `/op/production` | < 5 s | qty OK pré-remplie via stepper, qty NOK optionnelle |
| O-8 | Déclarer rebut | `/op/scrap` | < 10 s | qty + cause (liste courte) + photo optionnelle |
| O-9 | Changement de série | `/op/changeover` | | nouvelle référence, chrono lancé, « 1ère pièce bonne » |
| O-10 | Fin de poste | `/op/end` | | récap du shift, 0 saisie |
| O-11 | Historique perso | `/op/history` | | ses propres déclarations du jour, correction possible (tracée) |

**Règle de verrouillage** : quand un arrêt est ouvert, O-7 est désactivé avec explication (« clôturez l'arrêt pour déclarer la production »).

### 5.2 Mobile chef d'équipe (P2)
`/lead/map` plan atelier · `/lead/assign` affectation opérateurs → postes (drag ou tap-tap) · `/lead/alerts` file d'alertes + SLA · `/lead/validate` validation des clôtures · `/lead/team` présence de l'équipe (présence, pas performance individuelle).

### 5.3 Mobile technicien maintenance (P5) / qualité (P6)
`/tech/inbox` · `/tech/:id` détail intervention · action « J'arrive (ETA) » / « Occupé » · `/tech/:id/arrive` scan QR d'arrivée · `/tech/:id/close` clôture typée + cause racine + pièces · `/tech/history`.

### 5.4 Web superviseur / manager (P2/P3/P4)
`/dashboard` KPI + plan · `/map` plan atelier temps réel · `/events` tableau des événements (filtres, pagination, export) · `/events/:id` détail + timeline · `/reports/trs` · `/reports/pareto` · `/reports/mtbf` · `/reports/scrap` · `/andon` TV.

### 5.5 Web admin (P7/P8)
`/admin/sites` · `/admin/zones` · `/admin/lines` · `/admin/posts` (+ génération/impression QR) · `/admin/machines` · `/admin/products` · `/admin/routings` (gammes/cadences) · `/admin/causes` (arbre de causes) · `/admin/orders` (OF) · `/admin/users` · `/admin/roles` · `/admin/shifts` (calendrier d'équipes) · `/admin/routing-rules` (qui est notifié pour quel motif/zone) · `/admin/import` (import Excel) · `/admin/audit`.

---

## 6. Machine à états du poste (frontend)

```
                 ┌──────────────┐
   (pas d'op.)   │  NON AFFECTÉ │ gris
                 └──────┬───────┘
            affectation │
                        ▼
                 ┌──────────────┐
        ┌────────│  PRODUCTION  │ vert ◄────────┐
        │        └──────┬───────┘               │
        │               │                       │ clôture
   changement           │ déclaration d'arrêt   │ validée
   de série             ▼                       │
        │      ┌────────────────────┐           │
        ├─────►│ ATTENTE MATIÈRE    │ jaune ────┤
        │      ├────────────────────┤           │
        ├─────►│ ARRÊT TECHNIQUE    │ rouge ────┤
        │      ├────────────────────┤           │
        ├─────►│ ARRÊT QUALITÉ      │ bleu  ────┤
        │      └────────────────────┘           │
        ▼                                       │
 ┌──────────────────┐                           │
 │ CHANGEMENT SÉRIE │ orange ───(1ère pièce OK)─┘
 └──────────────────┘
```

- L'état est **dérivé** côté backend, jamais posé manuellement par l'UI.
- Le frontend affiche l'état reçu et applique les verrous d'action associés.
- Priorité si plusieurs événements ouverts : technique > qualité > matière > changement > production.

---

## 7. Moteur d'événements — contrat frontend

Cycle de vie affiché par `EventTimeline` :

```
DECLARED ──► NOTIFIED ──► ACKNOWLEDGED(ETA) ──► ON_SITE ──► RESOLVED ──► CLOSED
     │            │              │
     └─ CANCELLED └─ ESCALATED_1 └─ ESCALATED_2 / REROUTED
```

| Étape | Déclencheur UI | Affichage |
|---|---|---|
| DECLARED | opérateur, 2 taps | chrono démarre, poste change de couleur |
| NOTIFIED | backend (service mappé + chef) | « Maintenance notifiée » + SLA |
| ACKNOWLEDGED | technicien « J'arrive » + ETA | ETA visible côté opérateur |
| ON_SITE | scan QR du poste par le technicien | |
| RESOLVED | technicien, clôture typée | attente validation si requise |
| CLOSED | chef d'équipe / auto | poste repasse en production |

**SLA** (configurable, valeurs par défaut) : arrêt technique **10 min**, arrêt qualité **5 min**, attente matière **15 min**. Escalade N1 à l'échéance, N2 à 2× l'échéance. `SlaCountdown` passe ambre à 70 %, rouge à 100 %.

**Calendrier d'équipes** : le frontend n'affiche comme destinataires que les personnes en poste au moment de l'événement.

---

## 8. Offline-first

### 8.1 Règles
1. **Aucune perte de donnée.** Toute action opérateur est écrite d'abord en local, puis synchronisée.
2. **Horodatage à la saisie** (`occurred_at` local, monotone), pas à la synchronisation. Le backend enregistre aussi `received_at`.
3. Le frontend affiche toujours : en ligne / hors-ligne / N éléments en file.
4. Idempotence : chaque mutation porte un `client_event_id` (UUID v4) généré localement.

### 8.2 Implémentation
- Cache & mutations : **TanStack Query** + persister `IndexedDB` (`idb-keyval`).
- File durable : table `outbox` en IndexedDB `{ client_event_id, type, payload, occurred_at, attempts, status }`.
- Rejeu : au retour réseau, FIFO, backoff exponentiel, arrêt après 5 échecs → état `needs_attention` visible dans `SyncQueueSheet`.
- Référentiels (postes, causes, produits, OF du jour) : **préchargés** au démarrage du shift pour fonctionner totalement hors-ligne.
- Conflits : le serveur fait foi sur l'état dérivé ; les déclarations sont append-only donc sans conflit.

---

## 9. Stack technique frontend

```
React 18 · TypeScript 5 · Vite 5
Ionic React 8 + Capacitor 6      (mobile)
Tailwind CSS 3 (tokens CSS vars)
react-router-dom 7
@tanstack/react-query 5 (+ persist IndexedDB)
zustand                          (session, shift, préférences)
react-hook-form + zod            (formulaires + validation)
i18next / react-i18next          (fr, ar + RTL)
recharts                         (Pareto, tendance, jauge)
date-fns (+ locales fr, ar)
lucide-react                     (icônes)
```

**Plugins Capacitor** : `@capacitor/camera` ou `@capacitor-mlkit/barcode-scanning` (QR), `@capacitor/push-notifications`, `@capacitor/haptics` (retour tactile de confirmation), `@capacitor/network`, `@capacitor/preferences`, `@capacitor-community/biometric-auth` (ou `capacitor-native-biometric`).
**Interdits** : tout plugin de géolocalisation (PC-2 / D1-10).

### Budget de taille (< 50 Mo)
- Bundle web gzippé cible **< 400 Ko** ; APK cible < 15 Mo.
- Pas de librairie de composants lourde en plus d'Ionic.
- Recharts chargé en **lazy** — jamais dans le bundle opérateur.
- Polices auto-hébergées, subset latin + arabe, `woff2` uniquement.
- Code-splitting par rôle : `operator` / `technician` / `supervisor` / `admin` sont 4 chunks distincts. L'opérateur ne télécharge jamais l'admin.

### Performance
| Métrique | Cible |
|---|---|
| Démarrage à froid (Android 8, entrée de gamme) | < 3 s |
| Écran de déclaration interactif | < 1 s |
| Scan QR → écran poste | < 2 s |
| Déclaration complète | **< 5 s** |
| Rafraîchissement plan atelier | < 10 s (polling) ou temps réel |

---

## 10. Arborescence de fichiers cible

```
OAS/src/
├── app/
│   ├── router.tsx
│   ├── providers.tsx           QueryClient, i18n, theme, auth, offline
│   └── routes/
│       ├── operator/           O-1 … O-11
│       ├── technician/
│       ├── supervisor/
│       ├── admin/
│       └── andon/
├── components/
│   ├── ui/                     primitives
│   ├── domain/                 composants métier (§4.2)
│   └── layout/                 shells (§4.3)
├── features/
│   ├── auth/
│   ├── session/                prise de poste, shift courant
│   ├── assignment/
│   ├── declaration/            production, arrêt, rebut, changement
│   ├── events/                 moteur, SLA, escalade
│   ├── shopfloor/              plan atelier, états
│   ├── kpi/                    TRS, Pareto, MTBF
│   ├── admin/                  référentiels
│   └── offline/                outbox, sync, réseau
├── lib/
│   ├── api/                    client + endpoints typés
│   ├── db/                     IndexedDB (dexie/idb)
│   ├── i18n/
│   ├── state/                  machine à états poste (client)
│   └── utils/
├── styles/
│   ├── tokens.css
│   ├── theme-dark.css
│   ├── theme-light.css
│   └── rtl.css
├── types/                      types partagés (miroir du schéma DB)
└── locales/{fr,ar}/*.json
```

---

## 11. Contrat API attendu (côté frontend)

```
POST   /auth/login                      → session, rôles, tenant
POST   /sessions/start                  { post_id, qr_token } → session de poste
POST   /sessions/:id/end
GET    /assignments/me?date=
GET    /shopfloor/map?site_id=          → lignes + postes + états + événements ouverts
GET    /posts/:id
POST   /declarations/production         { client_event_id, session_id, qty_ok, qty_nok, occurred_at }
POST   /declarations/scrap              { client_event_id, qty, cause_id, photo?, occurred_at }
POST   /events/stop                     { client_event_id, post_id, reason_id, occurred_at }
POST   /events/:id/acknowledge          { eta_minutes }
POST   /events/:id/on-site              { qr_token }
POST   /events/:id/resolve              { closure_type, root_cause_id, note }
POST   /events/:id/close
POST   /events/:id/cancel
POST   /changeovers                     { post_id, to_product_id, occurred_at }
POST   /changeovers/:id/first-good-part
GET    /kpi/trs?scope=&from=&to=        → { trs, availability, performance, quality, mode: 'full'|'lite' }
GET    /kpi/pareto?scope=&from=&to=
GET    /kpi/mtbf?equipment_id=
GET    /events?filters&page&page_size   → liste paginée
GET    /reference/bootstrap             → tout le référentiel nécessaire au mode hors-ligne
GET    /audit?entity=&id=
```

Conventions : pagination `?page=&page_size=` → `{ items, total, page, page_size }` ; erreurs `{ error, message, details }` ; tout POST de déclaration accepte `client_event_id` pour l'idempotence.

---

## 12. Stratégie de test frontend

| Niveau | Outil | Critère |
|---|---|---|
| Unitaire | Vitest | machine à états, calculs TRS-lite, file outbox |
| Composant | Testing Library | verrous d'action, états dégradés, RTL |
| E2E | Playwright | les 6 parcours PA-1 → PA-6 |
| **Test des 5 secondes** | Playwright chronométré | déclaration d'arrêt en 2 taps, < 5 s |
| **Test du gant** | manuel | cibles ≥ 72 px, 1 main, gants |
| Offline | Playwright + `context.setOffline` | 0 perte, rejeu correct, horodatage à la saisie |
| RTL / i18n | snapshot visuel `ar` | aucun débordement, mirroring correct |
| Daltonisme | audit | icône + texte sur chaque état |
| Perf | Lighthouse + device réel Android 8 | démarrage < 3 s, bundle < 400 Ko |
| Accessibilité | axe | contraste AA, labels, focus |

---

## 13. Découpage de livraison

**Sprint P1 — Socle** : design tokens, i18n FR/AR + RTL, shells, primitives, référentiels en lecture, plan atelier statique, machine à états côté client.
**Sprint P2 — Parcours opérateur** : O-1 → O-11, scan QR, offline outbox, verrous, chronos.
**Sprint P3 — Visibilité & affectation** : plan atelier temps réel, fil d'alertes + SLA, affectation chef d'équipe, inbox technicien, clôtures, Andon.
**Puis L1 (MVP)** : KPI/reporting complet, admin référentiels + import Excel, journal d'audit, exports, notifications push, escalades.

---

## 14. Definition of Done d'un écran

- [ ] ≤ 5 éléments interactifs si écran opérateur
- [ ] Cibles ≥ 72 px pour les actions principales
- [ ] Rendu correct en `fr` **et** `ar` (RTL)
- [ ] États : chargement / vide / erreur / hors-ligne / données dégradées
- [ ] Aucune couleur hors des 6 tokens d'état ; état = couleur + icône + texte
- [ ] Aucun champ saisi qui pourrait être déduit (PC-1)
- [ ] Aucune donnée nominative de performance individuelle exposée (PC-2)
- [ ] Fonctionne hors-ligne ou dégrade explicitement
- [ ] Test E2E du parcours associé au vert
