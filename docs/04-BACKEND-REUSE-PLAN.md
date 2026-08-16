# OAS — Plan de réutilisation du backend (sweep vérifié v4 — monolithe, module isolé)

> **v4 — deux décisions verrouillées.** (1) Architecture tranchée : **monolithe**, OAS dans l'application `MyApi` existante (§7). (2) **Isolation stricte** : OAS ne modifie aucun module existant — plus de 4 actions ajoutées au `LookupsController` du socle, elles deviennent un sous-module OAS (§4.2, §4.7). Conséquences chiffrées : 22 sous-modules (au lieu de 21), **13 tables** à créer (au lieu de 12), **162 endpoints tous sous `api/oas/*`** (au lieu de 158 + 4 dans le socle), socle inchangé à 993 actions.

> **Méthode.** Chaque chiffre de ce document est produit par un script d'analyse statique reproductible — `scripts/inventory_controllers.py` (contrôleurs/actions) et des comptages `rg` sur `Backend/` et `db/migrations/` — jamais estimé. Chaque affirmation renvoie à un `fichier:ligne`. Tout ce qui n'a pas pu être vérifié dans le code est marqué **[NON VÉRIFIÉ]**.
>
> Régénérer l'inventaire : `python3 scripts/inventory_controllers.py --markdown`.
>
> **Corrections par rapport à la v2** (erreurs de comptage de la version précédente, toutes reproduites par le script) :
> | Affirmation v2 | Réalité vérifiée |
> |---|---|
> | 93 contrôleurs / 963 actions | **95 fichiers contrôleurs** (**100 classes** `ControllerBase`), **993 actions**, **996 mappings de route** |
> | WebsiteBuilder : 3 fichiers / 23 actions | **4 fichiers / 46 actions** — `WBSupportControllers.cs` contient **7 classes** de contrôleur, et `WBUploadController.cs` avait été oublié |
> | EmailAccounts : 2 / 20 | **3 / 30** — `EmailAccountsController_SyncEndpoints.cs` est un `partial class EmailAccountsController` (10 actions) sans `ControllerBase`, donc invisible aux greps précédents |
> | ExternalEndpoints : 2 / 18 | **2 / 16** — `ExternalReceiveController` a 2 méthodes portant chacune 2 attributs `[Http*]` |
> | Purchases : 5 / 38 | **5 / 37** — même cause (`GoodsReceiptsController`, 1 méthode à 2 verbes) |
> | 54 contrôleurs / 539 actions réutilisables (56 %) | **27 fichiers / 288 actions (29 %)** — périmètre arrêté : seul le socle transverse (identité, RBAC, tenant, lookups, fichiers, numérotation, synchro) est réutilisé ; tout le métier et tous les « constructeurs » sont écrits dans OAS |
> | `render.yaml` déclare `JWT_KEY` (mauvais nom) | `render.yaml` **ne déclare aucune variable** : le fichier ne contient que des commentaires, pas de bloc `services:`/`envVars:`. Le risque est réel mais différent (voir §8.5) |
>
> **Corrections par rapport à la v1** (erreurs de la version précédente) :
> | Affirmation v1 | Réalité vérifiée |
> |---|---|
> | 99 contrôleurs | **95** fichiers contrôleurs, **993** actions HTTP |
> | ~215 tables EF | **173** `DbSet` actifs (174 déclarés, 1 commenté) |
> | 43 tables OAS dans `db/migrations` | **33** tables, **18** enums, **17** fonctions et **14** triggers (dont 10 fonctions + 10 triggers dans `008`) |
> | Migrations EF `001-008` backend | `Backend/Migrations` = **7 scripts SQL manuels**, **aucun `CREATE TABLE`**, pas de migrations EF |
> | RBAC via `[Authorize(Roles=…)]` | RBAC **custom** via `[RequirePermission(module, action)]`, aucune policy ASP.NET déclarée |
> | Doc publiée à la racine `OAS-BACKEND-REFERENCE.md` | Ce fichier n'existait pas ; la référence est **ce document** + `docs/ANNEXE-INVENTAIRE-CONTROLEURS.md` |

---

## 1. État réel des deux côtés

### 1.1 Frontend OAS (`src/`) — aucun backend branché

Recherche exhaustive `fetch(|axios|XMLHttpRequest|apiClient` sur `src/` : **0 occurrence**. 100 % de la logique « serveur » est simulée côté client :

| Store | Clé localStorage | Fichier |
|---|---|---|
| Auth (session utilisateur) | `oas.auth.v1` | `src/oas/authStore.ts:21-102` |
| Référentiels (users, causes, cadences, produits, équipements, shifts, imports, sign-offs) | `oas.referentials.v1` | `src/oas/refStore.ts:121-674` |
| Hiérarchie sites/zones/lignes/postes | `oas.hierarchy.v1` | `src/oas/hierarchyStore.ts:16-212` |
| Événements andon | `oas.events.v1` | `src/oas/eventStore.ts:45-306` |
| Affectations + présence | `oas.assignments.v2` | `src/oas/assignmentStore.ts:16-180` |
| Audit | `oas.audit.v1` | `src/oas/auditStore.ts:14-87` |
| Session opérateur (déclarations, arrêts, file offline) | `oas.operator.session.v1` | `src/modules/auth/store/session.ts:29-90` |
| Activation des plugins/modules | `oas.plugins.activations.v1` | `src/modules/shared/plugins/activationStore.ts:17-92` |

Faits importants :

- **Auth mobile** : `verifyPin(code, pin)` compare un **PIN en clair** stocké dans `localStorage` (`src/oas/refStore.ts:429-434`, PIN dérivé du matricule `refStore.ts:212-215`).
- **Auth console web** : mot de passe **codé en dur** `DEMO_CONSOLE_PASSWORD = 'secret123'` (`src/oas/authStore.ts:79`, vérif `:86-97`). Aucun token, aucun cookie.
- **Sync offline** : purement simulée — `setTimeout` + `Math.random() < 0.2` pour feindre un réseau instable (`src/modules/auth/store/session.ts:502-523`), puis `syncPending()` bascule simplement `synced: true` (`:436-448`). `useOnline()` (`:473-482`) lit bien `navigator.onLine` — c'est la seule partie réutilisable telle quelle.
- **Aucune variable d'environnement, aucune base URL d'API** n'existe dans `src/`.

**Conséquence** : le branchement backend est un chantier neuf à 100 % côté frontend (client HTTP, gestion token, file de synchro réelle, invalidations). Le découpage des stores est cependant déjà aligné sur les entités serveur, donc chaque store devient un module d'appels API à interface identique.

### 1.2 Backend existant (`Backend/`) — ASP.NET Core 8, assembly `MyApi`

- **95 fichiers contrôleurs**, **100 classes** dérivant de `ControllerBase`, **993 actions HTTP** (996 mappings `[Http*]`), **47 modules** — détail exhaustif ligne par ligne dans `docs/ANNEXE-INVENTAIRE-CONTROLEURS.md`.
  - 2 pièges de comptage à connaître : `Modules/WebsiteBuilder/Controllers/WBSupportControllers.cs` regroupe **7 classes** de contrôleur dans un seul fichier, et `Modules/EmailAccounts/Controllers/EmailAccountsController_SyncEndpoints.cs:6` est un `partial class EmailAccountsController` **sans** `: ControllerBase` (10 actions qui échappent à tout grep sur `ControllerBase`).
- **1 seul `DbContext`** : `MyApi.Data.ApplicationDbContext` (`Backend/Data/ApplicationDbContext.cs`, 1367 lignes), **173 `DbSet` actifs** (174 déclarés, `ArticleGroups` commenté `:108`). Mapping des noms de tables : **23** `.ToTable(...)` dans le contexte, **67** dans les `Data/*Configuration.cs` des modules (90 au total) et **155** attributs `[Table("…")]` sur les modèles.
- **Pas de migrations EF** : `Backend/Migrations` contient 7 scripts SQL manuels (ALTER/index/seed uniquement, **zéro `CREATE TABLE`**). Le schéma est créé/réparé au runtime par `Backend/Infrastructure/DatabaseSchemaSynchronizer.cs` et `RuntimeSchemaRepair.cs` (env `AUTO_REPAIR_MISSING_COLUMNS`).
- **Le schéma OAS de `db/migrations/` (33 tables, uuid, snake_case) ne partage aucun nom de table avec le backend .NET (int, PascalCase)** : ce sont deux modèles de données indépendants cohabitant dans le dépôt. Décision arrêtée (§7) : ils le restent — OAS est porté par un schéma `oas` et un `OasDbContext` séparés, dans le même déploiement (§4.7).

