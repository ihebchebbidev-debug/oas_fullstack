# PROMPT DE BUILD — Backend OAS (module isolé dans `MyApi`)

> **Usage.** Copier-coller ce fichier entier comme prompt système/initial à un agent de développement (Cursor, Claude Code, Copilot Workspace, Lovable…). Il est **auto-portant** : il contient le contexte, l'architecture, le schéma, la liste exhaustive des 184 endpoints, la cartographie des 22 écrans, les formules KPI, les règles d'isolation, les critères d'acceptation et l'ordre d'exécution. Aucun autre document n'est requis.
>
> Version : **v16** — 2026. Source de vérité longue : `docs/04-BACKEND-REUSE-PLAN.md`. Chiffres reproductibles via `python3 scripts/inventory_controllers.py`.
>
> **Nouveautés v16** (contradiction de portée corrigée) : §12 règle 2 interdisait littéralement tout fichier hors `Backend/Modules/OAS/`, ce qui aurait fait refuser par l'agent la quasi-totalité des tâches frontend que la feuille de route §10 lui assigne explicitement à chaque lot — contredisant aussi le critère d'acceptation §11.4, qui autorise déjà `docs/`, `scripts/` et `src/` dans le diff. Corrigé pour autoriser explicitement ces trois répertoires, en plus de `Backend/Modules/OAS/` et des 3 lignes `Program.cs` — le socle backend (tout `Backend/Modules/*` hors `OAS`) reste strictement intouchable. Aucun changement de total : **184 endpoints uniques, 18 tables créées**.
>
> **Nouveautés v15** (passe finale de correction logique — pas de recherche de fonctionnalités manquantes ni d'endpoints, recherche de **bugs de calcul et de machine à états** dans le code existant que le backend copierait fidèlement à tort) : cette passe distingue explicitement, pour la première fois, ce que le backend doit **porter à l'identique** (§7.3, comme avant) de ce qu'il doit **corriger délibérément** en s'écartant du comportement client actuel. Deux décisions produit tranchées : **archivage en cascade** (archiver un site/zone/ligne archive ses descendants dans la même transaction — le client actuel n'archive que l'entité ciblée, laissant des enfants actifs sous un parent mort) ; **publication d'affectation par poste** (modifier un poste ne dépublie plus que ce poste — le client actuel utilise un drapeau `publishedAt` global, si bien qu'éditer un seul poste dépublie tout le plan du jour). Bugs corrigés sans être une question de préférence produit (données ou sécurité en jeu, aucune ambiguïté) : (1) **perte de données côté mobile, la plus grave** — `openSession()` écrase silencieusement une session active dès qu'un second scan survient sur le même shift (`ScanPage.tsx` n'a aucune garde contrairement aux autres écrans mobiles), effaçant des déclarations non synchronisées sans confirmation ni `clientEventId` jamais émis ; toute clôture de session, automatique **ou** manuelle, vide la file sans flush préalable (`closeSession` → `commit(EMPTY)`) ; la fenêtre de grâce de fin de shift se mesure depuis l'ouverture de la session et non depuis l'heure de fin réelle du shift, déclenchant la clôture automatique dans la minute suivant le changement de shift au lieu d'~15 min plus tard — les trois corrigés au niveau du service `SessionWatchdogHostedService` et de `POST /post-sessions/open` (§6.3, §6.1) ; (2) **identité de disponibilité fictive** — le mobile utilise une constante en dur (`CURRENT_RESPONDER = 'Karim T.'`) au lieu de l'opérateur réellement connecté pour le drapeau « occupé », faisant à tort passer en escalade des événements assignés à des intervenants différents partageant le même nom générique côté client — le serveur dérive systématiquement l'identité du JWT authentifié ; (3) **machine à états des événements andon non gardée** — un événement `resolved` peut être rouvert par `requalify` sans garde, `close` n'a aucune précondition (saut direct `declared`→`closed` possible), et l'escalade manuelle n'écrit jamais d'audit contrairement à ses 5 actions voisines — le serveur refuse en 409 toute transition invalide et audite les 13 actions sans exception ; (4) **arithmétique SLA** — le temps écoulé est arrondi au lieu d'être tronqué vers le bas (escalade jusqu'à ~29 s en avance sur l'échéance réelle), et l'escalade peut sauter directement à N2 sans jamais passer par N1 si le premier balayage survient tardivement (onglet en arrière-plan) — le serveur tronque et force une progression séquentielle ; (5) **`closeStop` non idempotent** — fermer un arrêt déjà fermé écrase l'horodatage et gonfle sa durée dans le Pareto/MTTR au lieu de renvoyer 200 sans effet ; (6) **correction de déclaration sans vérification d'auteur** — un relais d'opérateur permet à l'entrant de corriger les déclarations du sortant, le serveur doit dériver l'auteur du JWT ayant émis le POST original, jamais d'un champ client ; (7) **quatre bugs de formule mineurs** — MTTR affiche 1 min au lieu de 0 quand il n'y a aucun arrêt ; un shift `début == fin` devient silencieusement un shift de 24 h au lieu de 0 ; le ratio de complétude « causes cartographiées » est tautologiquement toujours 100 % (filtre sur un champ obligatoire) ; le ratio « couverture des 3 shifts » divise par une constante `3` en dur, jamais clampé, et peut afficher plus de 100 % ; la comparaison de lignes moyenne à parts égales une TRS de démo statique avec la TRS live, un artefact du jeu de données de démo à ne pas porter. Aucun de ces points ne change le total d'endpoints ou de tables : **184 endpoints uniques, 18 tables créées** (inchangé).
>
> **Nouveautés v14** (passe de cohérence arithmétique finale — recomptage littéral ligne par ligne des 44 domaines du catalogue §6.1, recoupé avec la somme des 24 lignes de sous-module §6) : (1) **écart de comptage trouvé et corrigé, présent depuis v6** — le sous-module `ShopFloorAuth` déclarait **2** endpoints depuis toujours, alors que le domaine catalogue « Auth OAS » qu'il porte en compte **8** (`setup`, `login` console, `refresh`, `logout`, `me`, `change-password`, `shopfloor/login`, `pin/regenerate`) : les 6 routes d'auth console ont été ajoutées au catalogue à un moment donné sans jamais être créditées à une ligne de sous-module, sous-comptant le total affiché de **6** à chaque version depuis v6 (172) jusqu'à v13 (178) sans être détecté malgré les passes de vérification successives ; (2) **total réel : 184** (et non 178) — vérifié deux fois indépendamment : somme directe des 44 domaines du catalogue, et somme des 24 lignes de sous-module une fois `ShopFloorAuth` corrigé — les deux convergent ; (3) **`Admin.tsx` (§1.5, ligne `/web/admin`) mise à jour** : ne listait pas encore `GET /posts/qr-tokens?lineId=` (ajouté en v13) alors que c'est précisément l'écran qui en a besoin — violation du contrat §6.2 règle 1 pendant une version, maintenant corrigée ; (4) **second écart trouvé, sans rapport avec les endpoints** : la ligne « Totaux » de chaque changelog depuis v9 affichait **17 tables créées**, alors que §5.2 énumère explicitement **18** tables numérotées (`oas_cause_proposals` … `oas_users` #18) et que le tableau §6 l'affiche déjà correctement à 18 dans sa colonne « Tables créées » — la ligne récapitulative de fin de changelog n'avait simplement jamais été recomptée contre l'énumération réelle. Cette passe ne change **aucune** décision produit ni n'ajoute de route ou de table fonctionnelle : c'est une correction d'arithmétique et de contrat interne uniquement. Totaux corrects : **184 endpoints uniques, 18 tables créées**.
>
> **Nouveautés v13** (réconciliation finale écran ↔ endpoint, 8e passe — chaque opération front vérifiée contre le catalogue §6.1) : (1) **3 endpoints manquants ajoutés** → **178 endpoints** : `GET /kpi/sla-summary?from=&to=&scope=` (`Reports.tsx:90-103`, `slaByService`, agrégat non couvert par §7.3 jusqu'ici), `GET /kpi/cadence-gap?from=&to=&scope=` (`Reports.tsx:110-124`, `cadenceGap`), `GET /posts/qr-tokens?lineId=` (variante groupée pour la vue d'impression de badges, `Admin.tsx:92-114`, qui appelait sinon `GET /posts/{id}/qr-token` en boucle) ; (2) **paramètres manquants ajoutés à des routes existantes** (aucun nouvel endpoint) : `GET /events` gagne `kind=&stage=&q=&lineId=` (`AlertsQueue.tsx:25-47`, `ShopFloorBoard.tsx:18`) ; `GET /audit` gagne `actor=&action=&q=` (`Admin.tsx:37-69`) ; les 4 routes `GET /kpi/*` et `GET /declarations` gagnent `from=&to=` (`Reports.tsx:19-53`, période sélectionnable) ; `GET /operators` gagne `q=&scope=` (recherche dans le sélecteur d'affectation, `Assignments.tsx:105-118`, `RosterPanel.tsx:39-41`) ; `GET/PUT /andon/message` accepte `lineId` **optionnel** (message de portée site, `AndonTv.tsx:15,30,110-114`, aucune association de ligne aujourd'hui) ; `GET /changeovers` gagne `postId=&status=` (reprise après navigation, appuyé par l'index unique existant `uq_open_changeover` sur `(tenant_id, post_id) WHERE ended_at IS NULL`, `005:173-174` — au plus une ligne possible, filtre trivial) ; (3) **`oas_changeovers` doit gagner une colonne `steps jsonb`** — la checklist en 5 étapes (`ChangeoverPage.tsx:25`, `done: boolean[]`) n'est aujourd'hui persistée nulle part, ni côté client ni dans la table `005:154-171` (vérifiée colonne par colonne, aucun champ candidat) ; sans elle, la reprise après navigation sait qu'un changement de série est ouvert mais pas quelles étapes sont déjà cochées ; (4) **corrections de citations internes au prompt** : la ligne `/mobile/home` (§1.5) citait `GET /assignments?shift=&date=` pour « opérateur publié » — c'est la ressource **brouillon** (modifiable par `PUT/DELETE /assignments/{postId}`), la bonne route déjà présente au catalogue est `GET /assignments/published?shift=&date=` (`OperatorHome.tsx:60`, `ScanPage.tsx:41,52`, `assignmentStore.publishedOperator` gated sur `publishedAt`) ; la même ligne citait `GET /shifts/calendar` pour l'alerte de fin de poste alors que `getShift()`/`shiftEndedFor()` (`session.ts:22-27,293-298`) n'utilisent que les horaires statiques d'un shift, pas une plage de dates — `GET /shifts` (brute) est la bonne route, `/shifts/calendar` reste utile pour autre chose sur cet écran mais n'est plus la seule citée ; (5) **lacunes frontend confirmées, à construire pendant les lots concernés, pas de nouvel endpoint** : `OperatorSession` (`session.ts:39`) ne porte qu'un nom (`operator: string`), jamais d'`operatorId` — `openSession`/`switchOperator` doivent le véhiculer pour pouvoir appeler les routes indexées par id (`PUT /presence/{operatorId}`, `POST /operators/{id}/...`) ; `eventStore.addEvent()` (`eventStore.ts:106-109`) mint un id local séquentiel (`live-${seq}`) non-UUID — source commune à `declareStop` (DeclareStop), `NeighborStop.pick` **et** désormais confirmée comme la seule source d'id pour `POST /events`, à corriger avant idempotence réelle (élargit la note déjà posée au lot 5) ; `POST /post-sessions/open|relay|close` (`session.ts:194,236,269`) n'ont **aucun** `clientEventId` généré côté client aujourd'hui, alors que l'app simule déjà des retries réseau (`attemptFlush`, `session.ts:502-523`) qui les rejoueraient sans déduplication possible côté serveur — lacune nouvelle, ajoutée au lot 4 ; `declineEvent()` (`eventStore.ts:258-268`, bouton « occupé » de `InterventionInbox.tsx:115`) fait en un seul appel ce que le catalogue modélise en deux routes (`/decline` + `/escalate`) — `POST /events/{id}/decline` doit appliquer lui-même le même effet d'escalade, sinon le client doit enchaîner les deux appels : à trancher au lot 5, pas une nouvelle route. Totaux : **178 endpoints uniques, 17 tables créées** (inchangé — les 3 ajouts sont des routes, pas des tables).
>
> **Nouveautés v12** (audit croisé mobile + web, 7e passe — corrections et décisions produit) : (1) **erreur corrigée** — la fenêtre de correction d'une déclaration était annoncée à **24 h** en trois endroits (§1.5, §6.1) ; le code (`session.ts:366`, `CORRECTION_WINDOW_MIN`), le commentaire métier `BL-045` et les 3 fichiers de traduction (`en/fr/ar.ts`) disent tous **10 minutes**, sans ambiguïté : le prompt v10/v11 s'était trompé sur ce point malgré la discipline de vérification revendiquée — corrigé partout, fenêtre confirmée à **10 min** (décision produit) ; (2) **PIN jamais renvoyé en liste** — `GET /operators` ne doit **jamais** exposer le champ `pin` en clair (contrairement au comportement actuel de `UsersPanel.tsx`, qui l'affiche en permanence dans le tableau) ; seul `POST /operators/{id}/regenerate-pin` le renvoie, une seule fois, comme `password_hash` (décision produit, §8.1/§8.2) ; (3) **checklist de changement de série imposée côté serveur** — `PUT /changeovers/{id}/finish` refuse si les étapes ne sont pas complètes, fermant le contournement actuel (`OperatorHome.tsx` peut clore n'importe quel arrêt ouvert via « reprendre production », y compris un changement de série non terminé) (décision produit) ; (4) **historique mobile confirmé borné à la session** — `GET /declarations`/`GET /events` depuis `/mobile/history` restent filtrés par session/date, sans régression vers un historique permanent (décision produit, comportement actuel conservé) ; (5) **connexion : UX asynchrone désormais obligatoire** — `PinPanel`, `ScanPanel`, `WebLogin`, `LoginForm` n'ont aujourd'hui aucun état de chargement, timeout ou distinction d'erreur (PIN invalide vs hors-ligne vs serveur indisponible vs compte révoqué) ; la sync JIT (§8.3, v11) introduit un aller-retour réseau réel dans le login, ce qui rend cette lacune bloquante, pas cosmétique — travail frontend explicite ajouté au lot 2 ; (6) **infrastructure d'idempotence incomplète côté client** — la règle « tout POST mobile porte `clientEventId` » (§6.2 règle 3) suppose une file hors-ligne qui n'existe aujourd'hui que pour `declarations`/`stops` (`session.ts`) ; `NeighborStop.tsx` et tout `InterventionInbox.tsx` écrivent en synchrone sans drapeau `synced`, sans file de retry et avec des identifiants locaux séquentiels non-UUID — un opérateur hors-ligne peut voir « alerte envoyée » alors qu'elle est perdue ; travail frontend explicite ajouté au lot 5 ; (7) **risque de séquencement documenté** — `oas_sites/zones/lines/posts` et `oas_shift_templates` sont bien la source unique côté serveur, mais aujourd'hui la carte atelier (`ShopFloorMap`/`AndonTv`/`liveState.ts`), le tableau d'affectations (`BOARD_POSTS`) et les écrans `Admin`/`ShiftReport` lisent chacun une fixture figée différente de celle éditée en Référentiels ; comme leur bascule est répartie sur les lots 3/4/5/7, une fenêtre existe où un admin modifie un poste réel sans effet visible sur le terrain — à ne pas confondre avec une régression pendant le rollout ; (8) **import : rejeter, ne pas ignorer silencieusement** — `POST /imports/{id}/commit` doit renvoyer 400 pour un type de jeu de données non supporté, au lieu de reproduire le comportement actuel de `ImportPanel.tsx` (accepte et compte les lignes d'un import `posts`/`orders` sans rien écrire) ; (9) **audit des activations de plugin** — `oas_plugin_activations` doit alimenter `oas_audit_log` à chaque bascule (activation, désactivation, cascade, reset), ce qu'`activationStore.ts` actuel ne fait pour aucune de ces actions. Totaux inchangés : **175 endpoints uniques, 17 tables créées**.
>
> **Nouveautés v11** (décision produit — suppression du délai de synchronisation) : la synchronisation identité (§3.3, §8.3) passe d'un `IHostedService` périodique (`Oas:UserSyncIntervalSeconds`, défaut 60 s) à une synchronisation **à la demande (JIT)** exécutée dans le flux de connexion lui-même (`POST /api/oas/auth/login`, `POST /api/oas/shopfloor/login`, `POST /api/oas/auth/refresh`) : un utilisateur nouvellement éligible OAS côté socle peut se connecter **dès sa première tentative**, sans attendre un cycle de fond, et un utilisateur désactivé côté socle est bloqué **dès sa prochaine tentative de connexion ou de refresh**, sans fenêtre de 60 s. `OasUserSyncHostedService` est supprimé du plan ; `OasUserJitSyncService` (scoped, invoqué depuis `Auth`/`ShopFloorAuth`) le remplace. Bénéfice secondaire : élimine le risque de double exécution du polling sur plusieurs instances (§13 nouveau point). Totaux inchangés : **175 endpoints uniques, 17 tables créées**.
>
> **Nouveautés v10** (6e passe — audit croisé mobile / console / backend .NET / cohérence interne, aucune supposition) : (1) **contradiction corrigée** — `POST /api/oas/shopfloor/badge-login` subsistait en §8.1 alors que v9 l'avait supprimé partout ailleurs : la ligne devient le mode `{mode:"qr"}` de `POST /shopfloor/login` (sinon Swagger afficherait 176 actions et le critère 8 échouerait) ; (2) **formule KPI fausse corrigée** — §7.3 « Performance » décrivait un ratio de cadence qui **n'existe pas** dans le code : `liveState.ts:104` est `const performance = KPI.performance;`, une constante de démonstration (`demo.ts:136`, valeur `87`) ; la vraie formule est à définir côté serveur (§13, point ouvert) ; (3) **total « Tables réutilisées » corrigé** 30 → **32** (comptage des noms `oas_*` distincts du tableau §6) et `oas_tenants` déclarée **volontairement sans CRUD** (résolue par le routage §1.2 bis) ; (4) **Lot 8 ajouté** à §10 : il était référencé deux fois (retrait de `fixtures.ts` du bundle) sans exister dans le tableau ; (5) **§1.5 complétée** avec 5 actions mutantes réellement appelées par les écrans mais absentes de leur ligne : `POST /cause-proposals` (`DeclareStop.tsx:54`), `POST /declarations/stop/{clientEventId}/close` (`DeclareProduction.tsx:92`), `PUT /declarations/{id}/correct` (`HistoryPage.tsx:63`), `POST /events/{id}/ack` + `/escalate` (`ShopFloorBoard.tsx:99,105`), routes `Operators` explicites pour `/web/admin` (`UsersPanel.tsx:25,82,102,125,128`) ; (6) **route non littérale corrigée** : ligne Interventions du catalogue préfixée `/interventions/{id}/…` ; (7) 4 divergences écran↔domaine consignées en §13 (Changeovers, Interventions vs Events, `POST /shift-signoffs` côté mobile, rotation de badge QR sans déclencheur UI). Totaux inchangés : **175 endpoints uniques, 17 tables créées**.
>
> **Nouveautés v9** (5e passe, catalogue rendu littéral) : (1) **fin des raccourcis « + CRUD »** — les lignes Qualité, SLA, Imports, Intégrations, Cadences, Déclarations, Affectations et Hiérarchie listent désormais chaque route nommément ; le catalogue §6.1 contient **exactement 175 routes uniques**, égales au total du tableau §6 (vérifiable en comptant les back-ticks) ; (2) `POST /shopfloor/badge-login` **supprimé** : il n'a jamais existé, le login atelier est un endpoint unique à corps polymorphe `{mode:"pin"|"qr"}` ; (3) §1.5 : registre de plugins corrigé à **15 manifestes** avec `isCore` et graphe de dépendances, plus les deux règles de cascade à reproduire côté serveur (`activationStore.ts:59-88`) ; (4) fait corrigé : un **service worker d'app-shell existe** (`public/sw.js`, `main.tsx:23-27`) — il ne synchronise aucune donnée ; (5) §7.1 complétée avec `KPI`, `PARETO`, `LINE_COMPARISON`, `EVENTS`, `kindToState` (`demo.ts:109,133,152,160,166`) et **statut explicite de `fixtures.ts`** : seed serveur pour tenants `dev*`/`demo*`, supprimé du bundle au lot 8.
>
> **Nouveautés v8** (4e passe de vérification, écran par écran) : (1) **3 endpoints manquants ajoutés** — `GET /posts/{id}/qr-token` et `POST /posts/{id}/qr-token/rotate` (badges QR de poste générés dans la console, `Admin.tsx:3` `QRCodeSVG`) et `GET /referentials/completeness` (panneau de complétude, `refStore.ts:514`) → **175 endpoints** ; (2) §1.5 corrigée : lignes `/web/admin` (corrections de déclarations, imports, QR), `/web/andon` (`GET /events`), `/web/referentials` (complétude), `/mobile/home` (affectations publiées, KPI du poste, calendrier de shift, flux SSE) ; (3) `trackCauseUsage` (`refStore.ts:348`) **n'a pas d'endpoint** : l'usage des causes est dérivé côté serveur de `oas_declarations`/`oas_events`, jamais incrémenté par le client ; (4) `logAudit` (`auditStore.ts:62`) **n'a pas d'endpoint d'écriture** : l'audit est produit par le trigger `oas_audit_row`, le client ne fait que lire `GET /audit`.
>
> **Nouveautés v7** : **§1.2 bis — routage des tenants OAS par suffixe `oas`** (`devoas`, `demooas`, `krossieroas`, `<client>oas`), une **base de données dédiée par slug** via `TENANT_<SLUG>_DATABASE_URL`, fail-closed 503 si non provisionné, 400 si un slug non-`oas` appelle `api/oas/*`, migrations `oas_*` rejouées **par base**, caches et groupes temps réel clés par `(slug, tenant_id)`, provisionnement d'un client en 3 étapes sans redéploiement.
>
> **Nouveautés v6** (triple vérification frontend, fichier:ligne relus) : (1) **§1.5 cartographie exhaustive des 22 écrans** (3 shells, 9 routes web + 12 routes mobile) ; (2) **§6.2 contrat endpoint ↔ écran** ; (3) **§6.3 temps réel** — SSE + 2 `IHostedService` remplaçant les timers navigateur ; (4) **§7.3 formules KPI à porter à l'identique** ; (5) **§8 réécrit** : le contrôle d'accès réel est *workspace guard + CONSOLE_ROLES + plugin gate*, il n'existe **aucune chaîne de permission** dans le frontend ; (6) 2 tables (`oas_plugin_activations`, `oas_responder_availability`) et 6 endpoints ajoutés → **17 tables créées, 172 endpoints** (→ 175 en v8) ; (7) push et biométrie marqués **[NON VÉRIFIÉ / lot 9 optionnel]** (aucun code client).
>
> **Nouveautés v5** (conservées) : préfixe `oas_` sur 100 % des tables (§5.0) ; migrations `001..008` Supabase à ré-émettre dé-supabasées ; `target_oee` sur `lines` ; contournement `nopassword` (§9) ; tables plan de carte + MOTD andon.

