# Backlog ↔ Frontend traceability (source: 09-product-backlog.xlsx)

Audit du 8 août 2026 — vérification écran par écran (web + mobile) et lecture du code.
Légende : ✅ conforme · 🟡 partiel (UI présente, règle métier incomplète) · ⛔ manquant

## M1 — Référentiels
| ID | Story | Statut | Écart constaté |
|----|-------|--------|----------------|
| BL-001 | Hiérarchie site→zone→ligne→poste | 🟡 | `hierarchyStore.ts` n'a ni `type`, ni `process`, ni `capacité` sur le poste (poste critique OK) |
| BL-002 | Machines & moules | 🟡 | Pas d'historique de montage machine/moule |
| BL-003 | Références + listes fermées | 🟡 | Un seul champ `ref` (pas réf client **et** code interne) |
| BL-004 | Cadences réf×équipement + conversion | ✅ | |
| BL-005 | Versionnage des cadences | ✅ | |
| BL-006 | Mode dégradé TRS-lite | 🟡 | Liste « sans cadence » seulement dans l'écran référentiels, pas d'état « non calculable » au niveau KPI |
| BL-007 | Arbres de causes 2 niveaux | ✅ | |
| BL-008 | Cause manquante → file de revue | ✅ | |
| BL-009 | Calendriers 2×8/3×8/4×8 + pauses | ✅ | |
| BL-010 | Import Excel multi-onglets | 🟡 | Seul le **premier onglet** est lu (`ImportPanel.tsx`), rapport d'erreurs et idempotence OK |
| BL-011 | Indicateur de complétude | ✅ | |

## M2 — Comptes
| ID | Story | Statut | Écart constaté |
|----|-------|--------|----------------|
| BL-012 | 8 rôles × niveau × périmètre | 🟡 | Pas de dimension « niveau » ; libellés de rôle non personnalisables (i18n figé) |
| BL-013 | Annuaire RH sans accès KPI | ✅ | |
| BL-014 | Activation matricule + code à usage unique, PIN, biométrie | 🟡 | Le code d'activation et le PIN sont le même champ ; pas d'écran de choix du PIN ; biométrie = icône seulement |
| BL-015 | Régénération du code | ✅ | |
| BL-016 | Une identification par prise de poste | 🟡 | Auto-clôture OK ; pas d'unicité réelle par poste/équipe |
| BL-017 | Tablette partagée ≤3 s + changement d'utilisateur | ✅ | Bouton « Changer d'opérateur » (menu mobile) → matricule + PIN ; le poste, la file et les arrêts restent ouverts (`session.switchOperator`) |
| BL-018 | Manager email+mdp / biométrie mobile | 🟡 | Email+mdp OK (mdp démo) ; pas de parcours biométrique mobile |

## M3 — Affectations & sessions
| ID | Story | Statut | Écart constaté |
|----|-------|--------|----------------|
| BL-019 | Confirmation Présent/Absent | 🟡 | Bouton « je suis à mon poste » seulement, pas d'« Absent », pas de notification avant poste |
| BL-020 | 3 listes vivantes (confirmés / sans réponse / absents) | ✅ | `RosterPanel` à 3 états + compteur confirmés/sans réponse/absents sur `Assignments` |
| BL-021 | Réaffectation en cours de poste | 🟡 | Possible mais non notifiée, non historisée (pas de `logAudit`), pas d'alerte de capacité |
| BL-022 | Ouverture de session par scan QR | ✅ | |
| BL-023 | Capacité >1 au prorata + 3 clôtures | ✅ | Capacité éditable par poste (`hierarchyStore.setPostAttributes`) ; clôtures fin de poste / relève / poste à l'arrêt dans `ShiftEnd` |