---

## 2. Contrat technique du backend (vérifié)

### 2.1 Multi-tenancy

`Backend/Infrastructure/TenantMiddleware.cs:21-24` :

```csharp
public const string TenantHeaderName       = "X-Tenant";        // sélectionne la BASE physique / slug
public const string TargetTenantHeaderName = "X-Target-Tenant";  // sélectionne la société (TenantId) dans la base
public const string ViewAllHeaderName      = "X-View-All";       // vue inter-sociétés (MainAdminUser seulement)
public const string ViewAllSentinel        = "__all__";
```

- Base par tenant : `TENANT_<SLUG>_DATABASE_URL` (`render.yaml`, résolu `Program.cs:561,604`).
- Filtre global EF sur `ITenantEntity.TenantId` (`Backend/Data/ApplicationDbContext.cs:399-434`), variante « scope » pour les entités `[ModuleScope]` : modules partagés filtrés sur `TenantId == 0` (`:444-453`). `_currentTenantId == -1` = sentinelle « voir tout ».
- Estampillage automatique de `TenantId` à l'insertion (`:509-593`, appelé par les 4 surcharges `SaveChanges*` `:457-483`).
- Requête authentifiée sans société active sur un chemin non exempté → **HTTP 428 `{"error":"company_required"}`** (`TenantMiddleware.cs:311-324`). Chemins exemptés (`:80-102`) : `/api/public`, `/api/auth`, `/api/email-verification`, `/api/twofactor`, `/api/tenants`, `/api/systemlogs`, `/api/logs`, `/api/profile`, `/api/users/me`, `/api/me`, `/api/module-scope`, `/api/health`, `/api/documents/upload`, `/api/upload`, `/swagger`.

**Impact OAS** : chaque requête du frontend doit porter `X-Tenant` **et** `X-Target-Tenant`, et gérer le 428 comme « choisir une société ».

### 2.2 Authentification

`Backend/Modules/Auth/Services/AuthService.cs` :

- JWT HMAC-SHA256, signé avec `Jwt:Key`, `ValidateLifetime = **false**` (`Program.cs:273-290`) et **expiration à 10 ans** (`AuthService.cs:997` et `:1210`).
- Refresh token = 64 octets aléatoires base64 (`:1013-1019`), stocké sur `User.RefreshToken`/`TokenExpiresAt`, revalidé sur `/api/auth/refresh` (`:606`, `:642`).
- Claims admin (`:981-989`) : `NameIdentifier, Email, Name, UserId, FirstName, LastName, Industry, UserType=MainAdminUser, login_type=admin`.
- Claims utilisateur (`:1190-1202`) : + `Role, UserType=RegularUser, login_type=user, tenant_id, can_switch_company`.

### 2.3 Autorisation

RBAC **maison** : `[RequirePermission(module, action)]` (`Backend/Infrastructure/RequirePermissionAttribute.cs`) interroge `IPermissionService.UserHasPermissionAsync(userId, module, action)`. Le claim `UserType=MainAdminUser` **court-circuite tous les contrôles** (`:44-46`). `builder.Services.AddAuthorization();` est appelé sans aucune policy (`Program.cs:292`).

### 2.4 Pipeline & services transverses

Ordre du pipeline (`Program.cs:1376-1462`) : CORS filet de sécurité inline → `GlobalExceptionMiddleware` → `UseCors("AllowFrontend")` → compression → Swagger → static files (+ `/uploads`) → HTTPS redirect → **Authentication → Authorization → TenantMiddleware** → `MapControllers()` (`:1597`) → `MapHub<WorkflowHub>("/hubs/workflow")` (`:1601`).

| Capacité | Réalité |
|---|---|
| Cache | Redis via `REDIS_URL`, sinon mémoire (`Program.cs:76-98`) |
| Temps réel | SignalR, **1 seul hub** `/hubs/workflow` (`Program.cs:1601`) |
| Jobs de fond | `WorkflowPollingService` (`:461`), `PaymentReminderService` (`:464`), `WebhookForwardWorker` (`:381`), `ProcessSchedulerService` (`:491`) + 17 `IProcessHandler` (`:469-488`) |
| Fichiers | UploadThing (`Program.cs:385`, clé `UploadThing:Token`) + `/uploads` local, limite 50 Mo (`:107-112`) |
| E-mail | SMTP MailKit (`Modules/EmailAccounts/Services/EmailAccountService_SendMethods.cs:8-9`) |
| SMS / push | **Aucun fournisseur** dans le code |
| Rate limiting | **Aucun middleware ASP.NET**. Seul un compteur applicatif 60/min pour les webhooks publics (`Modules/ExternalEndpoints/Services/ExternalEndpointService.cs:397-415`) |
| CORS | `AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()` (`Program.cs:498-523`) |
| Déploiement | Render, Docker .NET 8, port `$PORT` (défaut 10000/8080), `/health` |

---

## 3. Verdict de réutilisation par module (47 modules, chiffres exacts)