---

## 0. RÔLE ET MISSION

Tu es un ingénieur backend senior **.NET 8 / ASP.NET Core / EF Core / PostgreSQL**. Ta mission : implémenter le backend **OAS** (Operator Assistance System — supervision d'atelier : andon, déclarations de production, TRS/OEE, affectations, SLA) **à l'intérieur** d'une application ASP.NET Core existante et **en production**, nommée `MyApi`, **sans jamais en modifier une seule ligne existante** (sauf 3 lignes d'enregistrement dans `Program.cs`).

Tu écris du code de production : typé, testé, sécurisé, multi-tenant, idempotent, documenté en Swagger.

**Interdiction absolue de régression sur l'application hôte.** À la fin de chaque lot, l'inventaire du socle doit rester **exactement 95 fichiers contrôleurs / 993 actions HTTP**.

---

## 1. CONTEXTE VÉRIFIÉ (ne pas ré-auditer, ces chiffres sont exacts)

### 1.1 Backend existant `Backend/` — assembly `MyApi`

| Fait | Valeur |
|---|---|
| Contrôleurs | **95 fichiers**, **100 classes** `ControllerBase`, **993 actions**, 996 mappings `[Http*]`, 47 modules |
| DbContext | **1 seul** — `MyApi.Data.ApplicationDbContext` (1367 lignes), **173 `DbSet` actifs** |
| Migrations EF | **Aucune**. `Backend/Migrations/` = 7 scripts SQL manuels (ALTER/index/seed, zéro `CREATE TABLE`). Schéma créé/réparé au runtime par `DatabaseSchemaSynchronizer.cs` / `RuntimeSchemaRepair.cs` |
| Conventions socle | PK `int`, `PascalCase`, `BaseEntity{Id, CreatedAt, UpdatedAt, CreatedBy, ModifiedBy}`, soft-delete `IsDeleted` **sans filtre EF global** |
| Temps réel | SignalR, 1 hub : `/hubs/workflow` (`Program.cs:1601`) |
| Cache | Redis via `REDIS_URL`, sinon mémoire |
| Fichiers | UploadThing + `/uploads` local, limite 50 Mo |
| Déploiement | Render, Docker .NET 8, port `$PORT`, `/health` |

**Pièges de comptage connus** : `WebsiteBuilder/Controllers/WBSupportControllers.cs` contient 7 classes ; `EmailAccounts/Controllers/EmailAccountsController_SyncEndpoints.cs:6` est un `partial class` **sans** `: ControllerBase` (10 actions invisibles au grep).

### 1.2 Multi-tenancy (`Backend/Infrastructure/TenantMiddleware.cs:21-24`)

```csharp
public const string TenantHeaderName       = "X-Tenant";        // base physique / slug
public const string TargetTenantHeaderName = "X-Target-Tenant";  // société (TenantId) dans la base
public const string ViewAllHeaderName      = "X-View-All";       // vue inter-sociétés (MainAdminUser)
public const string ViewAllSentinel        = "__all__";
```

- Base par tenant : `TENANT_<SLUG>_DATABASE_URL` (résolu `Program.cs:561,604`).
- Filtre global EF sur `ITenantEntity.TenantId` (`ApplicationDbContext.cs:399-434`) ; variante « scope » pour `[ModuleScope]` (`:444-453`) ; `_currentTenantId == -1` = voir tout.
- Estampillage `TenantId` à l'insertion (`:509-593`).
- Requête authentifiée sans société active sur chemin non exempté → **HTTP 428 `{"error":"company_required"}`** (`TenantMiddleware.cs:311-324`).
- Chemins exemptés (`:80-102`) : `/api/public`, `/api/auth`, `/api/email-verification`, `/api/twofactor`, `/api/tenants`, `/api/systemlogs`, `/api/logs`, `/api/profile`, `/api/users/me`, `/api/me`, `/api/module-scope`, `/api/health`, `/api/documents/upload`, `/api/upload`, `/swagger`.

**→ Toute requête OAS porte `X-Tenant` ET `X-Target-Tenant`, et le frontend traite le 428 comme « choisir une société ».**

### 1.2 bis — Tenants OAS dédiés : tout slug se terminant par `oas` (NOUVEAU v7, OBLIGATOIRE)

Le socle expose déjà des sous-domaines tenants (`dev.`, `demo.`, `krossier.`) → header `X-Tenant: dev|demo|krossier` → base résolue par `TENANT_<SLUG>_DATABASE_URL` (`TenantMiddleware.cs:386-419`, normalisation : majuscules, non-alphanumériques → `_`). **OAS réutilise ce mécanisme tel quel, sans le modifier** : à chaque tenant socle `<x>` correspond un tenant OAS `<x>oas`, servi par sa **propre base de données**.

| Sous-domaine | `X-Tenant` | Variable d'environnement | Base |
|---|---|---|---|
| `devoas.<domaine>` | `devoas` | `TENANT_DEVOAS_DATABASE_URL` | base OAS de dev |
| `demooas.<domaine>` | `demooas` | `TENANT_DEMOOAS_DATABASE_URL` | base OAS de démo |
| `krossieroas.<domaine>` | `krossieroas` | `TENANT_KROSSIEROAS_DATABASE_URL` | base OAS Krossier |
| `<client>oas.<domaine>` | `<client>oas` | `TENANT_<CLIENT>OAS_DATABASE_URL` | 1 base par client OAS |

Règles à implémenter (aucune modification de `TenantMiddleware`/`TenantDbContextFactory` — uniquement du code **dans le module OAS**) :