## M4 — Déclarations
| ID | Story | Statut | Écart constaté |
|----|-------|--------|----------------|
| BL-024 | Relevé périodique pré-rempli + rebuts | ✅ | |
| BL-025 | Relevé reporté + écran consolidé | ✅ | |
| BL-026 | Arrêt en 2 taps, causes triées par fréquence | 🟡 | Sélection d'équipement optionnelle absente |
| BL-027 | Clôture par type + blocage production | 🟡 | Blocage OK ; `closeStop()` sans type simple/intervention/qualité |
| BL-028 | Changement de série | 🟡 | Lancé par l'opérateur (pas par le chef) ; clôture via checklist 5 étapes au lieu d'un tap « Série terminée » |
| BL-029 | Arrêt poste voisin par scan sans session | ✅ | |
| BL-030 | Confirmation des saisies incohérentes | ✅ | |

## M5 — Moteur d'événements
| ID | Story | Statut | Écart constaté |
|----|-------|--------|----------------|
| BL-031 | Notification simultanée service + chef | 🟡 | Requalification OK ; aucun destinataire multiple, aucun canal de notification |
| BL-032 | « J'arrive (+ETA) » / « Occupé » + scan arrivée | 🟡 | ETA/Occupé OK ; arrivée = bouton simple, pas de scan QR |
| BL-033 | SLA, re-routage zone, escalades 1/2, saut de niveau la nuit | 🟡 | SLA 5/10 min + escalades OK ; pas de re-routage zone, pas de saut de niveau nocturne |
| BL-034 | « Vu » + groupage anti-rafale | 🟡 | « Vu » OK ; groupage absent |
| BL-035 | Taux de respect SLA par service | ✅ | |

## M6 — Carte & Andon
| ID | Story | Statut | Écart constaté |
|----|-------|--------|----------------|
| BL-036 | Carte grille par zone | 🟡 | Groupement par **ligne**, pas de filtre zone en premier |
| BL-037 | Panneau de détail + actions | 🟡 | Escalade/requalif/Vu OK ; action « reprise qualité » absente |
| BL-038 | Andon TV | 🟡 | Rotation 12 s / 6 s au lieu de 15 s ; pas de séquence carte/KPI/arrêts ; reconnexion simulée (`Math.random`) |

## M7 — Indicateurs & rapports
| ID | Story | Statut | Écart constaté |
|----|-------|--------|----------------|
| BL-039 | Journée d'équipes 06:00→06:00 | ✅ | |
| BL-040 | TRS-lite→TRS, MTTR/MTBF, Paretos | ✅ | |
| BL-041 | « Mes résultats » opérateur | ✅ | |
| BL-042 | Dashboards par rôle | ✅ | |
| BL-043 | Rapport de poste + PDF | ✅ | |
| BL-044 | Export brut Excel/CSV | 🟡 | Exports agrégés ; pas d'export ligne à ligne des déclarations et des scans |

## M8/M9 — Audit & transverses
| ID | Story | Statut | Écart constaté |
|----|-------|--------|----------------|
| BL-045 | Correction sous 10 min | 🟡 | Parcours opérateur OK ; aucune UI de correction chef/responsable au-delà des 10 min |
| BL-046 | Rétention 2 ans + export avant purge | 🟡 | Journal append-only OK ; pas de rétention par date, pas d'export avant purge |
| BL-047 | Hors-ligne, file persistante | ✅ | |
| BL-048 | FR/AR RTL | 🟡 | FR/EN/AR + RTL OK ; langue stockée en local, pas rattachée au compte |
| BL-049 | PWA, clair/sombre | 🟡 | Manifest + SW OK ; pas de rafraîchissement périodique 5-10 s, pas de `theme_color` sombre |

## Correctifs appliqués pendant l'audit
- Boucle de redirection infinie sur toute route mobile inconnue (`/mobile/declare` → `/mobile/declare/home/home/home…`) : `Navigate` relatif remplacé par `/mobile/home` (`src/mobile/MobileApp.tsx`).

## Synthèse
17 stories conformes · 32 partielles · 0 manquante — BL-017, BL-020 et BL-023 sont livrées.