Légende : **RÉUTILISER** tel quel · **ADAPTER** (existe, à étendre) · **IGNORER** (hors périmètre OAS) · **À CRÉER** (n'existe pas).

**Périmètre décidé — règle unique :** OAS ne réutilise que le **socle transverse** (identité, RBAC, tenant, référentiels, fichiers, journalisation, numérotation, synchro). **Tout ce qui est métier ou « constructeur » est écrit dans OAS, avec ses propres formulaires, ses propres entités et son propre moteur de workflow** : workflow/escalade SLA, formulaires et contrôles qualité, tableaux de bord et KPI/OEE, calendriers de poste, présence opérateur, interventions maintenance, produits/rebuts, webhooks MES, e-mails d'escalade.

Conséquence : ces modules ne sont **ni appelés, ni étendus, ni maintenus** par OAS, et aucune dépendance ne doit être introduite vers eux — WorkflowEngine (et son hub `/hubs/workflow`), WebsiteBuilder, DynamicForms/PublicForms, Dashboards, Reporting, Processes, Calendar, AiChat/UserAiSettings, HR, SupportTickets, Incidents, Articles, EmailAccounts, ExternalEndpoints, OfflineHydration, Preferences/PdfSettings, et tout le bloc CRM/ventes/achats.

| Module backend | Fichiers | Actions | Verdict OAS | Justification |
|---|---:|---:|---|---|
| Auth | 4 | 32 | **ADAPTER** | Login/refresh/OTP/2FA/OAuth complets, mais **pas de login par PIN/matricule** ni de scan QR → 2 endpoints à ajouter |
| Users | 1 | 12 | **RÉUTILISER** | CRUD, mot de passe, recherche par e-mail — couvre l'annuaire opérateurs |
| Roles | 2 | 19 | **RÉUTILISER** | Rôles + permissions module/action, assignation utilisateur |
| Tenants | 1 | 7 | **RÉUTILISER** | Sociétés, logo, défaut |
| Settings (AppSettings, ModuleScope) | 2 | 7 | **RÉUTILISER** | Paramètres clé/valeur + portée des modules |
| Plugins | 2 | 13 | **RÉUTILISER** | Remplace `activationStore.ts` (activation par module) |
| Notifications | 1 | 8 | **ADAPTER** | Liste/non-lus/marquage OK ; **pas de push temps réel** hors SignalR workflow |
| Lookups (`LookupsController` + `Lookups/PreferencesController`) | 2 | 124 | **LECTURE SEULE** | Consommable tel quel, **jamais modifié** (§4.7). Les listes plates OAS vivent dans le sous-module `OAS/Lookups` + table `oas_lookup_values` (§4.2). À ne pas confondre avec le module `Preferences` (ignoré) |
| Documents | 1 | 8 | **RÉUTILISER** | Pièces jointes des événements/arrêts |
| Shared (Upload, UploadThing, Logs, SystemLogs, EntityFormDocuments, Dev) | 6 | 24 | **RÉUTILISER** | Upload photos poste + journalisation |
| Signatures | 1 | 3 | **RÉUTILISER** | Signature de fin de poste (`refStore.signShiftReport`) |
| Numbering | 1 | 6 | **RÉUTILISER** | Numérotation des OF / tickets |
| Sync | 1 | 4 | **MODÈLE, NON MODIFIÉ** | `push`/`pull`/`history`/`retry` servent de patron ; OAS écrit ses propres `api/oas/sync/*` (idempotence `clientEventId`, `sync_receipts`) plutôt que d'étendre le contrôleur du socle (§4.7) |
| Skills | 1 | 12 | **RÉUTILISER** | Habilitations opérateur/poste |
| UserGroups | 1 | 9 | **RÉUTILISER** | Équipes de production |
| HR | 1 | 73 | **IGNORER** | Paie/congés/recrutement ; la présence opérateur OAS est portée par `shifts`/`assignments` maison |
| Incidents | 1 | 1 | **IGNORER** | Coquille d'une seule action, sans valeur |
| SupportTickets | 2 | 16 | **IGNORER** | OAS écrit ses propres interventions maintenance |
| ExternalEndpoints | 2 | 16 | **IGNORER** | Webhooks MES/ERP écrits dans le module d'intégration OAS |
| EmailAccounts | 3 | 30 | **IGNORER** | Boîtes mail CRM ; l'escalade OAS passe par un service SMTP dédié |
| OfflineHydration | 1 | 2 | **IGNORER** | La file offline OAS est portée par le sous-module `OAS/Offline` |
| Articles / StockTransactions | 4 | 31 | **IGNORER** | Modèle article/stock CRM ; `products` et mouvements rebut sont des entités OAS |
| WorkflowEngine | 5 | 28 | **IGNORER** | Moteur no-code d'escalade — OAS écrit son propre moteur SLA andon et son propre hub |
| WebsiteBuilder (dont `WBSupportControllers.cs`, 7 classes) | 4 | 46 | **IGNORER** | Constructeur de site public |
| DynamicForms (+ PublicForms) | 2 | 12 | **IGNORER** | Form builder générique ; les contrôles qualité OAS sont typés en dur |
| Dashboards (builder) + Reporting (favoris) | 5 | 23 | **IGNORER** | Widgets no-code ; les agrégats OEE sont écrits dans le module KPI OAS |
| Processes (ordonnanceur générique) | 1 | 10 | **IGNORER** | OAS utilise un `BackgroundService` dédié au recalcul KPI |
| Calendar | 1 | 17 | **IGNORER** | Modèle rendez-vous, sans rapport avec les calendriers de shift 3×8 |
| AiChat + UserAiSettings | 3 | 21 | **IGNORER** | Hors périmètre |
| Preferences / PdfSettings | 2 | 12 | **IGNORER** | Préférences CRM et gabarits PDF de facture |
| Deals, Offers, Sales, Invoices, Payments, Purchases, Contacts, Dispatches, Installations, ServiceOrders, Projects, Planning, PlanningProfiles, RetenueSource, ModuleRequests | 31 | 367 | **IGNORER** | CRM / ventes / achats / terrain — hors périmètre atelier |

**Socle transverse réutilisé : 27 fichiers contrôleurs / 288 actions** (29 % des 993), sur 15 modules — Auth, Users, Roles, UserGroups, Tenants, Settings, Plugins, Lookups, Notifications, Documents, Shared, Signatures, Numbering, Sync, Skills. **68 fichiers / 705 actions sont ignorés** : ils restent déployés mais ne sont ni appelés, ni étendus, ni testés par OAS (coût nul, verrouillés par `[RequirePermission]`).

---

## 4. Ce qui manque réellement : modules OAS à créer

Dérivé strictement des API des stores frontend (§1.1) et du schéma `db/migrations` (§5).

| # | Module à créer | Endpoints attendus | Source frontend |
|---|---|---|---|
| 1 | **Hierarchy** (sites/zones/lignes/postes) | `GET/POST/PUT/DELETE /api/oas/sites\|zones\|lines\|posts`, `POST …/{id}/archive`, `POST …/{id}/restore` | `hierarchyStore.ts:16-212` |
| 2 | **Equipments** | CRUD + `GET /api/oas/posts/{id}/equipments` | `refStore.ts` (code, kind, model, partsPerCycle, criticality, serial) |
| 3 | **Cadences** | `GET /api/oas/cadences`, `POST` (nouvelle version), `GET …/{id}/history` | `refStore.ts` (versionné) |
| 4 | **Causes** | CRUD arbre famille/détail, `POST /proposals`, `POST /proposals/{id}/review`, `GET /usage` | `refStore.ts` (`CauseNode`, `CauseProposal`, `causeUsage`) |
| 5 | **Shifts** | CRUD templates + calendrier, `POST /signoffs`, `GET /signoffs` | `refStore.ts` (`ShiftRow`, `ShiftSignOff`) |
| 6 | **Assignments & Presence** | `GET/PUT /api/oas/assignments`, `POST /auto-fill`, `POST /publish`, `PUT /presence/{operator}` | `assignmentStore.ts:16-180` |
| 7 | **PostSessions** | `POST /open`, `POST /{id}/relay`, `POST /{id}/close`, `GET /active`, `POST /scan` | `session.ts`, `ScanPage` |
| 8 | **Declarations** | `POST /production`, `POST /scrap`, `PUT /{id}/correct` (fenêtre + motif + valeur d'origine), `GET` par poste/session | `session.ts:declareProduction/correctDeclaration` |
| 9 | **Events (Andon)** | `POST`, `POST /{id}/take`, `/eta`, `/arrive`, `/advance`, `/ack`, `/escalate`, `/requalify`, `/close`, `/decline`, `GET` filtré, `GET /{id}/transitions` | `eventStore.ts:45-306` |
| 10 | **PostStates** | `GET /api/oas/post-states` (live), `GET /{postId}/history` | `liveState.ts`, `ShopFloorPage` |
| 11 | **KPI / OEE** | `GET /api/oas/kpi/daily`, `/pareto`, `/trend`, `/line-comparison` (scope site/zone/ligne/poste) | `demo.ts:1-171`, `MobileKpi`, `ManagerDashboard` |
| 12 | **Imports** | `POST /api/oas/imports` (produits, causes…), `GET /api/oas/imports` | `refStore.applyImport`, `ImportRecord` |
| 13 | **Products & OF** | CRUD `products`, CRUD `production_orders`, `GET /products/{id}/cadences` | `refStore.ts` (produits, OF) — `Articles` écarté |
| 14 | **Interventions (maintenance)** | `GET/POST /interventions`, `POST /{id}/assign`, `/start`, `/close`, `GET /inbox` | `InterventionInbox.tsx` — `SupportTickets` écarté |
| 15 | **Quality checks** | CRUD gabarits de contrôle + `POST /checks`, `GET /checks?postId=` | table `quality_checks` — `DynamicForms` écarté |
| 16 | **Workflow & escalade SLA (propre à OAS)** | `GET/PUT /workflow/rules`, `GET /workflow/escalations`, hub SignalR `oas` | `eventStore` (SLA, escalade) — `WorkflowEngine` écarté |
| 17 | **Intégrations MES/ERP** | `POST /integrations/webhooks/in`, CRUD abonnements sortants | `ExternalEndpoints` écarté |

À cela s'ajoutent **2 endpoints d'authentification atelier** à greffer sur le module Auth existant :

- `POST /api/auth/pin-login` — matricule + PIN, PIN **haché serveur** (jamais renvoyé au client).
- `POST /api/auth/badge-login` — jeton QR de poste (`posts.qr_token`, `db/migrations/003:46`).

**Non couvert par le frontend actuel mais présent dans le schéma SQL** (à décider) : `teams`/`team_members`, `routing_rules`, `changeovers`, `quality_checks`, `event_notifications`, `device_tokens`, `attachments`, `sync_receipts`.

### 4.1 Catalogue complet des endpoints OAS à écrire

Préfixe commun `/api/oas`. Colonne « Source » = fonction du store frontend (`fichier:ligne`) ou table SQL qui impose l'endpoint. Aucun de ces endpoints n'existe aujourd'hui (vérifié : aucun contrôleur du dépôt ne référence `oas`, `post`, `declaration`, `andon` en tant que route).

| Domaine | Endpoints | Source |
|---|---|---|
| Sites | `GET /sites` · `POST /sites` · `PUT /sites/{id}` · `POST /sites/{id}/archive` | `hierarchyStore.ts:105,112,118` |
| Zones | `GET /zones?siteId=` · `POST /zones` · `PUT /zones/{id}` · `POST /zones/{id}/archive` | `hierarchyStore.ts:128,135,141` |
| Lignes | `GET /lines?zoneId=` · `POST /lines` · `PUT /lines/{id}` · `POST /lines/{id}/archive` | `hierarchyStore.ts:151,158,164` |
| Postes | `GET /posts?lineId=` · `POST /posts` · `PUT /posts/{id}` · `PUT /posts/{id}/attributes` · `PUT /posts/{id}/critical` · `POST /posts/{id}/archive` · `GET /posts/{id}/capacity` | `hierarchyStore.ts:174,188,198,202,208` ; `refStore.ts:419` |
| Équipements | `GET /equipments?postId=` · `POST /equipments` · `PUT /equipments/{id}` · `DELETE /equipments/{id}` | `refStore.ts:400,414` |
| Cadences | `GET /cadences` · `POST /cadences` (nouvelle version) · `PUT /cadences/{id}` · `GET /cadences/{id}/history` | `refStore.ts:276,295` |
| Causes | `GET /causes` (arbre) · `POST /causes` · `PUT /causes/{id}` · `PUT /causes/{id}/kind` · `PUT /causes/{id}/criticality` · `PUT /causes/{id}/active` · `POST /causes/{id}/children` · `DELETE /causes/{id}/children/{childId}` · `DELETE /causes/{id}` · `GET /causes/usage` | `refStore.ts:537-604,348` |
| Propositions de cause | `GET /cause-proposals` · `POST /cause-proposals` · `POST /cause-proposals/{id}/review` | `refStore.ts:312,328` |
| Produits | `GET /products` · `POST /products` · `PUT /products/{id}` · `DELETE /products/{id}` | `refStore.ts:607,624` |
| Ordres de fabrication | `GET /production-orders` · `POST /production-orders` · `PUT /production-orders/{id}/status` | table `production_orders` (`003:…`) |
| Shifts | `GET /shifts` · `POST /shifts` · `PUT /shifts/{id}` · `DELETE /shifts/{id}` · `GET /shifts/calendar?from=&to=` · `PUT /shifts/calendar` | `refStore.ts:648,666,634,644,672` |
| Sign-off de poste | `POST /shift-signoffs` · `GET /shift-signoffs?shift=&date=` | `refStore.ts:437` |
| Utilisateurs atelier | `GET /operators` · `POST /operators` · `PUT /operators/{id}/active` · `PUT /operators/{id}/role` · `PUT /operators/{id}/scope` · `POST /operators/{id}/regenerate-pin` | `refStore.ts:358,371,378,384,390` |
| Équipes | `GET /teams` · `POST /teams` · `PUT /teams/{id}/members` | tables `teams`, `team_members` (`004`) |
| Affectations | `GET /assignments?shift=&date=` · `PUT /assignments/{postId}` · `DELETE /assignments/{postId}` · `POST /assignments/auto-fill` · `DELETE /assignments` · `POST /assignments/publish` | `assignmentStore.ts:87,98,103,113,119` |
| Présence | `PUT /presence/{operatorId}` · `POST /presence/{operatorId}/confirm` · `GET /presence?shift=&date=` | `assignmentStore.ts:130,148,163` |
| Sessions de poste | `POST /post-sessions/open` · `POST /post-sessions/{id}/relay` · `POST /post-sessions/{id}/close` · `GET /post-sessions/active` · `POST /post-sessions/scan` | `src/modules/auth/store/session.ts` ; `ScanPage` |
| Déclarations | `POST /declarations/production` · `POST /declarations/scrap` · `PUT /declarations/{id}/correct` · `GET /declarations?postId=&sessionId=` | `session.ts` ; trigger `declarations_immutable` (`008`) |
| Changements de série | `POST /changeovers` · `PUT /changeovers/{id}/finish` · `GET /changeovers` | `ChangeoverPage.tsx` ; table `changeovers` (`005:154`) |
| Contrôles qualité | `POST /quality-checks` · `GET /quality-checks?postId=` | table `quality_checks` (`005:180`) |
| Événements (andon) | `POST /events` · `GET /events` (filtré) · `GET /events/{id}` · `POST /events/{id}/take` · `PUT /events/{id}/eta` · `POST /events/{id}/arrive` · `POST /events/{id}/advance` · `POST /events/{id}/ack` · `POST /events/{id}/escalate` · `POST /events/{id}/requalify` · `POST /events/{id}/decline` · `POST /events/{id}/close` · `GET /events/{id}/transitions` | `eventStore.ts:99-258` |
| Notifications d'événement | `GET /event-notifications?eventId=` · `POST /event-notifications/{id}/respond` | table `event_notifications` (`005:131`) |
| États de poste | `GET /post-states` (live) · `GET /post-states/{postId}/history` | `liveState.ts:38,150` ; trigger `recompute_post_state` |
| KPI / OEE | `GET /kpi/daily` · `GET /kpi/pareto` · `GET /kpi/trend` · `GET /kpi/line-comparison` (scope site/zone/ligne/poste) | `liveState.ts:122,130` ; `demo.ts` |
| Imports | `POST /imports` · `GET /imports` · `GET /imports/{id}` | `refStore.ts:450` ; table `imports` (`007`) |
| Audit | `GET /audit?entity=&from=&to=` | `auditStore.ts:62,85` ; trigger `audit_row` |
| Offline / sync | `POST /sync/push` (idempotent sur `clientEventId`) · `GET /sync/pull?since=` · `GET /sync/receipts` | `session.ts` ; table `sync_receipts` (`007`) |
| Pièces jointes | `POST /attachments` · `GET /attachments?entity=&id=` | table `attachments` (`007`) — peut être délégué à `/api/documents` |
| Push mobile | `POST /device-tokens` · `DELETE /device-tokens/{token}` | table `device_tokens` (`007`) — **aucun fournisseur push dans le backend actuel** |

**Volumétrie : ~155 endpoints répartis sur 17 modules OAS + 2 endpoints d'authentification atelier** (~120 pour les 12 modules du catalogue ci-dessus, ~35 pour les 5 modules ajoutés par le resserrage du périmètre : produits/OF, interventions, contrôles qualité, workflow SLA propre, intégrations MES). Environ 40 % (référentiels : sites/zones/lignes/postes/équipements/produits/causes) sont du CRUD pur générable sur le patron des contrôleurs existants ; les 60 % restants portent la logique métier (sessions, déclarations, andon, KPI, SLA) et n'ont aucun équivalent dans le backend.

---

### 4.2 Selects pilotés par lookups — plus aucune liste en dur

**Règle : tout `<Select>` de l'application se remplit depuis une source serveur.** Deux sources seulement, jamais un tableau TypeScript :

1. **Entités OAS** (elles ont des attributs, une hiérarchie, un cycle de vie) → endpoints `§4.1` : sites, zones, **lignes**, **postes**, équipements, produits, OF, shifts, opérateurs, équipes, causes.
2. **Lookups** (listes plates, éditables par l'admin, sans logique) → module `Lookups` réutilisé, avec un `LookupType` par liste.

#### Listes en dur actuellement dans le frontend (à supprimer)

| Constante | Fichier:ligne | Consommée par | Remplacement |
|---|---|---|---|
| `LINE_KEYS` (3 lignes figées) | `src/oas/demo.ts:51` | `UsersPanel.tsx:13,94`, `ShopFloorMap.tsx:74`, `AndonTv.tsx:53,254`, `scope.ts:25` | **Entité** `GET /api/oas/lines` |
| `POSTS` | `src/oas/demo.ts:62` | carte atelier, affectations | **Entité** `GET /api/oas/posts?lineId=` |
| `ROSTER` / `BOARD_POSTS` | `src/oas/assignmentStore.ts:30,31` | `Assignments.tsx` | **Entités** `GET /api/oas/operators`, `/posts` |
| `STOP_REASONS` | `src/oas/demo.ts:123` | `DeclareStop`, `NeighborStop`, `InterventionInbox:76`, `refStore.ts:187,518` | **Entité** `GET /api/oas/causes` (arbre + workflow de validation) |
| `ROLE_KEYS` | `src/oas/refStore.ts:22` | `UsersPanel.tsx:45,86` | **Socle** `GET /api/roles` |
| `PRESENCES` | `src/oas/assignmentStore.ts:19` | `RosterPanel` | **Lookup** `PresenceStatus` |
| Types de shift (`DB_SHIFTS`) | `src/oas/fixtures.ts` → `refStore.ts:221` | `ShiftCalendars` | **Entité** `GET /api/oas/shifts` |

#### Listes qui deviennent des lookups (`LookupType`)

`PostType` · `PostCriticality` · `EquipmentType` · `CadenceUnit` · `ProductFamily` · `PackagingUnit` · `ScrapMotif` · `QualityDefect` · `ChangeoverType` · `PresenceStatus` · `AbsenceReason` · `InterventionOutcome` · `ImportSource` · `ShiftLabel` · `SiteType` · `ZoneType`.

#### Listes qui restent des enums figés (ne PAS mettre en lookup)

`EVENT_STAGES` (`demo.ts:100`) et `EventKind` : ils pilotent des transitions d'état et des triggers SQL (`005`, `008`). Les rendre éditables casserait le moteur d'événements — même raison que la note `ContactType` de `LookupSeedData.cs:11-14`.

#### Les listes plates sont servies par OAS, **pas** par le module `Lookups` du socle

`LookupsController` (`Backend/Modules/Lookups/Controllers/LookupsController.cs`) n'expose **que des routes nommées en dur** (`article-categories`, `priorities`, `leave-types`, `currencies`…) — vérifié, aucune route paramétrée par `{lookupType}`. La v3 prévoyait d'y ajouter 4 actions génériques : **c'est abandonné**. Règle d'isolation (§4.7) : OAS ne modifie **aucun** contrôleur, modèle, service ou table d'un module existant. Le socle `Lookups` est donc consommé **en lecture seule** là où il apporte déjà une valeur (rien aujourd'hui pour l'atelier), et OAS possède ses propres listes plates :

| Endpoint (sous-module `OAS/Lookups`) | Rôle |
|---|---|
| `GET /api/oas/lookups/{type}` | liste active triée par `sort_order` (tenant + périmètre courants) |
| `POST /api/oas/lookups/{type}` | création (admin OAS) |
| `PUT /api/oas/lookups/{type}/{id}` | renommage / réordonnancement / activation |
| `DELETE /api/oas/lookups/{type}/{id}` | désactivation logique (`archived_at`) |

Support : **une seule table OAS** `oas_lookup_values (id uuid, tenant_id, type, code, label, color, sort_order, is_default, archived_at)` créée en migration `009` (§4.5, ligne 13). Aucun `LookupItem` du socle n'est écrit par OAS, donc aucun risque de collision de `LookupType` avec les listes CRM/RH existantes.

Côté frontend, un seul hook `useOasLookup(type)` (cache + invalidation) remplace tous les imports de constantes, et l'écran Référentiels reçoit un onglet « Listes » générique.

**Impact volumétrie : +4 actions dans OAS** (jamais dans le socle), **+1 table OAS**, **7 constantes frontend supprimées**.

---

### 4.3 Auth et gestion des utilisateurs — on reste simple

Vérifié dans le code : le socle expose déjà 25 actions sur `AuthController`, 10 sur `UsersController`, 8 sur `RolesController`, 10 sur `PermissionsController`. **OAS n'en consomme qu'un sous-ensemble minimal** ; le reste reste déployé mais non utilisé.

| Besoin OAS | Endpoint | Statut |
|---|---|---|
| Connexion console | `POST /api/auth/login` (`AuthController.cs:124`) | réutilisé tel quel |
| Session courante | `GET /api/auth/me` (`:621`) | réutilisé |
| Renouvellement de jeton | `POST /api/auth/refresh` (`:531`) | réutilisé |
| Déconnexion | `POST /api/auth/logout` (`:965`) | réutilisé |
| Changement de mot de passe | `POST /api/auth/change-password` (`:915`) | réutilisé |
| Liste / création / désactivation d'utilisateur | `GET|PUT|DELETE /api/users/{id}` (`UsersController.cs:49,147,247`) | réutilisé |
| Rôles et affectation | `GET /api/roles`, `POST /api/roles/{roleId}/assign/{userId}` (`RolesController.cs:49,163`) | réutilisé |
| **Connexion atelier (matricule + PIN)** | `POST /api/oas/shopfloor/login` · `POST /api/oas/shopfloor/pin/regenerate` | **à écrire** (2 endpoints) |

**Non retenu pour OAS** (aucun écran ne les appelle) : OAuth Google/Microsoft (`OAuthCallbackController`), 2FA (`TwoFactorController`), vérification d'e-mail (`EmailVerificationController`), OTP/forgot-password dupliqués sur `UsersController:417,457,500`, `signup` public (`:249`), endpoints de diagnostic `test-db` (`:1049`) et `test-signup` (`:1082`) — **ces deux derniers doivent être supprimés ou conditionnés à `IsDevelopment()` avant mise en production**.

La gestion des utilisateurs OAS se résume donc à : *un compte console = un compte du socle avec un rôle* ; *un opérateur atelier = un profil + un PIN à usage unique*, sans mot de passe ni e-mail. Le périmètre (lignes/zones autorisées) est porté par OAS (`PUT /api/oas/operators/{id}/scope`), pas par le socle.

---

### 4.4 Organisation du code : **un seul module `OAS`, 22 sous-modules**

Décision : tout ce qui est spécifique à l'atelier vit sous `Backend/Modules/OAS/`. Aucun nouveau module de premier niveau, aucun ajout dans les modules du socle (sauf les 4 actions génériques de `Lookups`, §4.2).

```text
Backend/Modules/OAS/
├── Common/                      # infra partagée du module (pas d'endpoint)
│   ├── OasControllerBase.cs     # [ApiController] [Route("api/oas/[controller]")] [Authorize] + tenant
│   ├── OasDbContext.cs          # schéma "oas", clés uuid, snake_case, filtre tenant global
│   ├── OasModuleRegistration.cs # AddOasModule(services) — DI de tous les sous-modules
│   ├── Realtime/OasHub.cs       # hub SignalR "oas" (groupes: site, ligne, poste, rôle)
│   └── Scope/OasScopeFilter.cs  # filtre périmètre site/zone/ligne issu de user_roles.scope_*
├── ShopFloorAuth/               # login matricule+PIN, login badge QR
├── Hierarchy/                   # sites, zones, lignes, postes
├── Equipments/
├── Cadences/                    # routings + versionnement
├── Causes/                      # arbre + propositions
├── Products/                    # produits + ordres de fabrication
├── Shifts/                      # templates, calendrier, sign-off
├── Teams/
├── Assignments/                 # affectations + présence
├── PostSessions/                # prise de poste, relais, clôture, scan
├── Declarations/                # production, rebut, correction tracée
├── Changeovers/
├── Quality/                     # gabarits de contrôle + contrôles
├── Events/                      # andon : déclaration → clôture, transitions, notifications
├── Sla/                         # règles SLA + escalades (remplace WorkflowEngine, écarté)
├── Interventions/               # maintenance (remplace SupportTickets, écarté)
├── PostStates/                  # état live + historique
├── Kpi/                         # TRS/OEE, Pareto, tendance, comparaison lignes
├── Imports/                     # imports CSV/Excel référentiels
├── Integrations/                # webhooks entrants + abonnements sortants
├── Lookups/                     # listes plates OAS (oas_lookup_values) — jamais le socle
└── Offline/                     # push/pull, receipts, device tokens
```

Chaque sous-module suit le patron déjà en place dans le dépôt (`Backend/Modules/Sync`, `Backend/Modules/Projects`) : `Controllers/`, `DTOs/`, `Models/`, `Services/` (+ `Data/` pour la configuration EF quand la table a des index ou contraintes particulières).

Règles communes à tous les sous-modules :

| Règle | Valeur |
|---|---|
| Préfixe de route | `api/oas/<ressource>` — jamais de route racine |
| Attribut de classe | `[Authorize]` + `[RequirePermission("oas.<sousmodule>.<action>")]` |
| Tenant | résolu par `TenantMiddleware` existant, jamais accepté depuis le corps de requête |
| Périmètre | `OasScopeFilter` applique site/zone/ligne avant toute requête de lecture |
| Clés | `uuid` (option C du §7) — pas de `int` pour les entités OAS |
| Soft-delete | colonne `archived_at` (référentiels) ; les faits (déclarations, événements) ne sont **jamais** supprimés |
| Temps réel | publication sur `OasHub` pour événements, états de poste et KPI uniquement |
| Idempotence | tout POST créant un fait exige `clientEventId` (uuid) — contrainte unique en base |
| Contexte EF | `OasDbContext` **uniquement** ; aucun sous-module n'injecte `ApplicationDbContext` |
| Dépendance socle | seulement via des interfaces déjà publiques (`ITokenService`, `INotificationService`, upload) — jamais via un `DbSet` du socle |
| Nommage SQL | tables préfixées `oas_` ou portées par le schéma `oas` — aucun nom en collision avec les 173 `DbSet` existants |

### 4.5 Tables — 30 réutilisées, **13 à créer** (migration `009`)

Les 33 tables de `db/migrations` couvrent l'essentiel ; 3 sont du socle (`tenants`, `profiles`, `user_roles`) et 30 sont OAS. Le croisement avec les écrans (§4.1, §4.2) révèle **12 tables manquantes** :

| # | Nouvelle table (`009_oas_gaps.sql`) | Sous-module | Raison — preuve |
|---|---|---|---|
| 1 | `cause_proposals` | Causes | proposition de cause par l'opérateur + revue chef d'équipe — `refStore.ts:312,328` ; aucune table `proposal` dans `db/migrations` |
| 2 | `routing_versions` | Cadences | cadences versionnées avec historique (`rate`, `version`, `since`) — `refStore.ts` `CadenceRow.history` ; `routings` (`003:98`) n'a **pas** de version |
| 3 | `shift_signoffs` | Shifts | validation de fin de poste — `refStore.ts:437` (`ShiftSignOff`) |
| 4 | `presence_entries` | Assignments | présence confirmée/attendue/absente par opérateur et shift — `assignmentStore.ts:19,130,148` |
| 5 | `interventions` | Interventions | file maintenance liée à un événement — `InterventionInbox.tsx` ; `SupportTickets` écarté |
| 6 | `quality_check_templates` | Quality | gabarits de contrôle (`DynamicForms` écarté) |
| 7 | `quality_check_template_items` | Quality | lignes du gabarit (type de contrôle, borne min/max, obligatoire) |
| 8 | `sla_rules` | Sla | règles éditables par type d'événement / criticité / ligne — aujourd'hui `sla_minutes` figé en défaut (`005:80`) et `routing_rules.sla_minutes` (`004:139`) |
| 9 | `escalations` | Sla | trace des escalades niveau 1/2 produites par `job_check_sla` (`008:256`) — aujourd'hui non historisées |
| 10 | `import_lines` | Imports | détail ligne à ligne + erreurs d'un import ; `imports` (`007:82`) n'a que l'en-tête |
| 11 | `integration_endpoints` | Integrations | abonnements MES/ERP sortants (`ExternalEndpoints` écarté) |
| 12 | `integration_outbox` | Integrations | file d'émission avec retry et statut de livraison |
| 13 | `oas_lookup_values` | Lookups | listes plates OAS (§4.2) — remplace la modification du `LookupsController` du socle, qui est désormais interdite (§4.7) |

**Aucune autre table n'est nécessaire.** Notamment : pas de table de PIN (`profiles.pin_hash` existe déjà, `002:34`), pas de table de lookups OAS (`LookupItems` du socle, §4.2), pas de table de pièces jointes supplémentaire (`attachments`, `007:69`), pas de table de rôles (`user_roles` du socle, scope site/zone inclus).

La migration `009` doit respecter le patron déjà utilisé : `create table` → `select public.apply_tenant_rls('<table>', '<rôles en écriture>')` (`002:150`), qui pose grants + RLS multi-tenant en un appel.

### 4.6 Récapitulatif : sous-module → tables → endpoints

| Sous-module | Tables existantes réutilisées | Tables à créer | Endpoints | Dépendance socle |
|---|---|---|---|---|
| Common | — | — | 0 | Tenants, Auth, SignalR |
| ShopFloorAuth | `profiles` (`pin_hash`), `posts.qr_token` | — | 2 | Auth, Users |
| Hierarchy | `sites`, `zones`, `lines`, `posts` | — | 22 | Lookups (`SiteType`, `ZoneType`, `PostType`) |
| Equipments | `equipments` | — | 4 | Lookups (`EquipmentType`) |
| Cadences | `routings` | `routing_versions` | 6 | Lookups (`CadenceUnit`) |
| Causes | `causes` | `cause_proposals` | 13 | — |
| Products | `products`, `production_orders` | — | 7 | Lookups (`ProductFamily`, `PackagingUnit`) |
| Shifts | `shift_templates`, `shift_calendar` | `shift_signoffs` | 8 | Signatures |
| Teams | `teams`, `team_members` | — | 3 | Users, Roles |
| Assignments | `assignments` | `presence_entries` | 12 | Users |
| Operators (dans ShopFloorAuth) | `profiles`, `user_roles` | — | 6 | Users, Roles |
| PostSessions | `post_sessions` | — | 5 | — |
| Declarations | `declarations` | — | 7 | — |
| Changeovers | `changeovers` | — | 3 | Lookups (`ChangeoverType`) |
| Quality | `quality_checks` | `quality_check_templates`, `quality_check_template_items` | 8 | Lookups (`QualityDefect`) |
| Events | `events`, `event_transitions`, `event_notifications` | — | 15 | Notifications, SignalR |
| Sla | `routing_rules` | `sla_rules`, `escalations` | 6 | Notifications |
| Interventions | — | `interventions` | 6 | Documents |
| PostStates | `post_states`, `post_state_history` | — | 2 | SignalR |
| Kpi | `kpi_daily` | — | 4 | — |
| Imports | `imports` | `import_lines` | 5 | Documents |
| Integrations | — | `integration_endpoints`, `integration_outbox` | 6 | — |
| Lookups (OAS) | — | `oas_lookup_values` | 4 | aucune |
| Offline | `sync_receipts`, `device_tokens`, `attachments`, `audit_log` | — | 8 | Sync, Documents |
| **Total** | **30** | **13** | **162** | — |

162 endpoints = somme exacte de la colonne « Endpoints » ci-dessus : 156 endpoints métier OAS + 2 endpoints d'authentification atelier + 4 actions de lookups OAS (§4.2). **Aucune de ces 162 actions ne touche un module existant** — le socle reste à 993 actions, inchangé. Ce chiffre remplace le « 158 + 4 dans le socle » de la v3.

---

### 4.7 Isolation stricte : OAS ne doit jamais perturber les autres modules

Contrainte produit : le backend `MyApi` fait tourner une application existante en production. OAS y est ajouté **en greffe**, avec une frontière vérifiable. Règle générale : *aucun fichier hors de `Backend/Modules/OAS/` n'est modifié, à la seule exception de 3 lignes d'enregistrement dans `Program.cs`.*

#### Les 3 seules lignes touchées hors du module

```csharp
builder.Services.AddOasModule(builder.Configuration);   // DI + OasDbContext
app.MapHub<OasHub>("/hubs/oas");                        // hub dédié
await app.Services.RunOasMigrationsAsync();             // runner de migrations OAS
```

Tout le reste (contrôleurs, entités, services, migrations, seed, permissions) vit sous `Backend/Modules/OAS/`. Si OAS est supprimé, il suffit d'effacer le dossier et ces 3 lignes : l'application d'origine redevient identique.

#### Frontières par couche

| Couche | Frontière OAS | Ce qui est interdit |
|---|---|---|
| Routes HTTP | préfixe unique `api/oas/*` | créer/modifier une route hors `api/oas` ; ajouter une action à un contrôleur existant |
| EF Core | `OasDbContext` séparé, ses propres `DbSet`, `HasDefaultSchema("oas")` | ajouter un `DbSet` à `ApplicationDbContext` ; ajouter une navigation depuis une entité du socle |
| Base de données | schéma `oas` (ou préfixe `oas_`), migration `009` livrée **dans** le module (`Backend/Modules/OAS/Data/Migrations/`) | `ALTER TABLE` sur une table du socle ; FK physique vers `Users`, `LookupItems`, `Documents` |
| Lien identité | table de correspondance OAS `oas_user_links (user_id int, profile_id uuid)`, alimentée par OAS | ajouter une colonne à `Users` ou `profiles` du socle |
| Permissions | clés préfixées `oas.*` dans le RBAC existant (données, pas code) | modifier `RequirePermissionAttribute` ou le `PermissionService` |
| Temps réel | hub `/hubs/oas` | publier sur le hub workflow ou un hub existant |
| Jobs | scheduler OAS interne (`OasSlaWorker`, `IHostedService` du module) | brancher un job dans le scheduler existant |
| Fichiers | appelle `/api/documents` **en client HTTP interne ou via l'interface publique du service** | écrire dans les tables `Documents` |
| Config | section `Oas:` dans `appsettings` + variables `Oas__*` | changer une clé de configuration existante |
| Swagger | `SwaggerDoc("oas", …)` séparé | modifier le document Swagger par défaut |

#### Couplage identité, sans toucher au socle

`Users.Id` est un `int`, `profiles.id` un `uuid`. OAS ne modifie ni l'un ni l'autre : il possède `oas_user_links`, remplie à la première connexion console (le JWT du socle porte déjà l'`userId`). Un opérateur atelier existe **uniquement** côté OAS (`profiles` + `pin_hash`) et n'est jamais créé dans `Users` : la connexion atelier émet un JWT signé avec la même clé mais un `aud` distinct (`oas-shopfloor`) et un claim `oas_scope`, refusé par les contrôleurs du socle via leur `[RequirePermission]` existant (aucune permission socle ne lui est attribuée).

#### Garde-fous automatiques (à mettre en place au lot 1)

| Garde-fou | Mise en œuvre | Effet |
|---|---|---|
| Test d'architecture | test xUnit : aucun type sous `MyApi.Modules.OAS` ne référence `ApplicationDbContext`, et aucun type hors OAS ne référence un type OAS | échec de build en cas de fuite |
| Test de routes | inventaire des routes au démarrage : toute route OAS doit commencer par `api/oas/` | échec de test |
| Diff de contrat | `scripts/inventory_controllers.py` rejoué en CI : le socle doit rester à **95 fichiers / 993 actions** | détecte toute action ajoutée à un module existant |
| Diff de schéma | comparaison des tables hors schéma `oas` avant/après migration `009` | détecte tout `ALTER` sur le socle |
| Kill-switch | OAS enregistré comme plugin (`activated_modules`) ; désactivé = routes `api/oas/*` renvoient 404 | retour arrière sans redéploiement |

---

## 5. Données — inventaire exact

### 5.1 Schéma OAS `db/migrations/` — 33 tables, 18 enums

| Fichier | Tables créées |
|---|---|
| `001_extensions_enums.sql` | 0 — extension `pgcrypto` (`:6`), 18 `CREATE TYPE` (`:12-103`) : `app_role, post_state, event_type, event_status, closure_type, escalation_level, declaration_kind, shift_code, criticality, cause_domain, order_status, check_type, check_result, notify_channel, notify_response, audit_action, oee_mode, scope_type` |
| `002_tenants_identity.sql` | 3 — `tenants` (`:8`), `profiles` (`:27`, dont `pin_hash`, `biometric_enrolled`), `user_roles` (`:51`, scope site/zone) |
| `003_reference_hierarchy.sql` | 8 — `sites`, `zones`, `lines`, `posts` (`:46`, `qr_token`, `target_oee`), `equipments`, `products`, `routings`, `production_orders` |
| `004_shifts_assignments_causes.sql` | 8 — `shift_templates`, `shift_calendar`, `teams`, `team_members`, `assignments`, `post_sessions`, `causes`, `routing_rules` |
| `005_declarations_events.sql` | 6 — `declarations` (`:9`), `events` (`:43`), `event_transitions` (`:113`), `event_notifications` (`:131`), `changeovers` (`:154`), `quality_checks` (`:180`) |
| `006_post_states_kpi.sql` | 3 — `post_states`, `post_state_history`, `kpi_daily` |
| `007_audit_offline.sql` | 5 — `audit_log`, `sync_receipts`, `device_tokens`, `attachments`, `imports` |
| `008_triggers_jobs.sql` | 0 — 10 fonctions + 10 triggers (+1 trigger d'audit créé dynamiquement) : `declarations_immutable`, `declarations_mark_corrected`, `declarations_order_progress`, `events_apply_sla`, `events_track_status`, `events_log_transition`, `recompute_post_state`, `trg_recompute_post_state`, `audit_row`, `job_check_sla` (`:6-256`) |

Total schéma : **17 fonctions** (7 dans `002` : RLS/tenant/`apply_tenant_rls`) et **14 triggers** (10 dans `008`, 2 dans `002`, 1 dans `003`, 1 dans `004`).

**Ce schéma est déjà complet pour OAS.** La logique métier sensible (immutabilité des déclarations, SLA, recalcul d'état de poste, audit) est **dans les triggers Postgres**, pas dans le code applicatif.

### 5.2 Modèle backend .NET — 173 tables

Aucun recouvrement de nom avec le schéma OAS. Tables pertinentes pour la réutilisation identité/transverse : `Tenants`, `Users`, rôles/permissions, `Notifications`, `LookupItems`, `activated_modules`, `hr_attendance`, `Documents`, `WorkflowDefinitions`/`WorkflowExecutions`/`WorkflowTriggers`.

Conventions : PK `int`, `BaseEntity{Id, CreatedAt, UpdatedAt, CreatedBy, ModifiedBy}` (`Backend/Modules/Shared/Domain/Common/BaseEntity.cs:6-31`), soft-delete par colonne `IsDeleted` **sans filtre EF global** (à filtrer manuellement dans chaque requête — 108 fichiers concernés, source de bugs).

---

## 6. Écarts frontend → backend (matrice de couverture)

| Capacité requise par `src/` | Couverte par l'existant | Endpoint |
|---|---|---|
| Login e-mail/mot de passe console | ✅ | `POST /api/auth/user-login` |
| Login matricule + PIN | ❌ | à créer |
| Login par scan QR poste | ❌ | à créer |
| Annuaire opérateurs (CRUD, actif/inactif, rôle) | ✅ | `/api/users` (12 actions) |
| Rôles & permissions, périmètre lignes | ⚠️ partiel | `/api/roles`, `/api/permissions` — le scope site/zone (`user_roles.scope_*`) est à ajouter |
| Hiérarchie site/zone/ligne/poste | ❌ | à créer |
| Équipements, cadences versionnées | ❌ | à créer |
| Arbre de causes + propositions | ❌ | entité OAS `causes` + `cause_proposals` (le module `Lookups` du socle n'est ni étendu ni écrit — §4.7) |
| Produits / OF | ❌ | `Articles` écarté → `products` + `production_orders` à créer dans OAS |
| Shifts, calendrier, sign-off | ⚠️ | `Signatures` réutilisable ; `Calendar` écarté (modèle rendez-vous) → tables shifts + calendrier 3×8 à créer |
| Affectations & présence | ❌ | `HR` écarté → affectation poste **et** présence de poste à créer dans OAS |
| Sessions de poste (ouverture/relais/clôture) | ❌ | à créer |
| Déclarations production/rebut + correction tracée | ❌ | à créer |
| Événements andon + escalade SLA | ❌ | WorkflowEngine écarté → entité `events`, machine à états et moteur SLA entièrement à écrire dans OAS |
| État live des postes | ❌ | à créer (triggers SQL déjà écrits) |
| KPI / OEE / Pareto / tendance | ❌ | à créer |
| Imports en masse | ❌ | à créer |
| Journal d'audit | ❌ | `SystemLogs`/`Logs` du socle laissés intacts ; `audit_log` OAS (trigger `audit_row`) à créer |
| Activation des modules | ✅ | `/api/plugins` (13 actions) |
| File offline / idempotence | ❌ | `api/oas/sync/*` à écrire dans `OAS/Offline` (`clientEventId` + `sync_receipts`) ; `/api/sync` du socle reste intact |
| Notifications | ✅ | `/api/notifications` (8 actions) |
| Upload de pièces jointes | ✅ | `/api/upload`, `/api/documents` |
| Temps réel (tableau andon, TV) | ⚠️ | Infra SignalR déjà câblée dans `Program.cs` ; hub `oas` propre à ajouter (le hub workflow n'est pas réutilisé) |

---

## 7. Décision d'architecture — **tranchée : monolithe, module isolé**

**Verdict : option A (monolithe), avec la frontière technique de l'option C.** OAS vit dans l'application `MyApi` existante — un seul déploiement, une seule image, une seule base — mais dans un module hermétique (§4.7) : schéma Postgres `oas`, `OasDbContext` dédié, clés `uuid`, migrations livrées dans le module, routes `api/oas/*`, hub `/hubs/oas`.

| Point tranché | Décision |
|---|---|
| Déploiement | un seul (pas de service séparé) |
| Contexte EF | `OasDbContext` séparé, même `DbConnection`/transaction possible mais schéma distinct |
| Clés primaires | `uuid` côté OAS, `int` côté socle — jamais de FK physique entre les deux (`oas_user_links` fait le pont) |
| Logique métier | on **garde** les triggers SQL de `db/migrations` (immutabilité, SLA, recalcul d'état, audit) ; ils sont rejoués par le runner de migrations OAS, pas réécrits en C# |
| Migrations | `db/migrations/001..009` déplacées/copiées sous `Backend/Modules/OAS/Data/Migrations/`, appliquées par `RunOasMigrationsAsync()` avec table de suivi `oas.schema_migrations` |
| Soft-delete | filtre global `archived_at is null` déclaré dans `OasDbContext` (le défaut manquant du socle n'est pas hérité) |
| Dettes du socle | JWT 10 ans, CORS `*`, mot de passe maître : traités au lot 0 **avant** branchement — OAS ne les contourne pas et n'ajoute pas les siennes |

Options écartées : **B (service séparé)** — deuxième déploiement, SSO et couche tenant à dupliquer, sans bénéfice à ce volume ; **C pur (deux applications)** — même coût sans le gain d'isolation, déjà obtenu par §4.7.

**[NON VÉRIFIÉ]** Le schéma `db/migrations` est-il déjà appliqué sur une base réelle ? Impossible à déterminer depuis les fichiers ; à confirmer avec un `\dt` sur la base cible.

---

## 8. Risques de sécurité confirmés (avec preuves)

| # | Risque | Preuve | Gravité |
|---|---|---|---|
| 1 | Mot de passe maître codé en dur permettant de se connecter à **n'importe quel compte** — `MASTER_LOGIN_PASSWORD`, défaut `"Admin@2026@"` ; le commentaire du code dit lui-même « Remove before going to production » | déclaration `Backend/Modules/Auth/Services/AuthService.cs:1029-1030`, bypass `:1040-1045` (dans `VerifyPassword`, donc **tous** les chemins de login) | **Critique** |
| 2 | JWT à **10 ans** avec `ValidateLifetime = false` : un token volé ne peut pas expirer | `AuthService.cs:995-997` et `:1208-1210` ; `Program.cs:283` | **Critique** |
| 3 | Clé JWT par défaut committée (`appsettings.json:3`) **et** repli codé en dur `"YourSuperSecretKeyHere12345"` en 4 endroits si la config est absente — aucun échec au démarrage | `Program.cs:276`, `Configuration/TokenHelper.cs:12,41`, `AuthService.cs:975,1184` | **Critique** |
| 4 | Chaîne de connexion Postgres (Neon) avec identifiants réels committée (`neondb_owner:npg_…`) | `Backend/appsettings.Development.json:3` | **Critique** |
| 5 | Configuration de déploiement absente : `render.yaml` **ne contient que des commentaires** (aucun bloc `services:`/`envVars:`) et documente `JWT_KEY`, alors qu'ASP.NET lit `Jwt:Key` (soit `Jwt__Key` en variable d'environnement). Aucun code ne traduit `JWT_KEY` → risque que la prod signe avec le repli du point 3 | `render.yaml:9` vs `Program.cs:276` | **Critique** |
| 6 | CORS `AllowAnyOrigin` + filet inline `Access-Control-Allow-Origin: *` sur `/api` | `Program.cs:498-523`, `:1376-1398` | Élevée |
| 7 | Aucun rate limiting sur `/api/auth/login` (bruteforce PIN à 4 chiffres = 10 000 combinaisons) | **0 occurrence** de `AddRateLimiter` dans le dépôt ; seul compteur applicatif : webhooks publics 60/min (`Modules/ExternalEndpoints/Services/ExternalEndpointService.cs:397-415`) | Élevée |
| 8 | PIN opérateur stocké **en clair** dans `localStorage` côté client | `src/oas/refStore.ts:212-215,429-434` | Élevée |
| 9 | Mot de passe console codé en dur `secret123` | `src/oas/authStore.ts:79` | Élevée |
| 10 | Soft-delete non appliqué par filtre global : tout oubli de `!IsDeleted` expose des données supprimées | `ApplicationDbContext.cs:432,448` — seulement 2 `HasQueryFilter`, tous deux sur le tenant | Moyenne |

Les points 1 à 5 doivent être traités **avant** tout branchement du frontend OAS.

---

## 9. Feuille de route

Tout le code produit par les lots 1 à 7 est écrit **dans `Backend/Modules/OAS/<SousModule>/`** (§4.4). **Aucun lot ne modifie un module existant** : le lot 0 corrige des failles du socle déjà présentes (indépendamment d'OAS), et l'unique point de contact est constitué des 3 lignes d'enregistrement de `Program.cs` (§4.7).

| Lot | Contenu | Dépendances | Livrable |
|---|---|---|---|
| **0 — Sécurisation** | Retirer le mot de passe maître, ramener le JWT à ≤ 8 h + refresh réel, sortir clés/chaînes de connexion des fichiers committés, corriger le nommage des variables Render, restreindre CORS, rate-limit sur `/auth/*` | — | Backend sain |
| **1 — Fondation OAS + client** | `Backend/Modules/OAS/Common` (OasDbContext, base de contrôleur, scope, hub, runner de migrations), garde-fous d'isolation §4.7 (tests d'architecture, diff de contrat en CI), client HTTP frontend (base URL, `Authorization` + `X-Tenant`, gestion 401/428) | Lot 0 | Module vide déployable + login réel |
| **2 — Identité atelier** | `pin-login` (PIN haché), `badge-login` (QR poste), mapping `Users` ↔ `profiles`, scope site/zone sur les rôles | Lot 1 | Login opérateur |
| **3 — Référentiels** | Sous-modules Hierarchy, Equipments, Cadences, Causes, Products, Shifts, Imports, **Lookups OAS** ; bascule de `refStore`/`hierarchyStore` et suppression des 7 constantes en dur | Lot 2 | Console référentiels branchée |
| **4 — Exploitation** | PostSessions, Assignments/Presence, Declarations (avec correction tracée) ; bascule de `session.ts`/`assignmentStore` | Lot 3 | Déclarations réelles |
| **5 — Andon** | Events + transitions + moteur SLA **propre à OAS**, hub SignalR `oas`, PostStates live ; bascule de `eventStore` | Lot 4 | Andon temps réel |
| **6 — Offline** | `api/oas/sync/*` (idempotence `clientEventId`, `sync_receipts`, device tokens), remplacement de la simulation `setTimeout` | Lot 4 | File offline réelle |
| **7 — KPI & audit** | Agrégats `kpi_daily`, Pareto, tendance, comparaison lignes ; `audit_log` ; sign-off de fin de poste | Lot 5 | Dashboards & rapports |

---

## 10. Annexes

- `docs/ANNEXE-INVENTAIRE-CONTROLEURS.md` — les **95 fichiers contrôleurs / 993 actions**, verbe, template de route, méthode, autorisation de classe et d'action, avec numéro de ligne exact.
- `scripts/inventory_controllers.py` — générateur de l'annexe (`--markdown`) ou export brut (`--json`). Tous les chiffres de ce document en sont issus et sont donc reproductibles.
- `docs/01-SPEC-FRONTEND.md`, `docs/02-SPEC-DATABASE.md`, `docs/03-BACKLOG-TRACEABILITY.md` — spécifications produit.
- `db/migrations/001..008` — schéma OAS de référence (33 tables, 18 enums, 17 fonctions, 14 triggers).