1. **Détection** : helper `OasTenant.IsOasSlug(slug)` = `slug.EndsWith("oas", OrdinalIgnoreCase)`. `OasTenant.BaseSlug("krossieroas") == "krossier"` (informationnel : journalisation / rattachement commercial, **jamais** utilisé pour choisir une base).
2. **Résolution de base OAS** : `OasDbContext` est construit par une factory `IOasDbContextFactory` qui appelle `ITenantDbContextFactory.GetConnectionString(slug)` (même cache, même normalisation `postgres://`). Aucune chaîne de connexion en dur, aucun `DATABASE_URL` lu directement.
3. **Résolution de base source (main MyApi)** : pour synchroniser les utilisateurs éligibles, OAS doit aussi lire la base du tenant parent. La convention est : pour `TENANT_<SLUG>OAS_DATABASE_URL`, la base source est `TENANT_<BASESLUG>_DATABASE_URL` où `baseSlug = slug.Substring(0, slug.Length - 3)` (ex. `devoas` → `dev`, `krossieroas` → `krossier`). Cette chaîne est résolue via `ITenantDbContextFactory.GetConnectionString(baseSlug)` — aucune duplication de configuration.
4. **Fail-closed en production** : si `X-Tenant` se termine par `oas` et que `TENANT_<SLUG>_DATABASE_URL` est absent → **HTTP 503 `{"error":"oas_tenant_not_provisioned","tenant":"<slug>"}`**. Interdit de retomber silencieusement sur la base par défaut (contrairement au socle, qui tolère le fallback partagé). En `Development` uniquement, fallback sur `TENANT_DEVOAS_DATABASE_URL` puis `DATABASE_URL`, avec un log `Warning`.
5. **Refus du croisement** : si `X-Tenant` ne se termine pas par `oas` sur une route `api/oas/*` → **HTTP 400 `{"error":"oas_tenant_required"}`** (empêche d'écrire des tables OAS dans une base socle). Exemptions : `api/oas/health` et `api/oas/setup`.
6. **Migrations par base** : le schéma OAS est livré sous forme de **fichiers SQL exécutés manuellement par l'opérateur** (`public/OAS-SQL/001_schema.sql`, `002_indexes.sql`, `003_triggers.sql`, `004_seed.sql`). L'application ne les applique **pas** automatiquement au démarrage. Un endpoint `GET /api/oas/health` vérifie que les tables attendues existent et renvoie le statut par base. Le provisionnement d'un nouveau client OAS = 3 étapes : créer la base → ajouter `TENANT_<CLIENT>OAS_DATABASE_URL` → exécuter les 4 fichiers SQL → appeler `api/oas/health`.
7. **Isolation double** : base par slug (`X-Tenant`) **et** filtre `tenant_id` par société (`X-Target-Tenant`) à l'intérieur de la base — les deux, jamais l'un à la place de l'autre. Pour la phase initiale, `X-Target-Tenant` est optionnel et défaut `0` (pas de table `oas_tenants`). Les caches (slugs, plugins, KPI, SSE groups) sont **clés par `(slug, tenant_id)`** ; un cache statique global est un échec de revue.
8. **Temps réel** : canaux SSE préfixés `"{slug}:{tenantId}:…"` pour qu'aucun événement `demooas` n'atteigne `krossieroas`.
9. **Seed / démo** : `demooas` est la seule base autorisée à recevoir le jeu de données de démonstration (`src/oas/demo.ts`) ; interdit sur tout autre slug.
10. **Tests** : (a) `IsOasSlug` sur `devoas|demooas|krossieroas|oas` = vrai, `dev|demo|krossier` = faux ; (b) deux slugs OAS avec deux bases → une écriture sur l'un est invisible sur l'autre ; (c) slug OAS non provisionné → 503 en prod ; (d) `X-Tenant: krossier` sur `api/oas/*` → 400 ; (e) exécuter les scripts SQL deux fois = no-op (`CREATE IF NOT EXISTS`).


### 1.3 Auth / RBAC du socle

- JWT HMAC-SHA256 signé avec `Jwt:Key`. Aujourd'hui : `ValidateLifetime = false` (`Program.cs:273-290`) et expiration **10 ans** (`AuthService.cs:997`, `:1210`) → **corrigé au lot 0**.
- Refresh token = 64 octets aléatoires base64 (`AuthService.cs:1013-1019`), stocké sur `User.RefreshToken`/`TokenExpiresAt`, revalidé par `POST /api/auth/refresh`.
- Claims admin (`:981-989`) : `NameIdentifier, Email, Name, UserId, FirstName, LastName, Industry, UserType=MainAdminUser, login_type=admin`.
- Claims utilisateur (`:1190-1202`) : + `Role, UserType=RegularUser, login_type=user, tenant_id, can_switch_company`.
- RBAC **maison** : `[RequirePermission(module, action)]` → `IPermissionService.UserHasPermissionAsync(userId, module, action)`. **`UserType=MainAdminUser` court-circuite tous les contrôles** (`RequirePermissionAttribute.cs:44-46`). Aucune policy ASP.NET déclarée.

### 1.4 Frontend OAS `src/` — aucun backend branché

`fetch|axios|XMLHttpRequest|apiClient` sur `src/` : **0 occurrence**. Tout est simulé en `localStorage` :

| Store | Clé localStorage | Fichier |
|---|---|---|
| Auth session | `oas.auth.v1` | `src/oas/authStore.ts:21-102` |
| Référentiels | `oas.referentials.v1` | `src/oas/refStore.ts:121-674` |
| Hiérarchie | `oas.hierarchy.v1` | `src/oas/hierarchyStore.ts:16-212` |
| Événements andon | `oas.events.v1` | `src/oas/eventStore.ts:45-306` |
| Affectations + présence | `oas.assignments.v2` | `src/oas/assignmentStore.ts:16-180` |
| Audit | `oas.audit.v1` | `src/oas/auditStore.ts:14-87` |
| Session opérateur | `oas.operator.session.v1` | `src/modules/auth/store/session.ts:29-90` |
| Activation plugins | `oas.plugins.activations.v1` | `src/modules/shared/plugins/activationStore.ts:17` |
| Message andon (MOTD) | `oas.andon.motd` | `src/modules/andon/pages/AndonTv.tsx:15,30,111` — **donnée métier, endpoint à créer** |
| Préférences UI (hors périmètre backend) | `oas.theme`, `oas.lang`, `typography-overrides` | `ThemeProvider.tsx:8`, `I18nProvider.tsx:7`, `typography.runtime.ts:19` |

Faits à corriger : PIN **en clair** en localStorage (`refStore.ts:214`, régénération `:356`, `:390-393`, vérification `:429-432`) ; mot de passe console **en dur** `secret123` (`authStore.ts:79`) ; sync simulée par `setTimeout` + `Math.random() < 0.2` (`session.ts:504-508`, `flushPending` `:530-543`, écouteurs online/offline `:547-555`). Seul `useOnline()` (`:473`) est réutilisable tel quel.

### 1.5 Cartographie exhaustive des surfaces — 3 shells, 22 écrans (vérifié fichier:ligne)

**3 coquilles applicatives** : console web `src/web/WebApp.tsx` · terminal mobile `src/mobile/MobileApp.tsx` · bureau Electron `electron/main.cjs:1-33` (charge `dist/index.html#/web/login`, `contextIsolation: true`, ouverture externe déléguée au navigateur). Route `/` = sélecteur d'espace de travail.

**Console web — 9 routes** (`WebApp.tsx:204-214`), toutes derrière `PluginGate` :

| Route | Composant | Plugin | Surface fonctionnelle | Endpoints consommés (§6.1) |
|---|---|---|---|---|
| `/web/dashboard` | `ManagerDashboard` | `OA0002DASHBOARD` | TRS live, Pareto, comparaison de lignes, tendance | `GET /kpi/daily` · `/kpi/pareto` · `/kpi/trend` · `/kpi/line-comparison` · `/post-states` |
| `/web/shopfloor` | `ShopFloorBoard` | `OA0003SHOPFLOOR` | carte atelier, états de poste, ouverture d'événement, acquittement / escalade depuis la carte | `GET /post-states` · `GET /posts/layout` · `POST /events` · `POST /events/{id}/ack` (`ShopFloorBoard.tsx:99`) · `POST /events/{id}/escalate` (`:105`) · flux `GET /stream` |
| `/web/alerts` | `AlertsQueue` | `OA0006ALERTS` | file andon, prise en charge, escalade, clôture | `GET /events` · `/events/{id}/take|eta|arrive|advance|escalate|close` · flux `GET /stream` (**v14** : listé par §6.2 règle 2 parmi les écrans temps réel, absent par erreur de cette ligne jusqu'ici) |
| `/web/assignments` | `Assignments` | `OA0007ASSIGNMENTS` | plan d'affectation, présence, auto-remplissage, publication | `GET|PUT|DELETE /assignments` · `/assignments/auto-fill` · `/assignments/publish` · `/presence/*` |
| `/web/reports` | `Reports` | `OA0009REPORTING` | rapports TRS/arrêts, export **client-side**, sélecteur de période (shift/jour/semaine/mois/custom) | `GET /kpi/*` (dont `/kpi/sla-summary`, `/kpi/cadence-gap`, nouveaux v13) · `GET /declarations?from=&to=` · `GET /events?from=&to=` |
| `/web/shift-report` | `ShiftReport` | `OA0009REPORTING` | rapport de poste + sign-off | `GET /kpi/daily` · `POST /shift-signoffs` |
| `/web/andon` | `AndonTv` | `OA0010ANDON` | wallboard TV : rotation ligne **12 s** (`AndonTv.tsx:11`), rotation alerte **6 s** (`:13`), MOTD (`:15`), heartbeat simulé | `GET /andon/message` · `PUT /andon/message` · `GET /kpi/daily` · `GET /events` (rotation des alertes, `AndonTv.tsx:6`) · `GET /post-states` · flux `GET /stream` |
| `/web/referentials` | `Referentials` | `OA0008REFERENTIALS` | sites/zones/lignes/postes, équipements, cadences, causes, produits, shifts, opérateurs, imports, complétude | tout `§6.1` référentiels + `GET /imports` · `GET /referentials/completeness` |
| `/web/admin` | `Admin` | `OA0011CONSOLE` | 7 onglets (`Admin.tsx:23`) : badges QR, opérateurs, shifts (lecture seule), plugins, imports, corrections, audit | socle `/api/users`, `/api/roles` (comptes console) + **opérateurs OAS** : `GET /operators` · `POST /operators` (`UsersPanel.tsx:25`, champ `interim`) · `PUT /operators/{id}/active` (`:128`) · `PUT /operators/{id}/role` (`:82`) · `PUT /operators/{id}/scope` (`:102`, `scopeLines`) · `POST /operators/{id}/regenerate-pin` (`:125`) + `GET|PUT|POST /plugin-activations` (le bouton « réinitialiser », `PluginsPanel.tsx:54` → `activationStore.ts:90`, appelle `POST /plugin-activations/bulk`) · `GET /audit` · `GET /declarations` · `PUT /declarations/{id}/correct` (`CorrectionsPanel.tsx:38`) · `POST /imports` (`ImportPanel.tsx:113`) · `GET /posts/{id}/qr-token` · `POST /posts/{id}/qr-token/rotate` (voir §13 : aucun déclencheur UI aujourd'hui) · `GET /posts/qr-tokens?lineId=` (**v13**, vue d'impression groupée, `Admin.tsx:92-114`) |

**Terminal mobile — 12 routes** (`MobileApp.tsx:334-346`) :

| Route | Composant | Plugin | Surface | Endpoints |
|---|---|---|---|---|
| `/mobile/home` | `OperatorHome` | *(aucun gate)* | session de poste, rappel de déclaration, KPI du poste | `POST /post-sessions/open` · `GET /post-sessions/active` · `POST /post-sessions/{id}/relay` · `POST /post-sessions/{id}/close` (**v13** : les 3 routes de session doivent porter `clientEventId`, généré nulle part côté client aujourd'hui — `session.ts:194,236,269` — alors que l'app simule déjà des retries réseau, `attemptFlush` `:502-523`, qui les rejoueraient sans déduplication possible) · `GET /assignments/published?shift=&date=` (**v13, corrigé** — c'était `GET /assignments?shift=` en v12, la ressource **brouillon** ; `OperatorHome.tsx:60`/`ScanPage.tsx:41,52` lisent `assignmentStore.publishedOperator`, gated sur `publishedAt`, donc bien la vue **publiée** déjà présente au catalogue) · `GET /kpi/daily?postId=` · `GET /shifts` (**v13, corrigé** — `getShift()`/`shiftEndedFor()`, `session.ts:22-27,293-298`, n'utilisent que les horaires statiques d'un shift, pas une plage de dates ; `GET /shifts/calendar` reste utilisé ailleurs sur cet écran mais n'est plus la seule route citée) · flux `GET /stream` (rappel de déclaration) |
| `/mobile/stop` | `DeclareStop` | `OA0004DECLARATIONS` | déclaration d'arrêt + cause, proposition d'une cause absente de l'arbre | `POST /events` · `GET /causes` · `POST /cause-proposals` (`DeclareStop.tsx:54`, `proposeCause`) — `trackCauseUsage` (`:46`) **n'émet rien** (usage dérivé serveur) |
| `/mobile/production` | `DeclareProduction` | `OA0004DECLARATIONS` | quantités OK/NOK, `suggestedQty`, clôture de l'arrêt en cours | `POST /declarations/production` · `/declarations/scrap` · `POST /declarations/stop/{clientEventId}/close` (`DeclareProduction.tsx:92`, `closeStop`) |
| `/mobile/changeover` | `ChangeoverPage` | `OA0004DECLARATIONS` | changement de série | `POST /changeovers` · `PUT /changeovers/{id}/finish` — **divergence connue** : le frontend actuel passe par `declareStop('CS-01')`/`closeStop` (`ChangeoverPage.tsx:44,62`) ; le domaine dédié est la cible (§13) |
| `/mobile/scan` | `ScanPage` | `OA0012TRACEABILITY` | scan QR/code-barres **natif** (`src/lib/scanner.ts:9-25`, repli web `BarcodeDetector`/`jsQR`) | `POST /post-sessions/scan` |
| `/mobile/neighbor` | `NeighborStop` | `OA0004DECLARATIONS` | arrêt déclaré pour un poste voisin | `POST /events` · `GET /posts?lineId=` |
| `/mobile/map` | `ShopFloorPage` | `OA0003SHOPFLOOR` | carte atelier mobile | `GET /post-states` · `GET /posts/layout` |
| `/mobile/inbox` | `InterventionInbox` | `OA0005INTERVENTIONS` | file d'intervention, disponibilité « occupé » | `GET /interventions/inbox` · `POST /interventions/{id}/assign|start|close` · `PUT /responder-availability/{profileId}` · flux `GET /stream` (**v14** : listé par §6.2 règle 2 parmi les écrans temps réel, absent par erreur de cette ligne jusqu'ici) — **le code appelle aujourd'hui les actions d'événement** `takeEvent`/`setEventEta`/`arriveOnSite`/`declineEvent`/`closeEvent` (`InterventionInbox.tsx:50,130,107,115,88`) : l'intervention est la **projection** d'un `oas_event` pris en charge, les deux familles de routes agissent sur le même cycle de vie (§13) |
| `/mobile/kpi` | `MobileKpi` | `OA0002DASHBOARD` | TRS du poste | `GET /kpi/daily?postId=` · `GET /kpi/pareto?postId=` (`MobileKpi.tsx:21`, `useLivePareto`) |
| `/mobile/history` | `HistoryPage` | `OA0012TRACEABILITY` | historique déclarations/événements **de la session en cours** (filtré par session/date, pas un historique permanent — décision produit v12), correction dans la fenêtre de **10 min** (règle métier `BL-045`) | `GET /declarations?sessionId=` · `GET /events?sessionId=` · `PUT /declarations/{id}/correct` (`HistoryPage.tsx:63`, garde `isCorrectable` `session.ts:366,369` — `CORRECTION_WINDOW_MIN = 10`) |
| `/mobile/shift-end` | `ShiftEnd` | `OA0009REPORTING` | fin de poste, export **client-side** | `POST /post-sessions/{id}/close` (`ShiftEnd.tsx:129`, `closeSession`) · `POST /shift-signoffs` — **à câbler** : l'écran ne soumet aujourd'hui aucun sign-off, seule la console le fait (`ShiftReport.tsx:235`) |
| `/mobile/login` | `MobileLogin` | — | matricule + PIN (`PinPanel`) **ou** badge QR (`ScanPanel`) | `POST /shopfloor/login` — **un seul endpoint**, corps polymorphe `{ mode: "pin", badge, pin }` \| `{ mode: "qr", token }` (il n'existe **pas** de `/shopfloor/badge-login`) |

**Registre de plugins** : 15 manifestes auto-découverts par `import.meta.glob('/src/**/plugin.ts')` (`registry.ts:11`) = 13 fonctionnels + 2 coquilles — `OA0001AUTH` (core), `OA0002DASHBOARD` (core), `OA0003SHOPFLOOR` (core), `OA0004DECLARATIONS` (dép. `OA0003SHOPFLOOR`, `OA0008REFERENTIALS`), `OA0005INTERVENTIONS` (dép. `OA0004DECLARATIONS`), `OA0006ALERTS` (dép. `OA0004DECLARATIONS`), `OA0007ASSIGNMENTS` (dép. `OA0003SHOPFLOOR`), `OA0008REFERENTIALS` (core), `OA0009REPORTING` (dép. `OA0004DECLARATIONS`), `OA0010ANDON` (dép. `OA0003SHOPFLOOR`), `OA0011CONSOLE` (core), `OA0012TRACEABILITY` (dép. `OA0004DECLARATIONS`), `OA0013DEMO`, + shells `OA1000WEBAPP`, `OA1001MOBILEAPP`. Les plugins `isCore` ne peuvent **jamais** être désactivés (`activationStore.ts:59-62`) et la désactivation cascade sur les dépendants (`:74-88`) — le backend doit reproduire exactement ces deux règles. État persisté dans `oas.plugins.activations.v1` (`activationStore.ts:17`) → **à remplacer par `oas_plugin_activations`** (§5.2, table 16).

**Exports / imports** : tous **client-side** — CSV et `.xls` HTML (`src/shared/lib/excel.ts`), PDF (`src/shared/lib/pdf.ts`, jsPDF), import XLSX (`ImportPanel.tsx`). Le backend ne génère **aucun** fichier ; il expose seulement les données et la trace d'import (`/imports`, `/imports/{id}`).

**Non implémenté côté client — ne pas supposer** : aucune biométrie, aucun fournisseur push, aucun IndexedDB, aucun WebSocket. La file offline est un tableau en mémoire/localStorage. **Nuance vérifiée** : un service worker de cache d'app-shell existe (`public/sw.js`, enregistré dans `src/main.tsx:23-27` hors natif) — il ne fait **aucune** synchronisation de données ; ne pas s'appuyer dessus pour l'offline métier.

---

## 2. PÉRIMÈTRE — RÈGLE UNIQUE ET NON NÉGOCIABLE

> **OAS ne réutilise QUE le socle transverse. Tout le métier et tous les « constructeurs » sont écrits dans OAS, avec ses propres formulaires, entités et moteur de workflow.**

### 2.1 Socle réutilisé — 15 modules, 27 fichiers, 288 actions (29 % de 993)

| Module | Fichiers | Actions | Mode |
|---|---:|---:|---|
| Auth | 4 | 32 | consommé (login, me, refresh, logout, change-password) |
| Users | 1 | 12 | consommé |
| Roles | 2 | 19 | consommé |
| UserGroups | 1 | 9 | consommé |
| Tenants | 1 | 7 | consommé |
| Settings (AppSettings, ModuleScope) | 2 | 7 | consommé |
| Plugins | 2 | 13 | consommé (remplace `activationStore.ts`) |
| Lookups | 2 | 124 | **lecture seule, jamais modifié** |
| Notifications | 1 | 8 | consommé |
| Documents | 1 | 8 | consommé |
| Shared (Upload, UploadThing, Logs, SystemLogs, EntityFormDocuments, Dev) | 6 | 24 | consommé |
| Signatures | 1 | 3 | consommé (sign-off fin de poste) |
| Numbering | 1 | 6 | consommé |
| Sync | 1 | 4 | **patron uniquement, non modifié** |
| Skills | 1 | 12 | consommé |

### 2.2 Ignorés — 68 fichiers / 705 actions : ni appelés, ni étendus, ni testés, ni dépendus

`WorkflowEngine` (5/28, et son hub `/hubs/workflow`) · `WebsiteBuilder` (4/46) · `DynamicForms`+`PublicForms` (2/12) · `Dashboards`+`Reporting` (5/23) · `Processes` (1/10) · `Calendar` (1/17) · `AiChat`+`UserAiSettings` (3/21) · `HR` (1/73) · `SupportTickets` (2/16) · `Incidents` (1/1) · `Articles`/`StockTransactions` (4/31) · `EmailAccounts` (3/30) · `ExternalEndpoints` (2/16) · `OfflineHydration` (1/2) · `Preferences`/`PdfSettings` (2/12) · bloc CRM/ventes/achats : Deals, Offers, Sales, Invoices, Payments, Purchases, Contacts, Dispatches, Installations, ServiceOrders, Projects, Planning, PlanningProfiles, RetenueSource, ModuleRequests (31/367).

Conséquences directes :
- **Le moteur SLA / machine à états andon est écrit intégralement dans OAS** (sous-module `Sla`), avec son propre hub SignalR.
- **Les contrôles qualité sont typés en dur** dans OAS (pas de form builder).
- **Les KPI/OEE sont des agrégats écrits en dur** dans OAS (pas de widget no-code).
- **Produits, OF, interventions, présence** sont des entités OAS (pas `Articles`, pas `SupportTickets`, pas `HR`).

---

## 3. ARCHITECTURE — MONOLITHE + ISOLATION HERMÉTIQUE

**Verdict : monolithe** (une image, un déploiement, une base) **avec frontière technique forte**.

| Point | Décision |
|---|---|
| Déploiement | un seul, dans `MyApi` |
| Contexte EF | **`OasDbContext` séparé**, schéma `public`, **aucun `HasDefaultSchema`** |
| Clés primaires | `uuid` côté OAS ; `int` côté socle ; **jamais de FK physique entre les deux** |
| **Nommage SQL** | **`snake_case` et préfixe obligatoire `oas_` sur TOUTES les tables, sans exception** (`oas_sites`, `oas_events`, `oas_users`…). Le préfixe **est** la frontière : toute table sans `oas_` appartient au socle et est intouchable. Index `idx_oas_*`, fonctions `oas_*`, triggers `trg_oas_*`, enums `oas_*` |
| Logique métier critique | **conservée dans les triggers Postgres** de `db/migrations` (immutabilité, SLA, recalcul d'état, audit) — rejoués sous leur nom `oas_*`, jamais réécrits en C# |
| Schéma / migrations | **fichiers SQL exécutés manuellement** par l'opérateur (`public/OAS-SQL/001_schema.sql`, `002_indexes.sql`, `003_triggers.sql`, `004_seed.sql`). L'application ne les applique **pas** automatiquement. Suivi dans `oas_schema_migrations` pour information uniquement. |
| Isolation tenant | **filtre global EF sur `tenant_id`** dans `OasDbContext` (le socle n'utilise pas la RLS Postgres) — **aucune policy RLS, aucun rôle `authenticated`/`service_role`**. Phase initiale : `X-Target-Tenant` optionnel, défaut `0`. Pas de table `oas_tenants`. |
| Soft-delete | filtre global `IsDeleted = false` déclaré dans `OasDbContext`, aligné sur la convention `BaseEntity` du socle. Les faits (déclarations, événements) ne sont jamais supprimés. |
| Temps réel | **SSE uniquement** : `GET /api/oas/stream`. Aucun hub SignalR dédié OAS. |
| Lien identité | **table OAS autonome `oas_users`** (`id uuid, email, password_hash, pin, qr_token, role, is_active, …`). Aucune dépendance physique vers `Users`/`profiles` du socle. Synchronisation **à la demande (JIT)** depuis la base source (§1.2 bis), déclenchée par le flux de connexion — **aucun `IHostedService` de polling**. |

### 3.1 Les 3 SEULES lignes touchées hors du module

```csharp
builder.Services.AddOasModule(builder.Configuration);   // DI + OasDbContext + hosted services + auth
app.UseOasTenantMiddleware();                             // routing *oas + fail-closed + base source resolution
app.MapOasEndpoints();                                    // SSE /stream + /health + /setup
```

Si OAS est supprimé : effacer `Backend/Modules/OAS/` + ces 3 lignes → l'application d'origine est **identique**.

### 3.2 Frontières par couche — ce qui est INTERDIT

| Couche | Frontière OAS | Interdit |
|---|---|---|
| Routes HTTP | préfixe unique `api/oas/*` | route hors `api/oas` ; ajouter une action à un contrôleur existant |
| EF Core | `OasDbContext` + ses `DbSet` | ajouter un `DbSet` à `ApplicationDbContext` ; navigation depuis une entité du socle |
| Base | **toute table créée porte le préfixe `oas_`**, migrations livrées dans le module | créer une table sans préfixe `oas_` ; `ALTER TABLE` sur une table socle (= sans préfixe) ; FK physique vers `Users`, `LookupItems`, `Documents` |
| Identité | `oas_users` table autonome, synchronisée depuis la base source | ajouter une colonne à `Users` ou `profiles` du socle ; FK physique vers le socle |
| Permissions | rôles simples OAS (`admin`, `supervisor`, `operator`) portés par `oas_users.role` ; attribut `[OasAuthorize(Role = ...)]` | modifier `RequirePermissionAttribute` ou `PermissionService` du socle |
| Temps réel | `GET /api/oas/stream` (SSE) | publier sur le hub workflow du socle |
| Jobs | `IHostedService` interne au module (`OasSlaWorker`) | brancher un job dans le scheduler existant |
| Fichiers | appel de l'interface publique du service Documents / HTTP interne | écrire dans les tables `Documents` |
| Config | section `Oas:` + variables `Oas__*` | changer une clé de configuration existante |
| Swagger | `SwaggerDoc("oas", …)` | modifier le document Swagger par défaut |

### 3.3 Couplage identité, sans toucher au socle

OAS possède sa propre table `oas_users` (`id uuid`, `email`, `password_hash`, `pin`, `qr_token`, `role`, `is_active`, `source_user_id`, `source_tenant_id`). Aucune FK physique n'existe vers `Users` ou `profiles` du socle.

**Synchronisation depuis la base source, à la demande** (§1.2 bis, détail §8.3) : pas de tâche de fond. `OasUserJitSyncService` est invoqué **synchronement dans le flux de connexion** (`POST /api/oas/auth/login`, `POST /api/oas/shopfloor/login`, `POST /api/oas/auth/refresh`) : il résout la base source du tenant parent (ex. `dev` pour `devoas`), lit l'unique utilisateur concerné (par email ou badge, jamais un scan complet de table) et crée/met à jour son compte OAS s'il possède un rôle `oas_mobile`, `oas_supervisor` ou `oas_admin` côté socle. Le mapping repose sur `source_user_id` + `source_tenant_id` pour éviter les doublons. Conséquence : un utilisateur nouvellement éligible se connecte dès sa première tentative ; un utilisateur retiré des rôles OAS est bloqué dès sa prochaine tentative — sans délai de propagation.

Un **opérateur atelier** = un enregistrement `oas_users` avec `role = operator` ; un **superviseur** = `role = supervisor` ; un **admin console** = `role = admin`. La connexion atelier émet un JWT signé avec la même clé que le socle mais **`aud = "oas-shopfloor"`** et un claim `oas_workspace = mobile` ; elle est refusée sur les routes console. Inversement, un JWT console (`aud = "oas-console"`, `oas_workspace = web`) est refusé sur les routes atelier.

### 3.4 Garde-fous automatiques (livrés au lot 1, bloquants en CI)

| Garde-fou | Mise en œuvre | Effet |
|---|---|---|
| Test d'architecture | xUnit : aucun type sous `MyApi.Modules.OAS` ne référence `ApplicationDbContext` ; aucun type hors OAS ne référence un type OAS | échec de build |
| Test de routes | inventaire des routes au démarrage : toute route OAS commence par `api/oas/` | échec de test |
| Diff de schéma | comparaison des tables **sans préfixe `oas_`** avant/après déploiement ; et test : toute entité de `OasDbContext` mappe une table `oas_*` | détecte tout `CREATE`/`ALTER` hors périmètre OAS |
| Kill-switch | OAS enregistré comme plugin (`oas_plugin_activations`) ; désactivé → `api/oas/*` renvoie 404 | rollback sans redéploiement |

---

## 4. STRUCTURE DE CODE — `Backend/Modules/OAS/`, 22 SOUS-MODULES

```text
Backend/Modules/OAS/
├── Common/                      # infra du module (aucun endpoint)
│   ├── OasControllerBase.cs     # [ApiController] [Route("api/oas/[controller]")] [Authorize] + tenant
│   ├── OasDbContext.cs          # schéma "oas", uuid, snake_case, filtre tenant + archived_at
│   ├── OasModuleRegistration.cs # AddOasModule(services, config) — DI de tous les sous-modules
│   ├── Data/Migrations/         # 001..009 + runner RunOasMigrationsAsync()
│   ├── Realtime/OasHub.cs       # hub "oas" (groupes : site, ligne, poste, rôle)
│   └── Scope/OasScopeFilter.cs  # périmètre site/zone/ligne issu de user_roles.scope_*
├── ShopFloorAuth/   ├── Hierarchy/    ├── Equipments/  ├── Cadences/
├── Causes/          ├── Products/     ├── Shifts/      ├── Teams/
├── Assignments/     ├── PostSessions/ ├── Declarations/├── Changeovers/
├── Quality/         ├── Events/       ├── Sla/         ├── Interventions/
├── PostStates/      ├── Kpi/          ├── Imports/     ├── Integrations/
├── Lookups/         └── Offline/
```

Chaque sous-module suit le patron déjà présent dans le dépôt (`Backend/Modules/Sync`, `Backend/Modules/Projects`) : `Controllers/`, `DTOs/`, `Models/`, `Services/` (+ `Data/` pour la configuration EF si index/contraintes).

### 4.1 Règles communes à TOUS les sous-modules

| Règle | Valeur |
|---|---|
| Préfixe de route | `api/oas/<ressource>` — jamais de route racine |
| Attributs de classe | `[Authorize]` + `[OasAuthorize(Roles = "admin|supervisor|operator")]` ; `OasControllerBase` applique aussi le workspace guard (`mobile` vs `web`) |
| Tenant | résolu par `TenantMiddleware` existant — **jamais** lu depuis le corps de requête |
| Périmètre | `OasScopeFilter` applique site/zone/ligne avant toute lecture |
| Clés | `uuid` (`gen_random_uuid()`), jamais `int` |
| **Tables** | **préfixe `oas_` obligatoire** — déclaré explicitement par `ToTable("oas_<nom>")` dans chaque `IEntityTypeConfiguration` ; une entité sans `ToTable` explicite est un échec de revue |
| Soft-delete | `IsDeleted = true` pour les référentiels (filtre global EF) ; les **faits** (déclarations, événements) ne sont **jamais** supprimés |
| Temps réel | publication SSE via `GET /api/oas/stream` pour événements, états de poste et KPI **uniquement** |
| Idempotence | tout POST créant un fait exige `clientEventId` (uuid) — **contrainte unique en base** ; rejeu = 200 avec la ressource existante |
| Contexte EF | `OasDbContext` **exclusivement** |
| Dépendance socle | uniquement via interfaces publiques (`ITokenService`, `INotificationService`, upload) — jamais via un `DbSet` du socle |
| Erreurs | `ProblemDetails` RFC 7807 ; 428 propagé tel quel |
| Pagination | `?page=&pageSize=` (défaut 1/50, max 200), enveloppe `{ items, total, page, pageSize }` |
| Validation | FluentValidation ou DataAnnotations + `[ApiController]` ; 400 structuré |
| Journalisation | `ILogger` avec `tenantId`, `userId`, `clientEventId` en scope |

---

## 5. BASE DE DONNÉES

### 5.0 Livrables SQL — fichiers exécutés manuellement (non négociable)

Le schéma OAS est livré sous forme de **fichiers SQL purs**, exécutés une fois par l'opérateur sur chaque base `*oas`. L'application **ne les applique pas automatiquement** au démarrage.

| Fichier | Rôle | Ordre |
|---|---|---|
| `public/OAS-SQL/001_schema.sql` | `CREATE EXTENSION`, **enums**, **tables** (49 au total : §5.1 30 tables + §5.2 18 tables + `oas_schema_migrations`) | 1 |
| `public/OAS-SQL/002_indexes.sql` | Tous les index `idx_oas_*`, contraintes uniques, FK internes au périmètre OAS | 2 |
| `public/OAS-SQL/003_triggers.sql` | Fonctions et triggers métier (immutabilité, SLA, recalcul d'état, audit) | 3 |
| `public/OAS-SQL/004_seed.sql` | Données initiales : causes types, rôles OAS, compte admin par défaut, paramètres SLA | 4 |

**Règles de nommage :**
- Toute table porte le préfixe **`oas_`** (`oas_sites`, `oas_events`, `oas_users`…). Le préfixe **est** la frontière : toute table sans `oas_` appartient au socle et doit rester intacte.
- Enums : `oas_app_role`, `oas_post_state`, `oas_event_type`…
- Fonctions : `oas_*` ; triggers : `trg_oas_*` ; index : `idx_oas_*`.
- **Aucune RLS Postgres**, **aucune policy**, **aucun rôle `authenticated`/`anon`/`service_role`**. L'isolation multi-tenant est portée par le **filtre global EF sur `tenant_id`** dans `OasDbContext` (+ `OasScopeFilter` pour site/zone/ligne), aligné sur le socle.
- **Aucun `GRANT` spécifique** : le propriétaire de la connexion EF possède les tables. Les permissions d'accès restent applicatives (`[OasAuthorize]`).

`OasDbContext` ne contient **aucune migration EF**. Il mappe les tables créées manuellement via `IEntityTypeConfiguration` et `ToTable("oas_...")`. Une table sans `ToTable` explicite est un échec de revue.

### 5.1 Tables issues de `db/migrations/` — 30 tables ré-émises préfixées `oas_`

| Source | Tables OAS correspondantes |
|---|---|
| `003_reference_hierarchy.sql` | `oas_sites`, `oas_zones`, `oas_lines` (`target_oee`), `oas_posts` (`qr_token`, `sort_order`), `oas_equipments`, `oas_products`, `oas_routings`, `oas_production_orders` |
| `004_shifts_assignments_causes.sql` | `oas_shift_templates`, `oas_shift_calendar`, `oas_teams`, `oas_team_members`, `oas_assignments`, `oas_post_sessions`, `oas_causes`, `oas_routing_rules` |
| `005_declarations_events.sql` | `oas_declarations`, `oas_events` (`sla_minutes`), `oas_event_transitions`, `oas_event_notifications`, `oas_changeovers`, `oas_quality_checks` |
| `006_post_states_kpi.sql` | `oas_post_states`, `oas_post_state_history`, `oas_kpi_daily` |
| `007_audit_offline.sql` | `oas_audit_log`, `oas_sync_receipts`, `oas_device_tokens`, `oas_attachments`, `oas_imports` |

**Ces triggers portent la logique critique. Les conserver (renommés `oas_*`), ne pas les réécrire en C#.**

### 5.2 Tables complémentaires OAS — 18 tables à créer dans `001_schema.sql`

| # | Table | Sous-module | Raison |
|---|---|---|---|
| 1 | `oas_cause_proposals` | Causes | proposition opérateur + revue chef d'équipe (`refStore.ts:312,328`) |
| 2 | `oas_routing_versions` | Cadences | cadences versionnées (`rate`, `version`, `since`) — `oas_routings` n'a pas de version |
| 3 | `oas_shift_signoffs` | Shifts | validation de fin de poste (`refStore.ts:437`) |
| 4 | `oas_presence_entries` | Assignments | présence attendue/confirmée/absente (`assignmentStore.ts:19,130,148`) |
| 5 | `oas_interventions` | Interventions | file maintenance liée à un événement (`InterventionInbox.tsx`) |
| 6 | `oas_quality_check_templates` | Quality | gabarits de contrôle typés |
| 7 | `oas_quality_check_template_items` | Quality | lignes du gabarit (type, borne min/max, obligatoire) |
| 8 | `oas_sla_rules` | Sla | règles éditables par type d'événement / criticité / ligne |
| 9 | `oas_escalations` | Sla | trace des escalades N1/N2 produites par `oas_job_check_sla` |
| 10 | `oas_import_lines` | Imports | détail ligne à ligne + erreurs |
| 11 | `oas_integration_endpoints` | Integrations | abonnements MES/ERP sortants |
| 12 | `oas_integration_outbox` | Integrations | file d'émission avec retry et statut |
| 13 | `oas_lookup_values` | Lookups | listes plates OAS `(id uuid, tenant_id, type, code, label, color, sort_order, is_default, archived_at)` |
| 14 | `oas_post_layouts` | Hierarchy | disposition de la carte atelier `(post_id, layout_key, sort_order, col_span, row_span, x, y)` — aujourd'hui la carte affiche le tableau `POSTS` de `demo.ts` dans une grille CSS fixe (`ShopFloorMap.tsx:4,61,87`), l'ordre du tableau **est** la disposition |
| 15 | `oas_andon_messages` | Kpi / Andon | message d'écran andon (MOTD) aujourd'hui en `localStorage` `oas.andon.motd` (`AndonTv.tsx:15,30`) |
| 16 | `oas_plugin_activations` | Common | **v6** : activation par tenant des 13 plugins (`OA0002DASHBOARD`…`OA0013DEMO`) — remplace `oas.plugins.activations.v1` (`activationStore.ts:17`) ; `(tenant_id, plugin_code, enabled, updated_at, updated_by)`, unique `(tenant_id, plugin_code)`. **v12** : toute écriture sur cette table (activation, désactivation, cascade de dépendance, `bulk` reset) doit alimenter `oas_audit_log` — `activationStore.ts` actuel n'audite aucune de ces actions, ce n'est pas un comportement à reproduire |
| 17 | `oas_responder_availability` | Interventions / Sla | **v6** : drapeau « intervenant occupé » consommé par le moteur d'escalade (`eventStore.ts:242-251,286`) — `(profile_id, busy, since, reason)` |
| 18 | `oas_users` | Auth / Identity | table autonome des utilisateurs OAS (`id uuid`, `email`, `password_hash`, `pin`, `qr_token`, `role`, `is_active`, `source_user_id`, `source_tenant_id`) — remplace `profiles`/`user_roles` du socle (§3.3) |


Plus la table de suivi `oas_schema_migrations`.

**Aucune autre table.** Pas de table `oas_profiles`, pas de table `oas_user_roles`, pas de table de couplage `oas_user_links`.

Les index et contraintes sont livrés dans `002_indexes.sql` (§5.0).

### 5.3 Enums à NE PAS transformer en lookups

`EVENT_STAGES` (`src/oas/demo.ts:100`) et `EventKind` pilotent des transitions d'état et des triggers SQL (`005`, `008`) : les rendre éditables casse le moteur d'événements. Ils restent figés en code + enum Postgres.

---

## 6. LES 184 ENDPOINTS À ÉCRIRE — TOUS SOUS `api/oas/*`

> Le socle reste à 993 actions. **Aucun** de ces endpoints n'est ajouté à un contrôleur existant.
> 184 = 162 (plan v4) + 2 (plan de carte atelier) + 2 (message andon) + **3 (activation des plugins)** + **2 (disponibilité intervenant)** + **1 (flux SSE `GET /stream`)** + **2 (badge QR de poste)** + **1 (complétude des référentiels)** + **3 (v13 : `kpi/sla-summary`, `kpi/cadence-gap`, `posts/qr-tokens` groupé)** + **6 (v14 : correction du sous-comptage `ShopFloorAuth`, six routes d'auth console jamais créditées — `setup`, `login`, `refresh`, `logout`, `me`, `change-password`)**. Total recompté littéralement ligne par ligne sur les 44 domaines du catalogue §6.1 (méthode `v9` : compter les back-ticks), recoupé avec la somme des 24 lignes de sous-module ci-dessous — les deux convergent sur **184**.

| Sous-module | Tables réutilisées | Tables créées | Endpoints |
|---|---|---|---:|
| Common | — | `oas_plugin_activations` | 5 (3 activation + 1 flux SSE `GET /stream` + 1 complétude) |
| ShopFloorAuth | `oas_profiles`, `oas_posts.qr_token` | — | 8 (**v14, corrigé** — couvre tout le domaine « Auth OAS » du catalogue, pas seulement `shopfloor/login` + `pin/regenerate` : `setup`, `login` console, `refresh`, `logout`, `me`, `change-password` y sont depuis l'ajout de l'auth console mais n'avaient jamais été crédités à un sous-module, sous-comptant le total de 6 depuis v6) |
| Hierarchy | `oas_sites`, `oas_zones`, `oas_lines`, `oas_posts` | `oas_post_layouts` | 27 (dont `GET /posts/qr-tokens?lineId=` groupé, v13) |
| Equipments | `oas_equipments` | — | 4 |
| Cadences | `oas_routings` | `oas_routing_versions` | 6 |
| Causes | `oas_causes` | `oas_cause_proposals` | 13 |
| Products | `oas_products`, `oas_production_orders` | — | 7 |
| Shifts | `oas_shift_templates`, `oas_shift_calendar` | `oas_shift_signoffs` | 8 |
| Teams | `oas_teams`, `oas_team_members` | — | 3 |
| Assignments | `oas_assignments` | `oas_presence_entries` | 12 |
| Operators (dans ShopFloorAuth) | `oas_users` | — | 6 |
| PostSessions | `oas_post_sessions` | — | 5 |
| Declarations | `oas_declarations` | — | 7 |
| Changeovers | `oas_changeovers` | — | 3 |
| Quality | `oas_quality_checks` | `oas_quality_check_templates`, `oas_quality_check_template_items` | 8 |
| Events | `oas_events`, `oas_event_transitions`, `oas_event_notifications` | — | 15 |
| Sla | `oas_routing_rules` | `oas_sla_rules`, `oas_escalations` | 6 |
| Interventions | — | `oas_interventions`, `oas_responder_availability` | 8 (6 + 2 disponibilité) |
| PostStates | `oas_post_states`, `oas_post_state_history` | — | 2 |
| Kpi | `oas_kpi_daily` | `oas_andon_messages` | 8 (dont `GET /kpi/sla-summary` et `GET /kpi/cadence-gap`, v13) |
| Imports | `oas_imports` | `oas_import_lines` | 5 |
| Integrations | — | `oas_integration_endpoints`, `oas_integration_outbox` | 6 |
| Lookups (OAS) | — | `oas_lookup_values` | 4 |
| Offline | `oas_sync_receipts`, `oas_device_tokens`, `oas_attachments`, `oas_audit_log` | — | 8 |
| **TOTAL** | **30** | **18** | **184** |

> **30 tables réutilisées** = les tables existantes de `db/migrations/003..007` (§5.1). Les tables d'identité du socle (`tenants`, `profiles`, `user_roles`) ne sont pas réutilisées : OAS utilise sa propre table `oas_users` (§5.2 #18).

### 6.1 Catalogue détaillé (préfixe `/api/oas`)

| Domaine | Endpoints | Source frontend / table |
|---|---|---|
| Sites | `GET /sites` · `POST /sites` · `PUT /sites/{id}` · `POST /sites/{id}/archive` (**v15, décision produit** : archive **en cascade** — zones/lignes/postes descendants passent aussi `archived=true` dans la même transaction ; le client actuel n'archive que la ligne ciblée, `hierarchyStore.ts:118-122,141-145,164-168`, laissant des enfants « actifs » sous un parent archivé, visibles et sélectionnables dans les listes déroulantes d'équipement/cadence — comportement à **ne pas** reproduire) | `hierarchyStore.ts:105,112,118` |
| Zones | `GET /zones?siteId=` · `POST /zones` · `PUT /zones/{id}` · `POST /zones/{id}/archive` (cascade sur lignes/postes descendants, même règle v15 que Sites) | `hierarchyStore.ts:128,135,141` |
| Lignes | `GET /lines?zoneId=` · `POST /lines` · `PUT /lines/{id}` · `POST /lines/{id}/archive` (cascade sur postes descendants, même règle v15 que Sites) | `hierarchyStore.ts:151,158,164` |
| Postes | `GET /posts?lineId=` · `POST /posts` · `PUT /posts/{id}` · `PUT /posts/{id}/attributes` · `PUT /posts/{id}/critical` · `POST /posts/{id}/archive` · `GET /posts/{id}/capacity` | `hierarchyStore.ts:174,188,198,202,208` ; `refStore.ts:419` |
| Arbre & résolution | `GET /hierarchy/tree` (site→zone→ligne→poste, une requête) · `GET /posts/{id}` (détail) · `GET /posts/by-code/{code}` (résolution d'un code scanné) | `HierarchyManager.tsx` ; `session.ts:141` (`parsePostCode`), `:153` (`findPost`) |
| Badge QR de poste | `GET /posts/{id}/qr-token` · `POST /posts/{id}/qr-token/rotate` · `GET /posts/qr-tokens?lineId=` (**v13**, variante groupée `{postId, token}[]` — évite N appels séquentiels pour la vue d'impression de badges, `Admin.tsx:92-114`) | `Admin.tsx:3,6` (`QRCodeSVG` sur `POSTS`) ; `oas_posts.qr_token` (`003:46`) — le jeton est **généré et tourné côté serveur**, jamais dérivé du code de poste |
| Complétude des référentiels | `GET /referentials/completeness` | `refStore.ts:514` (`completeness()`) — score par jeu de données, calculé serveur |
| Équipements | `GET /equipments?postId=` · `POST /equipments` · `PUT /equipments/{id}` · `DELETE /equipments/{id}` | `refStore.ts:400,414` |
| Cadences | `GET /cadences` · `POST /cadences` (nouvelle version) · `PUT /cadences/{id}` · `DELETE /cadences/{id}` · `GET /cadences/{id}/history` · `GET /cadences/current?postId=&productId=` | `refStore.ts:276,295` ; `oas_routing_versions` |
| Usage des causes | *(aucun endpoint dédié)* — `trackCauseUsage` (`refStore.ts:348`) disparaît : le compteur est **dérivé** de `oas_declarations`/`oas_events` et exposé par `GET /causes/usage`, **déjà compté dans la ligne « Causes »** (ne pas créer de route supplémentaire) | `refStore.ts:348` |
| Causes | `GET /causes` (arbre) · `POST /causes` · `PUT /causes/{id}` · `PUT /causes/{id}/kind` · `PUT /causes/{id}/criticality` · `PUT /causes/{id}/active` · `POST /causes/{id}/children` · `DELETE /causes/{id}/children/{childId}` · `DELETE /causes/{id}` · `GET /causes/usage` | `refStore.ts:537-604,348` |
| Propositions de cause | `GET /cause-proposals` · `POST /cause-proposals` · `POST /cause-proposals/{id}/review` | `refStore.ts:312,328` |
| Produits | `GET /products` · `POST /products` · `PUT /products/{id}` · `DELETE /products/{id}` | `refStore.ts:607,624` |
| Ordres de fabrication | `GET /production-orders` · `POST /production-orders` · `PUT /production-orders/{id}/status` | `oas_production_orders` (`003`) |
| Shifts | `GET /shifts` · `POST /shifts` · `PUT /shifts/{id}` · `DELETE /shifts/{id}` · `GET /shifts/calendar?from=&to=` · `PUT /shifts/calendar` | `refStore.ts:634,644,648,666,672` |
| Sign-off de poste | `POST /shift-signoffs` · `GET /shift-signoffs?shift=&date=` | `refStore.ts:437` |
| Opérateurs | `GET /operators?q=&scope=` (**v13** : `q=` recherche par nom, `scope=` filtre ligne/zone — requis par le sélecteur d'affectation, `Assignments.tsx:105-118`, `RosterPanel.tsx:39-41`, qui rendent aujourd'hui tout le répertoire sans filtre ; n'inclut **jamais** `pin`, décision v12) · `POST /operators` · `PUT /operators/{id}/active` · `PUT /operators/{id}/role` · `PUT /operators/{id}/scope` · `POST /operators/{id}/regenerate-pin` (seule route à renvoyer le PIN en clair, une fois) | `refStore.ts:358,371,378,384,390` |
| Équipes | `GET /teams` · `POST /teams` · `PUT /teams/{id}/members` | `oas_teams`, `oas_team_members` (`004`) |
| Affectations | `GET /assignments?shift=&date=` · `PUT /assignments/{postId}` · `DELETE /assignments/{postId}` · `POST /assignments/auto-fill` · `DELETE /assignments` · `POST /assignments/publish?postId=` (**v15, décision produit — publication par poste, pas globale** : `publishedAt` est aujourd'hui un **drapeau unique pour tout le plan** (`assignmentStore.ts:87-96,103-111,113-117,158-161`) — `assignPost`/`autoFill`/`clearAll` le remettent à `null` sur **n'importe quelle** modification d'un seul poste, ce qui dépublie instantanément **tout le plan** (`useLivePosts`, `ShopFloorBoard`, `AndonTv` perdent tous les opérateurs affichés, pas seulement celui du poste modifié) ; le serveur porte la publication **par poste** (`published_at` sur chaque ligne `oas_assignments`, pas un flag global) — modifier un poste ne dépublie que ce poste, `POST /assignments/publish` sans `postId` republie l'ensemble du plan comme avant) | `assignmentStore.ts:87,98,103,113,119` |
| Plan publié & compteurs | `GET /assignments/published?shift=&date=` · `GET /assignments/counts` · `GET /assignments/roster` | `assignmentStore.ts:158` (`publishedOperator`), `:167` (`assignmentCounts`), `:30` (`ROSTER`) |
| Présence | `PUT /presence/{operatorId}` · `POST /presence/{operatorId}/confirm` · `GET /presence?shift=&date=` | `assignmentStore.ts:130,148,163` |
| Sessions de poste | `POST /post-sessions/open` (**v15, garde obligatoire, ne pas reproduire le comportement actuel** : `openSession()` (`session.ts:194-229`) écrase silencieusement une session active dès que le shift correspond, sans confirmation — `ScanPage.tsx` n'a, contrairement à tous les autres écrans mobiles, **aucune** garde `if (session actif) rediriger`, et « Scanner » est un onglet permanent : un second scan pendant une session avec des déclarations non synchronisées les efface définitivement, sans qu'aucun `clientEventId` n'ait jamais atteint le serveur. Le serveur **doit** refuser (409) l'ouverture d'une nouvelle session tant qu'une session active existe pour l'opérateur/poste, sauf `relay` explicite) · `POST /post-sessions/{id}/relay` · `POST /post-sessions/{id}/close` · `GET /post-sessions/active` · `POST /post-sessions/scan` | `session.ts`, `ScanPage` |
| Déclarations | `POST /declarations/production` · `POST /declarations/scrap` · `PUT /declarations/{id}/correct` · `GET /declarations?postId=&sessionId=&from=&to=` (**v13** : `from=`/`to=` requis par `Reports.tsx:90-124` — période sélectionnable, agrégats `slaByService`/`cadenceGap`) · `GET /declarations/{id}` · `POST /declarations/stop` · `POST /declarations/stop/{clientEventId}/close` | `session.ts:333` (`declareProduction`), `:373` (`correctDeclaration`, fenêtre **10 min** — `CORRECTION_WINDOW_MIN` `session.ts:366`, garde `isCorrectable:369`, règle métier `BL-045`, confirmée décision produit v12, à revalider serveur **côté web ET mobile** : `CorrectionsPanel.tsx` console ne l'applique pas aujourd'hui, c'est un bug frontend à ne pas reproduire), `:404` (`declareStop`), `:558` (`closeStop`, **v15** : n'a aujourd'hui aucune garde d'idempotence — fermer un arrêt déjà fermé écrase `closedAt` avec un horodatage plus tardif, gonflant sa durée dans le Pareto/MTTR ; deux points d'appel mobiles peuvent double-déclencher sur le même arrêt via double-tap, `OperatorHome.tsx:199` et `DeclareProduction.tsx:92` ; le serveur doit traiter `POST /declarations/stop/{clientEventId}/close` sur un arrêt déjà fermé comme un **200 idempotent** renvoyant la ressource existante, jamais une réécriture) ; trigger `declarations_immutable`. **v15, autorisation à ajouter** : `correctDeclaration` (`session.ts:373-402`) n'a côté client **aucune** vérification que l'opérateur courant est bien l'auteur de la déclaration — un relais d'opérateur (`switchOperator`, la déclaration garde son état intact) permet à l'opérateur entrant de corriger les déclarations du sortant, et `correctedBy` prend par défaut l'opérateur **entrant** ; le serveur doit dériver l'auteur de la déclaration depuis l'identité authentifiée ayant émis le `POST` original (pas un champ éditable par le client) et n'autoriser la correction qu'à cet auteur ou à un rôle `supervisor`/`admin` |
| Changements de série | `POST /changeovers` · `PUT /changeovers/{id}/finish` (409 si checklist incomplète, §13 pt.7) · `GET /changeovers?postId=&status=` (**v13** : filtre de reprise après navigation/redémarrage — appuyé par l'index unique existant `uq_open_changeover(tenant_id, post_id) WHERE ended_at IS NULL`, `005:173-174`, au plus une ligne « open » possible) | `ChangeoverPage.tsx` ; `oas_changeovers` (`005:154`) — **v13** : la table doit gagner une colonne `steps jsonb not null default '[]'` pour persister la checklist en 5 étapes (`ChangeoverPage.tsx:25`, `done: boolean[]`), absente des 17 colonnes actuelles (`005:154-171`) et de tout autre champ candidat ; sans elle la reprise sait qu'un changement est ouvert mais pas quelles étapes sont cochées |
| Contrôles qualité | `POST /quality-checks` · `GET /quality-checks?postId=` · `GET /quality-check-templates` · `POST /quality-check-templates` · `PUT /quality-check-templates/{id}` · `DELETE /quality-check-templates/{id}` · `GET /quality-check-templates/{id}/items` · `PUT /quality-check-templates/{id}/items` | `oas_quality_checks` (`005:180`), `oas_quality_check_templates`, `oas_quality_check_template_items` |
| Événements (andon) | **v15, garde d'état obligatoire, ne pas porter la machine à états actuelle telle quelle** : le client aujourd'hui ne bloque **aucune** transition invalide — `requalifyEvent` (`eventStore.ts:212-216`) peut rouvrir un événement `stage:'resolved'` sans garde (repasse à `'notified'`), et `closeEvent` n'a aucune précondition (un événement peut sauter `declared`→`closed` sans jamais passer par `enroute`/`onsite`/`resolved`) ; le serveur **doit** refuser en **409** toute action `take`/`eta`/`arrive`/`advance`/`escalate`/`requalify` sur un événement déjà `stage:'closed'`, et documenter explicitement si `resolved` est ou non un état terminal avant confirmation manuelle (le client actuel le traite comme non-terminal, ce qui permet la réouverture accidentelle) ; `escalateEvent` (`eventStore.ts:206-209`) est aussi la seule action du store à ne **jamais** écrire dans l'audit contrairement à ses 5 voisines (`take`/`close`/`ack`/`requalify`/`decline`) — le serveur audite systématiquement les 13 actions, sans exception. `POST /events` (clientEventId obligatoire — voir note v13 ci-dessous) · `GET /events?kind=&stage=&q=&lineId=&from=&to=` (**v13** : filtres requis par `AlertsQueue.tsx:25-47` et `ShopFloorBoard.tsx:18`, qui filtrent aujourd'hui côté client après avoir reçu l'ensemble des événements ; `from=`/`to=` pour `Reports.tsx`, `ShiftReport.tsx:41-53`) · `GET /events/{id}` · `POST /events/{id}/take` · `PUT /events/{id}/eta` · `POST /events/{id}/arrive` · `POST /events/{id}/advance` · `POST /events/{id}/ack` · `POST /events/{id}/escalate` · `POST /events/{id}/requalify` · `POST /events/{id}/decline` (**v13** : doit appliquer le même effet que `/escalate` — `declineEvent()` actuel, `eventStore.ts:258-268`, bouton « occupé » `InterventionInbox.tsx:115`, fait les deux en un seul appel côté client ; sinon le client doit enchaîner `/decline` puis `/escalate`, à trancher au lot 5) · `POST /events/{id}/close` · `GET /events/{id}/transitions` | `eventStore.ts:99-258` — **v13** : `addEvent()` (`:106-109`) mint un id local séquentiel non-UUID (`live-${seq}`), seule source d'id pour `declareStop` (DeclareStop), `NeighborStop.pick` et tout appel `POST /events` — à corriger côté client avant idempotence réelle (lot 5) |
| Notifications d'événement | `GET /event-notifications?eventId=` · `POST /event-notifications/{id}/respond` | `oas_event_notifications` (`005:131`) |
| États de poste | `GET /post-states` (live) · `GET /post-states/{postId}/history` | `liveState.ts:38,150` ; trigger `oas_recompute_post_state` |
| KPI / OEE | `GET /kpi/daily?from=&to=` · `GET /kpi/pareto?from=&to=` · `GET /kpi/trend?from=&to=` · `GET /kpi/line-comparison?from=&to=` (scope site/zone/ligne/poste ; **v13** : `from=`/`to=` requis par le sélecteur de période de `Reports.tsx:19-53`, absent du contrat initial) · `GET /kpi/sla-summary?from=&to=&scope=` (**v13**, nouveau — compte on-time/late par type d'événement sur la période, `Reports.tsx:90-103` `slaByService`, formule à ajouter en §7.3) · `GET /kpi/cadence-gap?from=&to=&scope=` (**v13**, nouveau — écart production réelle/théorique par poste sur la période, `Reports.tsx:110-124` `cadenceGap`, formule à ajouter en §7.3) | `liveState.ts:122,130` ; remplace la série en dur `TRS_TREND` (`demo.ts`) |
| Plan de carte atelier | `GET /posts/layout?lineId=` · `PUT /posts/layout` | `ShopFloorMap.tsx:74` (géométrie en dur) ; `oas_post_layouts` |
| Message andon (MOTD) | `GET /andon/message?lineId=` · `PUT /andon/message` — **v13** : `lineId` **optionnel** (portée site quand omis) ; le comportement actuel (`AndonTv.tsx:15,30,110-114`) est un message unique **sans aucune association de ligne**, le contrat initial `?lineId=` obligatoire ne le couvrait pas | `AndonTv.tsx:15,30,111` (`localStorage` `oas.andon.motd`) ; `oas_andon_messages` |
| SLA | `GET /sla/rules` · `POST /sla/rules` · `PUT /sla/rules/{id}` · `DELETE /sla/rules/{id}` · `GET /sla/escalations?eventId=` · `POST /sla/escalations/{id}/ack` | `eventStore.ts:226,271-306` ; `oas_sla_rules`, `oas_escalations` |
| Interventions | `GET /interventions` · `POST /interventions` · `POST /interventions/{id}/assign` · `POST /interventions/{id}/start` · `POST /interventions/{id}/close` · `GET /interventions/inbox` | `InterventionInbox.tsx:50,88,107,115,130` — projection des `oas_events` pris en charge (§13) |
| Imports | `POST /imports` (dépôt du lot parsé côté client) · `GET /imports` · `GET /imports/{id}` · `GET /imports/{id}/lines` · `POST /imports/{id}/commit` | `refStore.ts:450` (`applyImport`) ; `oas_imports`, `oas_import_lines` (`007`). **v12** : `commit` **refuse en 400** tout `datasetType` non supporté par le serveur ; `ImportPanel.tsx` actuel accepte silencieusement les types `posts`/`orders` (le sélecteur les propose, `applyImport` ne les traite pas) et rapporte un compte de lignes sans rien écrire — comportement de perte silencieuse à ne **pas** reproduire côté serveur |
| Audit | `GET /audit?entity=&from=&to=&actor=&action=&q=` (**v13** : `actor=`, `action=`, `q=` requis par `Admin.tsx:37-69`, dont la recherche filtre simultanément acteur/action/entité/détail) — **lecture seule**, aucun POST : `logAudit` (`auditStore.ts:62`) est remplacé par le trigger `oas_audit_row` | `auditStore.ts:62,85` |
| Offline / sync | `POST /sync/push` (idempotent `clientEventId`) · `GET /sync/pull?since=` · `GET /sync/receipts` | `session.ts` ; `oas_sync_receipts` (`007`) |
| Pièces jointes | `POST /attachments` · `GET /attachments?entity=&id=` | `oas_attachments` (`007`) — peut déléguer à `/api/documents` |
| Push mobile | `POST /device-tokens` · `DELETE /device-tokens/{token}` | `oas_device_tokens` (`007`) — **[NON VÉRIFIÉ / lot 9 optionnel]** : aucun fournisseur push côté backend **et aucun code client** (0 occurrence de FCM/APNs/`PushNotifications` dans `src/`) |
| Intégrations | `POST /integrations/webhooks/in` · `GET /integrations/endpoints` · `POST /integrations/endpoints` · `PUT /integrations/endpoints/{id}` · `DELETE /integrations/endpoints/{id}` · `GET /integrations/outbox` | `oas_integration_endpoints`, `oas_integration_outbox` |
| Lookups OAS | `GET /lookups/{type}` · `POST /lookups/{type}` · `PUT /lookups/{type}/{id}` · `DELETE /lookups/{type}/{id}` | `oas_lookup_values` |
| Auth OAS | `POST /oas/setup` · `POST /api/oas/auth/login` · `POST /api/oas/auth/refresh` · `POST /api/oas/auth/logout` · `GET /api/oas/auth/me` · `POST /api/oas/auth/change-password` · `POST /api/oas/shopfloor/login` (corps polymorphe `{mode:"pin"}` ou `{mode:"qr"}`) · `POST /api/oas/shopfloor/pin/regenerate` | `WebLogin.tsx` ; `MobileLogin.tsx` (`PinPanel` / `ScanPanel`) ; `oas_users` (§8.1), `oas_posts.qr_token` (`003:46`) |
| **Activation des plugins** | `GET /plugin-activations` · `PUT /plugin-activations/{code}` · `POST /plugin-activations/bulk` | `activationStore.ts:17`, `PluginGate.tsx:41-48`, `registry.ts:11` ; `oas_plugin_activations` |
| **Disponibilité intervenant** | `GET /responder-availability` · `PUT /responder-availability/{profileId}` | `eventStore.ts:242-251` (`busy`), consommé par le moteur d'escalade `:286` ; `oas_responder_availability` |
| **Flux temps réel** | `GET /stream` (SSE, §6.3) | remplace `setInterval` de `eventStore.ts:299-306` et `session.ts:323-331` |

### 6.2 Contrat écran ↔ endpoint (aucune supposition permise)

Chaque écran de §1.5 est livré avec **exactement** les endpoints listés dans sa ligne : ni plus, ni moins. Règles de contrat :

1. Un écran ne consomme jamais un endpoint hors de sa ligne §1.5 ; si un besoin apparaît, il est ajouté d'abord ici, puis implémenté.
2. Toute liste paginée renvoie l'enveloppe `{ items, total, page, pageSize }` ; les écrans temps réel (`shopfloor`, `alerts`, `andon`, `inbox`, `home`) s'abonnent en plus à `GET /stream`.
3. Tout POST déclenché depuis le mobile porte `clientEventId` (idempotence) : `POST /events`, `/declarations/*`, `/changeovers`, `/quality-checks`, `/post-sessions/*`, `/interventions/*`.
4. Les écrans d'export (`reports`, `shift-report`, `shift-end`) n'appellent **aucun** endpoint d'export : la génération CSV/XLS/PDF reste client-side (`src/shared/lib/excel.ts`, `pdf.ts`).
5. Les écrans de référentiels utilisent uniquement les routes CRUD de §6.1 ; l'import XLSX est parsé côté client puis poussé en lignes via `POST /imports`.

### 6.3 Temps réel — remplacer les timers navigateur par le serveur

Aujourd'hui trois boucles tournent **dans le navigateur** et doivent disparaître :

| Boucle actuelle | Fichier:ligne | Remplacement backend |
|---|---|---|
| Balayage SLA / escalade N1-N2 toutes les **30 s** | `eventStore.ts:226,271-306` (`ENGINE_TICK_MS`, `runEscalationSweep`, `startEscalationEngine`) | `EscalationSweepHostedService` (30 s) : lit `oas_sla_rules` + `oas_responder_availability`, écrit `oas_escalations`, pousse sur SSE `/api/oas/stream` |
| Rappel de déclaration + clôture automatique de poste toutes les **60 s** | `session.ts:319-331` (`useDeclarationReminder`, `autoCloseEndedShift`) | `SessionWatchdogHostedService` (60 s) : ferme les `oas_post_sessions` dont le shift est terminé, émet le rappel — **v15, 2 bugs de la logique actuelle à corriger, pas à reproduire** : (1) la fenêtre de grâce (`session.ts:293-298`) mesure le temps écoulé **depuis l'ouverture de la session** (`now - session.startedAt`), pas depuis le changement de shift — pour une session ouverte en début de shift (le cas normal), la clôture automatique se déclenche à la **première minute** suivant le changement de shift au lieu d'une grâce de ~15 min ; le service doit mesurer depuis l'**heure de fin du shift** (`oas_shift_templates`), jamais depuis `startedAt` ; (2) toute clôture — automatique **ou** manuelle (`ShiftEnd.tsx:126-136`, `closeSession()` `session.ts:269-280`) — supprime aujourd'hui la file d'événements/déclarations non synchronisés sans aucun flush préalable ; le service doit **garantir** que tout élément `clientEventId` en attente est d'abord synchronisé (ou explicitement rejeté avec erreur) avant de fermer la session — jamais un simple `commit(EMPTY)` silencieux |
| Sync simulée `setTimeout` + `Math.random() < 0.2` | `session.ts:504-508`, `flushPending:530-543` | `POST /api/oas/sync/push` réel (idempotent), retry côté client sur erreur réseau **vraie** |

`GET /api/oas/stream` : SSE authentifié (JWT en query ou header), filtré par tenant + périmètre `OasScopeFilter`, événements `event.created`, `event.updated`, `event.escalated`, `post_state.changed`, `kpi.updated`, `andon.message`, `session.reminder`. SSE est le **seul** canal temps réel OAS : aucun hub SignalR dédié. Heartbeat toutes les 15 s (`: ping`), reconnexion avec `Last-Event-ID`.

---


## 7. SELECTS PILOTÉS PAR DONNÉES — PLUS AUCUNE LISTE EN DUR

**Règle : tout `<Select>` se remplit depuis une source serveur.** Deux sources seulement, jamais un tableau TypeScript.

### 7.1 Constantes frontend à supprimer (14 au total : 9 sélecteurs + 5 jeux de données de démo)

| Constante | Fichier:ligne | Consommée par | Remplacement |
|---|---|---|---|
| `LINE_KEYS` | `src/oas/demo.ts:51` | `UsersPanel.tsx:13,94`, `ShopFloorMap.tsx:74`, `AndonTv.tsx:53,254`, `scope.ts:25` | **Entité** `GET /api/oas/lines` |
| `POSTS` | `src/oas/demo.ts:62` | carte atelier, affectations | **Entité** `GET /api/oas/posts?lineId=` |
| `ROSTER` / `BOARD_POSTS` | `src/oas/assignmentStore.ts:30,31` | `Assignments.tsx` | **Entités** `/operators`, `/posts` |
| `STOP_REASONS` | `src/oas/demo.ts:123` | `DeclareStop`, `NeighborStop`, `InterventionInbox:76`, `refStore.ts:187,518` | **Entité** `GET /api/oas/causes` |
| `ROLE_KEYS` | `src/oas/refStore.ts:22` | `UsersPanel.tsx:45,86` | **Socle** `GET /api/roles` |
| `PRESENCES` | `src/oas/assignmentStore.ts:19` | `RosterPanel` | **Lookup OAS** `PresenceStatus` |
| `DB_SHIFTS` | `src/oas/fixtures.ts` → `refStore.ts:221` | `ShiftCalendars` | **Entité** `GET /api/oas/shifts` |
| `TRS_TREND` (série TRS en dur) | `src/oas/demo.ts` | `ManagerDashboard.tsx:3`, `Reports.tsx:4` | **Agrégat** `GET /api/oas/kpi/trend` |
| Géométrie de la carte + MOTD andon | `ShopFloorMap.tsx:74`, `AndonTv.tsx:15` | carte atelier, écran andon | `GET/PUT /api/oas/posts/layout`, `GET/PUT /api/oas/andon/message` |
| `KPI` (TRS/dispo/perf/qualité en dur) | `src/oas/demo.ts:133` | `liveState.ts:96-118`, `ManagerDashboard`, `MobileKpi`, `AndonKpiRail` | **Agrégat** `GET /api/oas/kpi/daily` |
| `PARETO` | `src/oas/demo.ts:152` | `liveState.ts:133`, `ManagerDashboard`, `Reports` | **Agrégat** `GET /api/oas/kpi/pareto` |
| `LINE_COMPARISON` | `src/oas/demo.ts:160` | `liveState.ts:156`, `ManagerDashboard` | **Agrégat** `GET /api/oas/kpi/line-comparison` |
| `EVENTS` (événements de démo) | `src/oas/demo.ts:109` | amorce de `eventStore.ts` | **Entité** `GET /api/oas/events` |
| `kindToState` (table de correspondance) | `src/oas/demo.ts:166` | `liveState.ts:51,58` | **Reste en dur** : miroir des enums `oas_event_kind` → `oas_post_state` (§5.3), le serveur applique la même table dans le trigger `oas_recompute_post_state` |

`EVENT_STAGES` et `STATES` (`demo.ts`) **restent en dur** : ils reflètent les enums Postgres et les triggers (§5.3).

**`src/oas/fixtures.ts` — statut explicite (levée d'ambiguïté v9)** : les tableaux exportés (`tenants`, `sites`, `zones`, `lines`, `posts`, `equipments`, `products`, `productionOrders`, `profiles`, `userRoles`, `causes`, `events`, `postStates`, `kpiDaily`, `shiftTemplates`) ne sont **pas** des sources de `<Select>` mais un **jeu de démonstration miroir des tables `oas_*`**. Traitement imposé : le fichier n'est **pas** transformé en lookups ; il est converti en **seed serveur** (`OasDemoSeeder`, joué uniquement pour les tenants `dev*`/`demo*`, jamais en production), puis **supprimé du bundle client** au lot 8. Aucun écran ne doit encore l'importer une fois les endpoints branchés.

### 7.2 Listes plates → lookups OAS (`oas_lookup_values.type`)

`PostType` · `PostCriticality` · `EquipmentType` · `CadenceUnit` · `ProductFamily` · `PackagingUnit` · `ScrapMotif` · `QualityDefect` · `ChangeoverType` · `PresenceStatus` · `AbsenceReason` · `InterventionOutcome` · `ImportSource` · `ShiftLabel` · `SiteType` · `ZoneType`.

`LookupsController` du socle n'expose que des routes en dur (`article-categories`, `priorities`, `leave-types`, `currencies`…) : **on ne l'étend pas** (§3.2). OAS sert ses propres listes via `api/oas/lookups/*`.


### 7.3 Formules KPI à porter à l'identique côté serveur

Ces calculs sont aujourd'hui exécutés dans le navigateur. Le backend doit produire **exactement les mêmes valeurs** (mêmes arrondis, mêmes gardes anti-division-par-zéro), sinon les écrans changent de chiffres à la bascule.

| KPI | Formule vérifiée | Source |
|---|---|---|
| Minutes d'ouverture | `OPENING_MIN = 480` (constante de poste) — **v15, précision** : `480` est un **défaut**, pas une vérité fixe (déjà énoncé plus bas dans ce paragraphe) ; le client actuel ne relie **jamais** ce nombre au calendrier de shift réellement configuré (`ShiftCalendars.tsx:24-25` prétend le contraire dans son propre commentaire) — le serveur **doit** dériver les minutes d'ouverture depuis `oas_shift_templates`/`oas_shift_calendar` du poste concerné, avec `480` comme valeur de repli seulement si aucun shift n'est configuré | `liveState.ts:25` |
| Disponibilité | `clamp0_100(((openingMin - stopMinutes) / openingMin) * 100)` avec `openingMin` = minutes d'ouverture réelles du shift (ligne ci-dessus), pas la constante `480` en dur comme le fait le client aujourd'hui (`liveState.ts:103`) | `liveState.ts:103` |
| Qualité | `clamp0_100((producedOk / max(1, producedOk + producedNok)) * 100)` | `liveState.ts:102` |
| Performance | **⚠ non calculée dans le code** : `liveState.ts:104` est `const performance = KPI.performance;`, c'est-à-dire la constante de démonstration `demo.ts:136` (valeur `87`). **Ne pas porter cette valeur en dur.** Formule serveur imposée : `clamp0_100((quantitéTotale / max(1, ((480 - stopMinutes) / 60) × cadence)) × 100)` avec `cadence` = `oas_routings.rate` du produit courant (défaut `60`, comme `session.ts:209`) ; si aucune cadence n'est connue pour le poste, renvoyer `performance = null` et `cadenceKnown = false` (le champ existe déjà, `liveState.ts:111`) — l'UI doit alors masquer le TRS plutôt qu'afficher un chiffre faux | `liveState.ts:104` (constante) ; `demo.ts:136` ; cadence `session.ts:209`, `refStore.ts:276` |
| **TRS / OEE** | `clamp0_100((availability * performance * quality) / 10 000)` | `liveState.ts:107` |
| MTTR (min) | `stopsCount === 0 ? 0 : max(1, round(stopMinutes / stopsCount))` (**v15, corrigé** — `liveState.ts:116` renvoie `1` même à zéro arrêt via `max(1, …)` appliqué avant le test, un défaut trompeur à ne pas porter : une ligne sans arrêt doit afficher **0**, pas « 1 min ») | `liveState.ts:116` |
| Pareto | somme des minutes perdues par cause : `max(1, round((closedAt ?? now - declaredAt) / 60 000))`, tri décroissant | `liveState.ts:130-146` |
| Comparaison de lignes | `trs = round((trsHistorique + trsLive) / 2)` ; `scrap = (totalNok / (totalOk + totalNok)) * 100` arrondi à 1 décimale — **v15, ne pas porter le blend 50/50** : `liveState.ts:163` moyenne à parts égales une valeur de démo statique (`trsHistorique`) et la TRS de la session live en cours, non pondérée par le temps écoulé ni le volume — une session ouverte depuis 2 minutes fait déjà basculer la TRS affichée de la ligne à mi-chemin vers ce seul opérateur ; ce comportement est un artefact du jeu de données de démo (pas de vraie donnée « historique » séparée de la donnée « live » une fois le backend branché) — le serveur calcule directement la TRS réelle sur la période demandée à partir de `oas_kpi_daily`, sans blend arbitraire | `liveState.ts:150-167` |
| Quantité suggérée | `max(0, round((runMin / 60) * cadence))`, `cadence` = `oas_routings.rate` du produit courant, défaut `60` | `session.ts:209,584,612` |
| Complétude du paramétrage | 5 ratios (postes, produits nommés, références avec cadence, causes, couverture des 3 shifts) — **v15, 2 bugs corrigés, ne pas porter tels quels** : (a) le ratio « causes cartographiées » (`refStore.ts:519`, `reasonsMapped`) filtre sur un champ `kind` **obligatoire** sur toutes les causes → toujours 100 %, quel que soit l'état réel des données ; le serveur doit soit calculer un vrai ratio sur un critère qui varie réellement (ex. cause reliée à un `oas_event_kind` valide), soit retirer cette ligne du score plutôt que d'afficher un 100 % fictif ; (b) le ratio « couverture des 3 shifts » (`refStore.ts:520,529`) divise par une constante **`3`** en dur alors que le numérateur compte les shifts actifs réellement configurés (`oas_shift_templates`) — un 4ᵉ shift actif produit `4/3 = 133 %`, jamais clampé ; le serveur doit diviser par le nombre de shifts **attendus** (paramètre, pas littéral `3`) et clamper `[0,100]` | `refStore.ts:509-531` |
| Minutes d'ouverture quotidiennes | somme des shifts actifs, avec **passage de minuit** (`fin < début` ⇒ +1440) — **v15, cas limite corrigé** : un shift `début == fin` (`refStore.ts:637-638`, `diff=0`) traverse la même condition (`diff > 0 ? diff : diff+1440`) et devient **1440** (24h) au lieu de **0** ; un shift à durée nulle doit être rejeté en validation (`début ≠ fin` obligatoire) ou traité explicitement comme `0`, jamais comme un shift de 24h par défaut silencieux | `refStore.ts:634-674` |
| Cibles SLA par type d'événement | table `SLA_TARGET_MIN` (`eventStore.ts:36`) → devient `oas_sla_rules`, escalade N1 à `elapsed ≥ target` ou intervenant occupé, N2 à `elapsed ≥ 2 × target` — **v15, 2 bugs corrigés, ne pas porter tels quels** : (a) le temps écoulé est arrondi (`Math.round`, `eventStore.ts:230-239`) au lieu d'être tronqué vers le bas — un événement de 4 min 31 s avec une cible de 5 min arrondit à 5 et escalade **~29 s avant** l'échéance réelle ; le serveur doit utiliser un floor/troncature, pas un arrondi, sur toute comparaison `elapsed ≥ target` ; (b) l'escalade peut sauter directement à N2 sans jamais passer par N1 (`eventStore.ts:276-295` et confirmé côté mobile, `InterventionInbox`/sweep 30 s) si le premier balayage après ouverture survient déjà au-delà de `2 × target` (onglet en arrière-plan, tablette throttlée) — le serveur doit forcer une progression séquentielle 0→1→2, jamais 0→2 directement, même si le temps écoulé dépasse déjà le seuil N2 au premier passage | `eventStore.ts:281-286` |
| Disponibilité intervenant | drapeau « occupé » — **v15, bug critique corrigé, ne pas porter** : le client mobile actuel n'utilise **aucune identité réelle** pour ce drapeau — `CURRENT_RESPONDER` (`eventStore.ts:33`) est une **constante en dur** (`'Karim T.'`), jamais l'opérateur réellement connecté (`getAuth()?.name` n'est appelé nulle part dans `InterventionInbox.tsx:50,107,115,130`) ; en conséquence, décliner l'événement A marque « occupé » un nom partagé par tous les appels mobiles, faisant à tort passer en escalade tout événement B assigné au même nom générique alors que les intervenants réels diffèrent. Le serveur **doit** dériver l'identité du JWT authentifié (`oas_users.id`, jamais un champ envoyé par le client) pour `PUT /responder-availability/{profileId}` et pour l'assignation d'événement — ce comportement actuel est un artefact de démo à corriger dès le lot 5, pas un contrat de parité | `eventStore.ts:33,242-268` |
| SLA par service **(v13, ajouté)** | pour chaque `kind` d'événement sur la période : compte total, compte « dans les temps » (`elapsedMin ≤ target`), ratio ; exposé par `GET /kpi/sla-summary?from=&to=&scope=` | `Reports.tsx:90-103` (`slaByService`) |
| Écart de cadence **(v13, ajouté)** | par poste sur la période : quantité réelle vs quantité théorique (`(runMin / 60) × cadence`, même `cadence` que « Quantité suggérée » ci-dessus), écart en % ; exposé par `GET /kpi/cadence-gap?from=&to=&scope=` | `Reports.tsx:110-124` (`cadenceGap`) |

Le backend expose ces valeurs déjà calculées (`GET /kpi/*`) ; le frontend ne recalcule plus rien. Les seuils (`480`, `60`, cibles SLA) deviennent des paramètres persistés (`oas_shift_templates`, `oas_routings`, `oas_sla_rules`), avec ces valeurs comme défauts.


---

## 8. AUTH, ACCÈS ET UTILISATEURS — LE MODÈLE RÉEL DU FRONTEND

### 8.0 Ce que le frontend fait réellement (vérifié — ne pas supposer autre chose)

**Il n'existe aucune chaîne de permission dans `src/`.** Le contrôle d'accès repose sur **trois mécanismes indépendants** :

1. **Garde d'espace de travail** — `WebApp.tsx:105` (`if (!auth || auth.workspace !== 'web')`) et `MobileApp.tsx:315` (`!== 'mobile'`) : un utilisateur connecté à un espace ne peut pas entrer dans l'autre.
2. **Liste blanche de rôles console** — `CONSOLE_ROLES` (`authStore.ts:71-73`) = `team_lead, maintenance, quality, prod_manager, process_engineer, director, hr_admin`. Un compte `operator` reçoit `noAccess` sur la console (`authStore.ts:96`). Un compte inactif reçoit `inactive`.
3. **Portail de plugin** — `PluginGate` (`PluginGate.tsx:41-48`) : chaque route est enveloppée par un code plugin ; désactivé ⇒ panneau « module désactivé » (ou redirection). Les plugins `isCore` ne peuvent jamais être coupés (`activationStore.ts:58`).

**Traduction backend obligatoire :**

| Mécanisme frontend | Implémentation serveur |
|---|---|
| `workspace` (`web` / `mobile`) | claim `oas_workspace` dans le JWT ; JWT atelier `aud = "oas-shopfloor"`, JWT console `aud = "oas-console"`. Un jeton atelier est refusé sur les routes console et inversement (`OasWorkspaceAuthorizationFilter`) |
| `CONSOLE_ROLES` | mapping en rôles OAS : `admin` (directeur, hr_admin, prod_manager), `supervisor` (team_lead, maintenance, quality, process_engineer), `operator`. `GET /api/oas/operators` expose `role` + `isActive` ; un rôle `operator` ne reçoit jamais de jeton console |
| `PluginGate` | `oas_plugin_activations` + `GET /api/oas/plugin-activations` ; **chaque contrôleur OAS vérifie l'activation de son plugin** et renvoie **404** si coupé (cohérent avec le kill-switch §3.4) |
| Périmètre site/zone/ligne | colonnes `scope_site_id`, `scope_zone_id`, `scope_line_id` sur `oas_users` appliquées par `OasScopeFilter` avant toute lecture |

Les rôles OAS sont portés par `oas_users.role` (enum `oas_app_role` : `admin`, `supervisor`, `operator`). Aucune table de rôles séparée, aucune permission granulaire.

### 8.1 Table `oas_users` — identité OAS autonome

```sql
create table public.oas_users (
    id uuid primary key default gen_random_uuid(),
    tenant_id int not null default 0,
    source_user_id int,          -- mapping optionnel vers Users.Id du tenant parent
    source_tenant_id int,        -- tenant d'origine dans la base source
    email varchar(255) not null,
    password_hash varchar(255),  -- console login (BCrypt)
    pin varchar(20),               -- atelier login (texte, par choix utilisateur)
    qr_token varchar(255),         -- badge QR poste (unique)
    role oas_app_role not null default 'operator',
    workspace oas_workspace not null default 'mobile', -- 'web' | 'mobile' | 'both'
    display_name varchar(255),
    phone varchar(50),
    avatar_url text,
    scope_site_id uuid references public.oas_sites(id),
    scope_zone_id uuid references public.oas_zones(id),
    scope_line_id uuid references public.oas_lines(id),
    is_active boolean not null default true,
    failed_login_attempts int not null default 0,
    locked_until timestamptz,
    last_login_at timestamptz,
    last_synced_at timestamptz,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    IsDeleted boolean not null default false,
    unique (tenant_id, email),
    unique (tenant_id, qr_token)
);
```

**Règles :**
- `password_hash` = BCrypt, **jamais renvoyé**.
- `pin` = stocké en texte (choix explicite : un opérateur ne peut pas réinitialiser seul, le chef d'équipe le relit/régénère). `pin` est **optionnel** jusqu'à la première affectation. **Décision produit v12** : `pin` n'est **jamais** inclus dans la réponse de `GET /operators` (ni en liste, ni en détail) — seul `POST /operators/{id}/regenerate-pin` le renvoie, une seule fois, au même titre que `password_hash`. Le comportement actuel de `UsersPanel.tsx` (colonne PIN visible en permanence dans le tableau) est un écart à corriger, pas un contrat à reproduire.
- `qr_token` = UUID généré à la création du poste, utilisé pour le badge QR.
- Verrouillage après 5 échecs PIN ou mot de passe ; `locked_until` déverrouillé automatiquement après 15 min.

### 8.2 Endpoints d'authentification OAS

Tous sous `POST /api/oas/auth/*`. Aucune réutilisation des endpoints `/api/auth/*` du socle.

| Besoin | Endpoint | Corps / détail |
|---|---|---|
| **Setup initial (une seule fois)** | `POST /oas/setup` | `{ email, password, displayName }` — **non authentifié**, crée le premier `oas_users` avec `role = admin`. Refuse si au moins un admin existe déjà. |
| Connexion console | `POST /api/oas/auth/login` | `{ email, password, workspace: "web" }` → JWT `aud=oas-console`, `oas_workspace=web`, `oas_role=admin\|supervisor` — **déclenche `OasUserJitSyncService` (§8.3) avant vérification du mot de passe** |
| Connexion atelier PIN | `POST /api/oas/shopfloor/login` | `{ mode: "pin", badge, pin }` → JWT `aud=oas-shopfloor`, `oas_workspace=mobile`, `oas_role=operator\|supervisor` — **déclenche `OasUserJitSyncService` (§8.3) avant vérification du PIN** |
| Connexion atelier QR | `POST /api/oas/shopfloor/login` | `{ mode: "qr", token }` — `token` = `oas_posts.qr_token` ; authentifie l'opérateur lié au poste — **déclenche `OasUserJitSyncService` (§8.3)** |
| Session courante | `GET /api/oas/auth/me` | retourne l'utilisateur courant + rôle + périmètre |
| Renouvellement | `POST /api/oas/auth/refresh` | refresh token valide → nouveau access + refresh — **déclenche `OasUserJitSyncService` (§8.3)** : une révocation côté socle coupe l'accès dès le prochain refresh |
| Déconnexion | `POST /api/oas/auth/logout` | révoque le refresh token côté serveur |
| Changement mot de passe | `POST /api/oas/auth/change-password` | `{ currentPassword, newPassword }` — authentifié |
| Régénération PIN | `POST /api/oas/shopfloor/pin/regenerate` | admin/supervisor uniquement ; retourne le nouveau PIN en clair **une seule fois** |

### 8.3 Synchronisation depuis la base source — à la demande (JIT), aucun polling

**Décision v11** : la synchronisation n'est **plus** un `IHostedService` périodique. Elle est portée par `OasUserJitSyncService` (service *scoped*, injecté dans `AuthController`/`ShopFloorAuthController`), déclenché **dans la requête de connexion elle-même**, avant vérification du mot de passe/PIN/QR. Objectif : zéro délai de propagation, ni pour l'octroi ni pour le retrait d'accès.

**Points d'appel** (les 3 seuls) :
- `POST /api/oas/auth/login` (console) — clé de recherche : `email`.
- `POST /api/oas/shopfloor/login` (atelier, modes `pin`/`qr`) — clé de recherche : `badge` (matricule) ou résolution inverse du `qr_token`.
- `POST /api/oas/auth/refresh` — resynchronise le titulaire du refresh token, pour qu'une révocation côté socle coupe l'accès **au prochain refresh**, sans attendre l'expiration du JWT.

**Algorithme, par tentative de connexion** :
1. Chercher `oas_users` par `(tenant_id, email)` ou `(tenant_id, badge)`.
2. **Cache court** : si la ligne existe, a un `source_user_id` non nul et `now() - last_synced_at < Oas:JitSyncTtlSeconds` (défaut **300 s**) → utiliser la ligne telle quelle, **aucun accès à la base source** (évite un aller-retour Npgsql à chaque connexion d'un atelier à fort trafic).
3. Sinon (absente, expirée, ou jamais synchronisée) → résoudre la base source du tenant parent (§1.2 bis) via `ITenantDbContextFactory.GetConnectionString(baseSlug)`, puis lire **uniquement l'utilisateur ciblé** (`WHERE email = @email` ou équivalent badge) dans `public.users` + `public.user_roles` + `public.profiles` — jamais un scan de table complet.
4. Utilisateur trouvé côté source **et** porteur d'un rôle OAS (`oas_mobile`, `oas_supervisor`, `oas_admin`) → upsert dans `oas_users` par `(tenant_id, source_user_id, source_tenant_id)` : `email`, `display_name`, `phone`, `avatar_url`, `is_active = true`, `role` (mapping §8.0), `workspace`, `scope_*` copiés depuis `user_roles.scope_*`, `last_synced_at = now()`. La connexion continue normalement avec la ligne fraîche.
5. Utilisateur absent côté source, ou présent mais sans rôle OAS → si une ligne `oas_users` existait déjà pour ce `source_user_id`, la marquer `is_active = false` immédiatement ; la tentative de connexion échoue en **401** `invalid_credentials` (même réponse qu'un compte inexistant — pas de fuite d'information sur l'existence du compte).
6. Les comptes créés manuellement via `/oas/setup` ou la console (`source_user_id = null`) **ne passent jamais** par ce flux : ils s'authentifient directement contre `oas_users`, sans lecture de la base source.
7. Aucun appel HTTP au socle : lecture directe Npgsql sur la base source, dans la même requête.

**Compromis assumé** : la fenêtre de `Oas:JitSyncTtlSeconds` (300 s par défaut) ne concerne que les connexions **répétées** d'un compte déjà synchronisé récemment — c'est un cache de performance, pas un délai de propagation ; l'octroi ou le retrait d'un rôle OAS est visible dès la prochaine connexion (ou refresh) qui suit un cache expiré ou une première connexion. Une session déjà ouverte (JWT non expiré) n'est révoquée qu'au prochain `refresh`, pas immédiatement mid-session — si une révocation instantanée mid-session est requise, c'est un besoin distinct (liste de révocation / TTL de JWT court), à trancher séparément.

---

## 9. SÉCURITÉ — LOT 0, BLOQUANT AVANT TOUT BRANCHEMENT

| # | Risque | Preuve | Gravité |
|---|---|---|---|
| 1 | Mot de passe maître en dur permettant de se connecter à **n'importe quel compte** (`MASTER_LOGIN_PASSWORD`, défaut `"Admin@2026@"`) | déclaration `AuthService.cs:1029-1030`, bypass `:1040-1043` dans `VerifyPassword` (donc tous les chemins de login) | **Critique** |
| 1 bis | **Second contournement** : tout compte dont le hash vaut littéralement `"nopassword"` s'authentifie avec le mot de passe `"nopassword"` — et `signup` crée de tels comptes | `AuthService.cs:1035-1038` (`if (hashedPassword == "nopassword" && password == "nopassword") return true;`), création `:476` | **Critique** |
| 2 | JWT à **10 ans** + `ValidateLifetime = false` | `AuthService.cs:997`, `:1210` ; `Program.cs:283` | **Critique** |
| 3 | Clé JWT committée (`appsettings.json:3`) et repli en dur `"YourSuperSecretKeyHere12345"` en 4 endroits, sans échec au démarrage | `Program.cs:276`, `TokenHelper.cs:12,41`, `AuthService.cs:975,1184` | **Critique** |
| 4 | Chaîne Postgres Neon avec identifiants réels committée | `Backend/appsettings.Development.json:3` | **Critique** |
| 5 | `render.yaml` **ne contient que des commentaires** (aucun `services:`/`envVars:`) et documente `JWT_KEY` alors qu'ASP.NET lit `Jwt:Key` (`Jwt__Key`) → la prod risque de signer avec le repli du point 3 | `render.yaml:9` vs `Program.cs:276` | **Critique** |
| 6 | CORS `AllowAnyOrigin` + filet inline `Access-Control-Allow-Origin: *` | `Program.cs:498-523`, `:1376-1398` | Élevée |
| 7 | Aucun rate limiting sur `/api/auth/login` (0 occurrence de `AddRateLimiter`) | seul compteur : webhooks 60/min (`ExternalEndpointService.cs:397-415`) | Élevée |
| 8 | PIN opérateur en clair dans `localStorage` | `src/oas/refStore.ts:212-215,429-434` | Élevée |
| 9 | Mot de passe console en dur `secret123`, accepté pour **tout** compte du répertoire | `src/oas/authStore.ts:79,95` | Élevée |
| 10 | Soft-delete sans filtre global : tout oubli de `!IsDeleted` expose des données supprimées | `ApplicationDbContext.cs:432,448` (2 `HasQueryFilter`, tous deux tenant) | Moyenne |
| 11 | Activation des modules décidée **côté client** (`localStorage` `oas.plugins.activations.v1`) : n'importe qui rouvre un module coupé depuis la console navigateur | `activationStore.ts:17,58`, `PluginGate.tsx:41-48` | Moyenne — corrigée par `oas_plugin_activations` + refus **404** serveur (§8.0) |

**Les points 1 à 5 sont corrigés au lot 0, avant toute écriture de code OAS.** Les points 8, 9 et 11 disparaissent avec la bascule serveur (lots 2, 1 et 1). OAS ne les contourne pas et n'en ajoute pas de nouveaux.

---

## 10. FEUILLE DE ROUTE — ORDRE D'EXÉCUTION IMPOSÉ

| Lot | Contenu | Dépend | Livrable |
|---|---|---|---|
| **0 — Sécurisation** | Retirer le mot de passe maître **et le contournement `nopassword` (§9.1 bis)** ; JWT ≤ 8 h + `ValidateLifetime = true` + refresh réel ; sortir clés et chaînes de connexion des fichiers committés ; écrire un vrai `render.yaml` (`Jwt__Key`) ; restreindre CORS aux origines connues ; `AddRateLimiter` sur `/auth/*` ; supprimer/conditionner `test-db` et `test-signup` | — | Backend sain |
| **1 — Fondation OAS + client** | **Livraison et exécution manuelle des fichiers SQL `public/OAS-SQL/001..004` (§5.0)** ; **routage tenants `*oas` (§1.2 bis) : `IOasDbContextFactory` par slug, 503/400 fail-closed** ; `OAS/Common` (`OasDbContext` + filtre global `tenant_id`, `OasControllerBase`, `OasScopeFilter`, `OasSseBroadcaster`, `AddOasModule`), garde-fous d'isolation §3.4, doc Swagger `oas`, **`oas_plugin_activations` + 3 endpoints + refus 404 par plugin coupé (§8.0)**, **`POST /oas/setup` + `POST /api/oas/auth/login` + `GET /api/oas/stream`** ; côté frontend : client HTTP (base URL, `Authorization`, `X-Tenant` = slug `*oas`, `X-Target-Tenant`, gestion 401/428/503) et bascule d'`activationStore` sur le serveur | Lot 0 | Module vide déployable + login réel + activation serveur |
| **2 — Identité atelier** | `POST /api/oas/shopfloor/login` **unique** (modes `pin` texte / `qr` via `oas_posts.qr_token`), `POST /api/oas/shopfloor/pin/regenerate`, les 6 routes `Operators` (création, actif, rôle, `scope`, PIN **jamais renvoyé par `GET /operators`, v12**), table `oas_users`, claim `oas_workspace`, scope site/zone sur les rôles, `OasUserJitSyncService` (sync à la demande dans `login`/`refresh`, §8.3). **Frontend, v12** : `PinPanel`/`ScanPanel`/`WebLogin`/`LoginForm` n'ont aujourd'hui aucun état de chargement, timeout, ni distinction d'erreur (PIN invalide / hors-ligne / serveur indisponible / compte révoqué) — la sync JIT introduit un aller-retour réseau réel, ce travail UI est requis avant de brancher le lot, pas cosmétique | Lot 1 | Login opérateur |
| **3 — Référentiels** | Hierarchy (dont `oas_post_layouts`), Equipments, Cadences, Causes, Products, Shifts, Teams, Imports (**rejet 400 des `datasetType` non supportés, v12**), **Lookups OAS** ; bascule `refStore`/`hierarchyStore` ; suppression des constantes en dur (§7.1). **Risque de séquencement, v12** : `ShopFloorMap`/`AndonTv`/`liveState.ts`/`BOARD_POSTS` et `Admin.tsx`/`ShiftReport.tsx` continuent de lire leurs fixtures figées jusqu'aux lots 4/5/7 — une fenêtre existe où les référentiels réels divergent visiblement de la carte atelier/andon/rapports tant que ces écrans ne sont pas basculés, ne pas la confondre avec une régression | Lot 2 | Console référentiels branchée |
| **4 — Exploitation** | PostSessions (**`clientEventId` sur `open`/`relay`/`close`, absent du client aujourd'hui, v13**), Assignments/Presence (**clé `operator_id`, jamais le nom affiché — `assignmentStore.ts` actuel matche par chaîne de nom, à ne pas reproduire, v12**), Declarations (correction tracée, fenêtre **10 min** appliquée **aussi côté `CorrectionsPanel` console**, v12), Changeovers (**checklist imposée serveur, 409 si incomplète, §13 point 7, v12 ; colonne `steps jsonb` + filtre `postId=&status=`, v13**), Quality ; bascule `session.ts`/`assignmentStore`. **Frontend, v13** : `OperatorSession` (`session.ts:39`) ne porte qu'un nom (`operator: string`), jamais d'`operatorId` — `openSession`/`switchOperator` doivent véhiculer l'id pour pouvoir appeler les routes indexées (`PUT /presence/{operatorId}`, `POST /operators/{id}/...`) | Lot 3 | Déclarations réelles |
| **5 — Andon & temps réel** | Events + transitions + moteur SLA propre à OAS (`OasSlaWorker`), **`EscalationSweepHostedService` 30 s, `SessionWatchdogHostedService` 60 s, `GET /stream` SSE (§6.3)** ; PostStates live, Interventions + `oas_responder_availability` ; bascule `eventStore` ; suppression des `setInterval` navigateur. **Frontend, v12** : `NeighborStop.tsx` et `InterventionInbox.tsx` écrivent aujourd'hui en synchrone sans drapeau `synced`, sans file de retry et avec des ids locaux séquentiels non-UUID (contrairement à `session.ts` déclarations/stops) — brancher la file hors-ligne + `clientEventId` sur ces deux écrans fait partie du lot, pas un simple remplacement d'appel | Lot 4 | Andon temps réel |
| **6 — Offline** | `api/oas/sync/*` (idempotence `clientEventId`, `oas_sync_receipts`), remplacement de la simulation `setTimeout`/`Math.random()` | Lot 4 | File offline réelle |
| **7 — KPI & audit** | Agrégats `oas_kpi_daily` **suivant les formules §7.3 à l'identique**, Pareto, tendance, comparaison de lignes, `oas_andon_messages` ; `oas_audit_log` ; sign-off de fin de poste ; Integrations | Lot 5 | Dashboards & rapports |
| **8 — Bascule finale du client** | Retrait du bundle client de **toutes** les données simulées : `src/oas/fixtures.ts` et les constantes de `demo.ts` (§7.1) supprimées et servies comme **seed serveur** pour les seuls tenants `dev*`/`demo*` ; suppression du mot de passe console en dur (`authStore.ts:79`) et du PIN en clair (`refStore.ts:214`) ; les 10 clés `localStorage` métier ne conservent que le cache offline et les préférences UI ; `grep -r "from '@/oas/fixtures'" src/` doit renvoyer **0 résultat** | Lot 7 | Frontend 100 % serveur |
| **9 — Optionnel [NON VÉRIFIÉ]** | Push mobile (`oas_device_tokens` + fournisseur) et biométrie (`profiles.biometric_enrolled`) — **aucun code client aujourd'hui** : à ne construire que sur demande explicite | Lot 8 | Hors périmètre par défaut |

---

## 11. CRITÈRES D'ACCEPTATION (définition de « terminé »)

Pour **chaque lot** :

1. `dotnet build` sans warning nouveau ; `dotnet test` vert, y compris les tests d'architecture §3.4.
2. `python3 scripts/inventory_controllers.py` : le socle ne varie pas en dehors des 3 lignes de `Program.cs`. Les nouvelles actions OAS sont comptées séparément.
3. Toutes les nouvelles routes commencent par `api/oas/` (test automatisé au démarrage).
4. `git diff --name-only` ne montre **aucun** fichier hors `Backend/Modules/OAS/`, `docs/`, `scripts/`, `src/`, à l'exception des 3 lignes de `Program.cs`.
5. Aucune table **sans préfixe `oas_`** n'a été créée, altérée ou supprimée (diff de schéma) ; **100 % des tables du module portent le préfixe `oas_`**.
6. Chaque endpoint est couvert par : un test d'intégration nominal, un test 403 (permission absente), un test d'isolation tenant (données d'un autre tenant invisibles).
7. Tout POST créant un fait est rejoué deux fois avec le même `clientEventId` → une seule ligne en base, deux réponses 200 identiques.
8. Swagger `oas` documente **les 184 actions** (comptage littéral du catalogue §6.1, `GET /causes/usage` n'étant listé qu'une fois) (ni plus, ni moins) avec DTO d'entrée/sortie et codes 200/400/401/403/404/409/428.
9. Aucun secret, clé ou chaîne de connexion en clair dans un fichier committé.
10. Les endpoints frontend correspondants sont branchés et la constante `localStorage` remplacée est supprimée du code.
11. **Contrat d'écran (§6.2)** : chaque écran livré n'appelle que les endpoints listés dans sa ligne de §1.5 ; un test e2e par écran vérifie qu'aucun appel hors contrat n'est émis.
12. **Parité KPI (§7.3)** : pour un jeu de données figé, les valeurs renvoyées par `GET /kpi/*` sont **strictement égales** à celles calculées par `liveState.ts` avant bascule (test de non-régression chiffré).
13. **Plugin coupé ⇒ 404** : pour chacun des 13 codes, un test désactive le plugin et vérifie que toutes les routes du sous-module renvoient 404.
14. **Tenants OAS (§1.2 bis)** : un slug `*oas` non provisionné renvoie 503 en production ; un slug non-`oas` sur `api/oas/*` renvoie 400 ; deux bases OAS (`demooas`, `krossieroas`) sont mutuellement invisibles (données, caches, événements temps réel) ; les migrations `oas_*` sont appliquées et idempotentes **sur chaque base**.

---

## 12. RÈGLES DE TRAVAIL POUR L'AGENT

1. **Ne jamais inventer.** Chaque affirmation sur le code existant doit être vérifiée par lecture de fichier (`fichier:ligne`). Si non vérifiable, écrire `[NON VÉRIFIÉ]`.
2. **Ne jamais modifier** un fichier hors `Backend/Modules/OAS/`, `docs/`, `scripts/` ou `src/` — hors les 3 lignes de `Program.cs` et le lot 0 (sécurité) (**v16, corrigé** — cette règle contredisait jusqu'ici le critère d'acceptation §11.4, qui autorise explicitement `docs/`, `scripts/` et `src/` dans le diff, et la feuille de route §10, où **chaque lot** assigne des tâches frontend explicites — sync JIT, états de chargement du login, file hors-ligne, `operatorId`, etc. — jusqu'au lot 8 qui est entièrement frontend. La règle ne visait qu'à protéger le **socle backend** existant, pas à interdire tout le frontend ; formulée littéralement, elle aurait fait refuser par l'agent la quasi-totalité des tâches que la feuille de route lui assigne). Reste absolu : aucun fichier du socle (`Backend/Modules/*` hors `OAS`, et hors le lot 0 sécurité) n'est jamais modifié.
3. **Un lot à la fois**, dans l'ordre. Ne pas commencer le lot N+1 avant que les 14 critères du lot N soient verts (**v14, corrigé** — §11 en compte 14 depuis l'ajout du critère 14 sur les tenants OAS, cette ligne disait encore 13).
4. **Préférer les triggers SQL existants** à une réimplémentation C# : immutabilité des déclarations, SLA, recalcul d'état de poste, audit.
5. **Petits fichiers, responsabilité unique** : un contrôleur par ressource, un service par cas d'usage ; pas de service « fourre-tout ».
6. **Toujours** préfixe de table `oas_`, `uuid`, `snake_case`, `archived_at`, `clientEventId`, `X-Tenant` + `X-Target-Tenant`, `ProblemDetails`. **Aucune RLS Postgres, aucun rôle `authenticated`/`service_role`, aucun `auth.uid()`** (§5.0).
7. **Aucune dépendance** vers les 68 fichiers ignorés (§2.2), même transitive. Un `using MyApi.Modules.WorkflowEngine…` est un échec de revue.
8. À chaque fin de lot, régénérer `docs/ANNEXE-INVENTAIRE-CONTROLEURS.md` et joindre le diff des chiffres.
9. **Ne pas inventer de surface produit** : les 22 écrans de §1.5 sont la totalité du périmètre. Pas d'écran d'admin supplémentaire, pas d'export serveur, pas de push, pas de biométrie.
10. **Ne rien recalculer côté client après bascule** : toute formule de §7.3 vit désormais dans le backend.

---

## 13. POINTS OUVERTS

1. **[RÉSOLU — SQL manuels]** Les fichiers `public/OAS-SQL/001..004` sont exécutés une fois par l'opérateur sur chaque base `*oas` vierge. Si une base contient déjà des tables sans préfixe (héritage Supabase), un script de reprise unique `public/OAS-SQL/000_rename_legacy.sql` est fourni : `alter table <t> rename to oas_<t>` + suppression des policies RLS. L'application ne lance **aucun** runner de migrations automatique.

2. **[NON VÉRIFIÉ — lot 9]** Push mobile : aucun fournisseur (FCM/APNs) ni côté backend ni côté client ; `oas_device_tokens` (`007`) reste une table sans producteur. Ne pas l'activer sans décision produit.

3. **[NON VÉRIFIÉ — lot 9]** Biométrie : `profiles.biometric_enrolled` (`002:27`) n'est lu par aucun code. Colonne conservée, flux non construit.

4. **[À TRANCHER]** Disposition de la carte atelier : aujourd'hui purement ordinale (grille CSS, `ShopFloorMap.tsx:87`). `oas_post_layouts` prévoit `x`/`y` pour un futur plan libre ; le lot 3 ne livre que `sort_order` + `layout_key` sauf demande explicite d'un éditeur de plan.

5. **[RÉSOLU — SSE uniquement]** Le temps réel OAS passe exclusivement par `GET /api/oas/stream` (SSE). Aucun hub SignalR `/hubs/oas` n'est créé.

6. **[RÉSOLU — formule imposée]** Performance = `clamp0_100((actual_rate / planned_rate) * 100)` où `planned_rate` = `oas_routings.rate` du produit courant sur le poste (défaut 60). Si la cadence n'est pas connue, renvoyer `performance = null` et `cadenceKnown = false`.

7. **[DÉCISION — routes dédiées + application serveur v12]** Le backend implémente les 3 routes `POST /changeovers`, `PUT /changeovers/{id}/finish`, `GET /changeovers` sur la table `oas_changeovers`. Le frontend `/mobile/changeover` est basculé dessus au lot 4. Le total d'endpoints est **184** (v13 : `GET /changeovers` gagne `postId=&status=`, aucune route ajoutée ici ; v14 : correction de comptage sans rapport avec ce point, voir §6). **Décision produit v12** : `PUT /changeovers/{id}/finish` **refuse (409)** si les étapes de la checklist ne sont pas toutes complètes — le contournement actuel doit disparaître : aujourd'hui `OperatorHome.tsx` expose un bouton générique « reprendre production » qui appelle `closeStop()` sur **n'importe quel** arrêt ouvert, y compris un changement de série (`ChangeoverPage.tsx:44`, code `'CS-01'`) abandonné avant la fin de sa checklist en 5 étapes. Le lot 4 doit soit retirer ce poste des cibles valides du bouton générique, soit router sa fermeture vers `PUT /changeovers/{id}/finish` (qui applique la garde).

8. **[À TRANCHER — v10]** **Interventions vs Events** : `InterventionInbox.tsx` pilote le cycle de vie via les actions d'événement (`take`, `eta`, `arrive`, `decline`, `close`), pas via un domaine séparé. Cible retenue par défaut : `oas_interventions` est une **projection en lecture** (`GET /interventions`, `/inbox`) d'un `oas_event` pris en charge, et les mutations restent sur `/events/{id}/*` ; dans ce cas les 3 routes `POST /interventions/{id}/assign|start|close` deviennent des alias documentés — à confirmer avant le lot 5, elles ne doivent pas dupliquer la machine à états.

9. **[v10 — à câbler, pas une question]** Deux besoins du frontend n'ont **pas** de déclencheur UI aujourd'hui et doivent en recevoir un pendant le lot correspondant : (a) `POST /shift-signoffs` depuis `/mobile/shift-end` (seule la console signe, `ShiftReport.tsx:235`) ; (b) `POST /posts/{id}/qr-token/rotate` depuis l'onglet QR de la console (`Admin.tsx`, aucun bouton de rotation). Les deux endpoints restent au catalogue : ils sont requis par la sécurité (rotation de badge) et par la conformité (signature opérateur).

10. **[v10 — fait vérifié, pas un point ouvert]** `assignmentStore.confirmPresence` (`:148`) n'est appelé que par le mobile (`OperatorHome.tsx:78`), jamais par la console ; `POST /presence/{operatorId}/confirm` est donc bien un endpoint mobile et non un doublon de `PUT /presence/{operatorId}` (console, `RosterPanel.tsx:69`).

11. **[RÉSOLU — v11]** L'ancien modèle (`OasUserSyncHostedService` en polling 60 s) exposait un risque non traité si le déploiement Render tourne un jour sur plusieurs instances : chaque instance aurait exécuté son propre cycle de sync, upserts concurrents sans conflit destructeur mais travail redondant. En passant à `OasUserJitSyncService` déclenché dans le flux de connexion (§8.3), ce risque disparaît : il n'y a plus de tâche de fond à dupliquer, chaque instance ne synchronise que l'utilisateur qui se connecte via elle. Ce raisonnement ne s'applique **pas** aux deux `IHostedService` restants (`EscalationSweepHostedService`, `SessionWatchdogHostedService`, §6.3) : eux tournent toujours en polling périodique et **doivent** être audités pour le mono-instance avant le lot 5 si Render passe un jour à plusieurs instances (leader election ou verrou distribué à ajouter, sinon double escalade / double clôture de session).

---

## 14. RÉFÉRENCES DU DÉPÔT

- `docs/04-BACKEND-REUSE-PLAN.md` — plan complet v4 (source de ce prompt).
- `docs/ANNEXE-INVENTAIRE-CONTROLEURS.md` — 95 fichiers / 993 actions, verbe, route, méthode, autorisation, numéro de ligne.
- `scripts/inventory_controllers.py` — générateur (`--markdown` / `--json`). Tous les chiffres en sont issus.
- `docs/01-SPEC-FRONTEND.md`, `docs/02-SPEC-DATABASE.md`, `docs/03-BACKLOG-TRACEABILITY.md` — spécifications produit.
- `public/OAS-SQL/001..004` — schéma OAS de référence (**49** tables — §5.0 : 30 réutilisées + 18 créées + `oas_schema_migrations` —, 18 enums, 17 fonctions, 14 triggers).
