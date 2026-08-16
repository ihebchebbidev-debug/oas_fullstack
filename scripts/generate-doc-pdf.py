#!/usr/bin/env python3
"""Génère complete_pdf.pdf : documentation fonctionnelle détaillée (FR) de la suite OAS.

Usage: python3 scripts/generate-doc-pdf.py [dossier_captures] [sortie.pdf]
Les captures sont produites par les scripts Playwright (/tmp/shots par défaut).

Structure d'un écran documenté :
    (fichier, titre, route, rôles, [fonctionnalités], [logique & règles], données)
"""
import os
import subprocess
import sys

from PIL import Image
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (Image as RLImage, ListFlowable, ListItem,
                                PageBreak, Paragraph, SimpleDocTemplate,
                                Spacer, Table, TableStyle)

SHOTS = sys.argv[1] if len(sys.argv) > 1 else "/tmp/shots"
OUT = sys.argv[2] if len(sys.argv) > 2 else "/mnt/documents/complete_pdf.pdf"


def reg(name, query):
    path = subprocess.check_output(["fc-match", "-f", "%{file}", query], text=True).strip()
    pdfmetrics.registerFont(TTFont(name, path))


reg("DJ", "DejaVu Sans")
reg("DJB", "DejaVu Sans:bold")
reg("DJI", "DejaVu Sans:italic")

INK = colors.HexColor("#111827")
GREY = colors.HexColor("#6B7280")

H1 = ParagraphStyle("H1", fontName="DJB", fontSize=21, leading=26, textColor=INK, spaceAfter=6)
H2 = ParagraphStyle("H2", fontName="DJB", fontSize=14, leading=18, textColor=INK, spaceAfter=3)
H3 = ParagraphStyle("H3", fontName="DJB", fontSize=8.6, leading=11, textColor=colors.HexColor("#374151"),
                    spaceBefore=3, spaceAfter=2)
SUB = ParagraphStyle("SUB", fontName="DJI", fontSize=8.4, leading=11, textColor=GREY, spaceAfter=4)
BODY = ParagraphStyle("BODY", fontName="DJ", fontSize=9.3, leading=13, textColor=colors.HexColor("#1F2937"))
LI = ParagraphStyle("LI", parent=BODY, fontSize=8.3, leading=11.4)
NOTE = ParagraphStyle("NOTE", fontName="DJ", fontSize=7.8, leading=10.5, textColor=GREY)
SECT = ParagraphStyle("SECT", fontName="DJB", fontSize=28, leading=34, textColor=INK, spaceAfter=10)

MOBILE = [
    ("entry", "Écran d'entrée — choix de l'espace", "/", "Tous les utilisateurs",
     ["Deux portes d'entrée exclusives : « Application opérateur » (usage terrain sur tablette/téléphone) et « Console de supervision » (usage bureau).",
      "Sélecteur de langue global FR / EN / TN (arabe tunisien) présent dès le premier écran, avant toute authentification.",
      "Bouton « Télécharger l'application Android (APK) » avec mention explicite « Installation directe — hors Play Store ».",
      "Résumé des capacités de chaque espace directement sur les cartes (scan de poste, déclaration d'arrêt / affectations, cartographie, TRS)."],
     ["Le choix d'espace est mémorisé dans le stockage local : au prochain lancement l'utilisateur arrive directement dans le bon espace, avec un lien « Changer d'espace » toujours disponible.",
      "La langue est persistée sous la clé `oas.lang` et propagée à l'ensemble des écrans, y compris la direction RTL pour l'arabe.",
      "Dans l'APK Capacitor, cet écran est la première vue chargée ; le service worker est désactivé pour éviter tout cache incohérent dans le shell natif."],
     "Préférences locales : espace choisi, langue."),
    ("entry-apk-modal", "Modale de téléchargement APK", "/ (modale)", "Client / opérateur installant l'application",
     ["Explication en 3 langues du fait que l'application n'est pas distribuée sur le Play Store et doit être installée manuellement.",
      "Procédure numérotée : télécharger le fichier, ouvrir le fichier .apk, autoriser l'installation depuis cette source, puis « Installer quand même » sur l'écran Play Protect.",
      "Capture d'écran de l'avertissement Google Play Protect intégrée pour lever le doute au moment critique de l'installation.",
      "Bouton de téléchargement direct et bouton d'annulation ; note de sécurité rappelant que le fichier est fourni par l'éditeur."],
     ["L'URL de l'APK est centralisée dans `src/config/download.config.ts` : une seule valeur à modifier pour publier une nouvelle version, sans toucher aux composants.",
      "Tous les libellés passent par le dictionnaire i18n (en / fr / ar) : aucune chaîne codée en dur.",
      "Mise en page responsive : la capture est plafonnée en largeur sur petit écran et la modale défile verticalement pour rester utilisable sur téléphone.",
      "L'image de référence est embarquée dans les assets de l'application (pas de dépendance à un CDN externe)."],
     "Aucune donnée métier — écran d'aide à l'installation."),
    ("mob-login-pin0", "Connexion opérateur — matricule", "/mobile/login", "Opérateur, régleur, technicien",
     ["Deux modes d'authentification proposés côte à côte : « Scanner mon badge » (QR) et « Matricule + PIN ».",
      "Champ matricule et pavé numérique tactile pour le code PIN à 4 chiffres.",
      "Sélecteur de langue accessible depuis l'en-tête, sans être connecté.",
      "Lien « Changer d'espace » pour revenir au choix mobile / console."],
     ["Les cibles tactiles sont surdimensionnées (hauteur ≥ 72 px) pour un usage avec gants, en environnement bruyant et mal éclairé.",
      "Le matricule identifie l'opérateur dans le référentiel du site ; le PIN, dérivé du matricule dans le jeu de démonstration, sert de second facteur simple.",
      "Aucune géolocalisation ni score individuel : l'outil est un outil de pilotage de flux, pas de surveillance des personnes — le rappel figure en pied d'écran."],
     "Session opérateur : identité, équipe, shift."),
    ("mob-login-filled", "Connexion opérateur — PIN saisi", "/mobile/login", "Opérateur",
     ["Affichage masqué du PIN, chiffre par chiffre, avec touche d'effacement.",
      "Bouton principal « Prendre mon poste » qui reste inactif tant que la saisie est incomplète.",
      "Message d'aide rappelant la règle de composition du PIN de démonstration."],
     ["La validation ouvre la session : rattachement automatique à l'équipe et au shift en cours d'après le paramétrage des plages horaires.",
      "En cas d'échec, le message d'erreur est générique et ne précise pas quel champ est faux (bonne pratique de sécurité).",
      "Après authentification, la navigation est forcée vers le scan du poste : impossible de déclarer quoi que ce soit sans poste rattaché."],
     "Événement de prise de poste horodaté."),
    ("mob-login-scan", "Connexion par badge QR", "/mobile/login (scan)", "Opérateur",
     ["Ouverture de la caméra pour scanner le badge personnel de l'opérateur.",
      "Bouton de simulation de scan pour la démonstration et la formation.",
      "Lien « Impossible de scanner ? » qui bascule vers la saisie matricule + PIN."],
     ["Connexion en une seconde, sans clavier : c'est le mode nominal en production, notamment en début de shift quand plusieurs opérateurs se connectent en même temps.",
      "Si la caméra est indisponible (permission refusée, matériel sans caméra, APK sans autorisation accordée), un repli manuel est proposé automatiquement au lieu d'un écran bloquant.",
      "Les badges QR sont générés depuis la console d'administration, ce qui garantit l'unicité des codes."],
     "Session opérateur ouverte par badge."),
    ("mob-scan", "Scan du poste", "/mobile/scan", "Opérateur, technicien",
     ["Écran de scan du QR collé sur la machine, avec cadre de visée.",
      "Action « Signaler un arrêt sur un autre poste » pour dépanner un collègue sans prendre sa session.",
      "Action « Saisir le code manuellement » en repli.",
      "Barre de navigation basse permanente : Poste, Atelier, Scan, Alertes, Suivi."],
     ["Étape obligatoire après connexion : elle ancre la session au poste physique, ce qui évite ensuite toute saisie de contexte (ligne, machine, OF).",
      "Ce rattachement conditionne la qualité des données : chaque arrêt, chaque quantité et chaque alerte sont automatiquement liés au bon poste et au bon opérateur.",
      "Si l'application est ouverte sans session poste, tous les écrans d'action (arrêt, production, changement de série) redirigent vers ce scan."],
     "Liaison poste ↔ opérateur ↔ shift."),
    ("mob-scan-manual", "Saisie manuelle du code poste", "/mobile/scan (manuel)", "Opérateur",
     ["Champ de saisie du code poste (format PO-XXX) avec suggestions cliquables des postes du site.",
      "Bouton « Ouvrir la session » qui verrouille le poste choisi.",
      "Message d'erreur explicite si le code est inconnu du référentiel."],
     ["Repli utilisé quand l'étiquette QR est arrachée, sale ou illisible, ou lorsque la caméra n'est pas disponible.",
      "Le code saisi est validé contre le référentiel des postes : impossible d'ouvrir une session sur un poste inexistant.",
      "La session ouverte reste active pour la durée du shift, jusqu'à la clôture ou la relève."],
     "Session poste (code poste, heure d'ouverture)."),
    ("mob-post-opened", "Poste ouvert — accueil opérateur", "/mobile/home", "Opérateur",
     ["Bandeau de confirmation de présence : « Vous êtes affecté au poste PO-103 » + bouton « Je suis à mon poste ».",
      "Carte poste : code, ligne, état d'affectation, état courant (Production) et durée depuis le dernier changement d'état.",
      "Bloc OF : ordre de fabrication, référence produit, avancement (pièces / objectif), cadence théorique, pièces rebutées, taux qualité.",
      "Quatre actions rapides : Déclarer arrêt, Production, Manque matière, Changement série."],
     ["La confirmation de présence sépare l'affectation théorique (faite par le chef d'équipe) de la présence réelle, indispensable au calcul du temps d'ouverture.",
      "Les actions affichées dépendent de l'état du poste : en arrêt, l'écran propose la reprise ; en changement de série, il propose la checklist.",
      "L'avancement et le taux qualité sont recalculés à chaque saisie et alimentent directement le TRS visible côté console."],
     "État du poste, avancement de l'OF, présence opérateur."),
    ("mob-home2", "Accueil — poste en production", "/mobile/home", "Opérateur",
     ["Vue de conduite du poste pendant la production : état, OF, cadence, avancement et rebuts en un écran.",
      "Accès direct à « Fin de poste » depuis la carte poste.",
      "Indicateur de synchronisation dans l'en-tête (« À jour » ou nombre de saisies en file)."],
     ["Chaque action génère un événement horodaté (poste, opérateur, shift, cause) : c'est la matière première du calcul TRS et des rapports.",
      "Le mode hors-ligne met les saisies en file locale et les rejoue dès le retour du réseau — l'atelier n'est jamais bloqué par une coupure Wi-Fi.",
      "Aucun champ libre obligatoire : tout est codifié, ce qui garantit des statistiques comparables entre équipes et entre lignes."],
     "Événements de production et de changement d'état."),
    ("mob-menu", "Menu latéral mobile", "/mobile (panneau)", "Opérateur, technicien",
     ["Sélection de la langue (FR / EN / TN) en cours de session.",
      "Bascule de thème (clair / sombre) pour les ateliers peu éclairés.",
      "Lancement de la démo guidée sonore.",
      "Changement d'opérateur et déconnexion."],
     ["Le menu regroupe tout ce qui n'est pas une action de production, afin de garder l'écran principal épuré et sans risque d'appui accidentel.",
      "Le changement de langue est immédiat et n'interrompt pas la session en cours.",
      "La démo guidée sert à la formation des nouveaux arrivants sans mobiliser un formateur."],
     "Préférences utilisateur (langue, thème)."),
    ("mob-switch-user", "Changement d'opérateur (relève)", "/mobile (modale)", "Opérateur entrant / sortant",
     ["Modale de relève : saisie du matricule et du PIN du nouvel opérateur sur la même tablette.",
      "Rappel explicite : « Aucune session ouverte sur cette tablette » ou identité de l'opérateur sortant.",
      "Pavé numérique identique à celui de la connexion, pour éviter tout réapprentissage."],
     ["Le poste et l'ordre de fabrication restent ouverts : seule l'identité change, ce qui évite de perdre le contexte machine lors des pauses et des relèves.",
      "La traçabilité est préservée : les événements déjà enregistrés restent attribués à l'opérateur sortant, les suivants au nouvel arrivant.",
      "Cas d'usage typiques : pause déjeuner, relève d'équipe, remplacement ponctuel par un polyvalent."],
     "Historique d'occupation du poste par opérateur."),
    ("mob-stop2", "Déclaration d'arrêt — catégories", "/mobile/stop", "Opérateur",
     ["Quatre familles d'arrêt en grandes tuiles : Arrêt technique, Attente matière, Arrêt qualité, Changement série.",
      "Chaque tuile indique le nombre de causes disponibles derrière elle.",
      "Rappel du contexte en haut d'écran (poste, OF) pour éviter les déclarations sur le mauvais poste.",
      "Lien « Cause absente de la liste » pour signaler un motif non prévu."],
     ["Déclaration en deux appuis seulement : famille puis cause — objectif de moins de 5 secondes pour ne pas dissuader la déclaration.",
      "La famille choisie détermine le service destinataire de l'alerte (maintenance, logistique, qualité, régleurs) et le SLA appliqué.",
      "Le lien « cause absente » alimente l'amélioration du référentiel sans bloquer l'opérateur."],
     "Événement d'arrêt (famille, heure de début)."),
    ("mob-stop-causes", "Choix de la cause codifiée", "/mobile/stop (causes)", "Opérateur",
     ["Liste des causes de la famille sélectionnée avec leur code (TC-01 Panne machine, TC-02 Réglage outil, TC-03 Maintenance préventive…).",
      "Bouton « Retour » pour corriger la famille sans repartir de zéro."],
     ["La codification centralisée est la clé de la fiabilité statistique : deux opérateurs différents déclarant le même problème produisent la même donnée.",
      "Les codes alimentent les Pareto d'arrêts de la console et permettent le suivi d'actions correctives dans le temps.",
      "Le référentiel de causes est administrable par site : chaque atelier garde son vocabulaire métier."],
     "Cause codifiée rattachée à l'événement."),
    ("mob-stop-confirm", "Arrêt en cours et reprise", "/mobile/home (arrêt ouvert)", "Opérateur",
     ["Bandeau rouge « Arrêt en cours — Panne machine » avec chronomètre qui défile en temps réel.",
      "Bouton unique et large « Arrêt résolu — reprendre la production ».",
      "L'état du poste passe visuellement en « Arrêt technique » dans la carte poste."],
     ["Dès la déclaration, une alerte est poussée au service concerné et apparaît dans la file d'alertes de la console et sur l'Andon.",
      "Le chronomètre matérialise le temps d'immobilisation : il rend le coût de l'arrêt visible pour tous, opérateur comme superviseur.",
      "La clôture par l'opérateur fige la durée réelle d'arrêt, utilisée pour la disponibilité du TRS.",
      "Tant qu'un arrêt est ouvert, les autres saisies (production, fin de poste) invitent d'abord à le clôturer, pour éviter les temps fantômes."],
     "Durée d'arrêt, cause, opérateur, poste."),
    ("mob-production2", "Saisie de production", "/mobile/production", "Opérateur",
     ["Compteurs « Pièces bonnes » et « Pièces rebutées » avec boutons − / + surdimensionnés.",
      "Rappel de l'OF, de la référence et du nombre de pièces théoriques depuis la dernière saisie.",
      "Affichage du taux qualité déclaré et du total du poste après saisie (x / objectif).",
      "Bouton « Valider » unique en bas d'écran."],
     ["Le système propose une estimation théorique fondée sur la cadence, l'opérateur n'a qu'à corriger : la saisie devient un ajustement, pas une comptabilité.",
      "Les pièces bonnes alimentent la performance du TRS, les rebuts alimentent le taux de qualité.",
      "Chaque validation est horodatée et rattachée au couple poste / opérateur, ce qui permet de reconstituer la courbe de production du shift."],
     "Quantités produites et rebutées, taux qualité."),
    ("mob-production-filled", "Saisie bloquée par un arrêt ouvert", "/mobile/production", "Opérateur",
     ["Bandeau d'alerte : « Un arrêt est encore ouvert : clôturez-le avant de déclarer la production ».",
      "Bouton d'action directe « Clôturer l'arrêt et continuer » qui enchaîne les deux opérations.",
      "Les compteurs restent visibles mais la validation est désactivée."],
     ["Règle de cohérence : on ne peut pas produire et être à l'arrêt en même temps — le contrôle est fait à la saisie plutôt qu'a posteriori en correction.",
      "Ce garde-fou supprime la principale source d'incohérence des données TRS (arrêts jamais refermés).",
      "Les contrôles de saisie vérifient également que les valeurs sont numériques et que les rebuts restent cohérents avec la quantité produite."],
     "Données de production fiabilisées."),
    ("mob-changeover2", "Changement de série — checklist SMED", "/mobile/changeover", "Régleur, opérateur",
     ["Chronomètre de changement de série affiché en grand, avec les références concernées (REF sortante → REF entrante).",
      "Checklist en 5 étapes : fin de série en cours, démontage outillage, montage nouvel outillage, réglage, 1ère pièce bonne.",
      "Compteur d'étapes cochées avant de pouvoir clôturer.",
      "Bouton de validation de la 1ère pièce bonne, qui conclut l'opération."],
     ["Le changement de série est tracé comme un arrêt planifié : il est distingué des pannes dans le calcul et l'analyse du TRS.",
      "Le découpage en étapes horodatées permet de mesurer chaque phase et d'appliquer une démarche SMED (comparer, réduire, standardiser).",
      "La séquence guide les régleurs moins expérimentés et réduit la variabilité entre équipes."],
     "Temps de changement par étape et au total."),
    ("mob-changeover-step", "Changement de série — étape en cours", "/mobile/changeover (étape)", "Régleur",
     ["Étape active mise en évidence, étapes terminées cochées, étapes suivantes grisées.",
      "Durée écoulée par étape et progression globale (x / 5 étapes)."],
     ["La validation d'une étape est un appui unique : le régleur garde les mains libres pour son intervention.",
      "En fin de checklist, le poste bascule automatiquement en production sur le nouvel OF, sans saisie supplémentaire.",
      "Les alertes de l'atelier restent consultables pendant l'opération, sans perdre la progression en cours."],
     "Jalons horodatés du changement de série."),
    ("mob-neighbor3", "Arrêt sur un poste voisin", "/mobile/neighbor", "Opérateur, technicien, chef d'équipe",
     ["Mode « Poste voisin — arrêt uniquement » : scan ou saisie du code d'un poste dont on n'a pas la session.",
      "Restriction volontaire des actions au signalement d'arrêt."],
     ["Permet d'alerter pour un poste inoccupé, un collègue absent ou une machine en défaut repérée en passant.",
      "L'événement est rattaché au poste concerné mais attribué au déclarant : la traçabilité reste complète.",
      "La session personnelle de l'opérateur sur son propre poste n'est pas interrompue."],
     "Alerte rattachée à un poste tiers."),
    ("mob-inbox", "Boîte d'alertes (interventions)", "/mobile/inbox", "Technicien, régleur, logistique, qualité",
     ["File des événements ouverts avec compteur en en-tête.",
      "Filtres par famille : Tout, Technique, Qualité, Matière.",
      "Par carte : poste, cause, heure, prise en charge, SLA restant et action « PRENDRE ».",
      "Code couleur par famille et mise en évidence des SLA dépassés."],
     ["C'est l'écran de travail des intervenants mobiles : ils voient ce qui les concerne sans passer par le superviseur.",
      "Le bouton « PRENDRE » attribue l'intervention à celui qui appuie et informe immédiatement la console : plus de double intervention sur le même problème.",
      "Le tri met en avant l'urgence réelle (SLA restant) plutôt que le seul ordre chronologique."],
     "Prise en charge, délai de réaction par intervenant."),
    ("mob-inbox2", "Boîte d'alertes — filtres par famille", "/mobile/inbox", "Intervenants",
     ["Filtrage instantané de la file par famille d'événement.",
      "Étiquettes « PRIS EN CHARGE » / « PRENDRE » directement lisibles sur chaque ligne."],
     ["Chaque métier se concentre sur son périmètre : la maintenance sur le technique, la logistique sur la matière, la qualité sur les défauts.",
      "Le SLA affiché en rouge (dépassement) déclenche l'escalade côté console.",
      "La file se rafraîchit en continu pendant que l'intervenant se déplace dans l'atelier."],
     "Charge par famille et par intervenant."),
    ("mob-inbox-take", "Prise en charge d'une intervention", "/mobile/inbox (détail)", "Technicien",
     ["Fiche événement dépliée : référence (EVT-…), cause, poste et frise du circuit de traitement.",
      "Actions de terrain : « Je suis arrivé », « Occupé », et estimation d'arrivée en 5 / 10 / 15 min.",
      "Retour visuel immédiat de l'étape atteinte."],
     ["Le technicien fait avancer le statut depuis le terrain : déclaré → notifié → en route → sur place → résolu.",
      "L'estimation d'arrivée est renvoyée à l'opérateur et au superviseur : l'attente devient prévisible, ce qui réduit les relances.",
      "Le statut « Occupé » permet de refuser proprement une prise en charge, pour que l'alerte soit re-routée vers un autre intervenant."],
     "Horodatage de chaque étape du circuit."),
    ("mob-kpi", "Indicateurs du poste (suivi du shift)", "/mobile/kpi", "Opérateur, chef d'équipe",
     ["TRS du poste affiché en grand, avec ses trois composantes : disponibilité, performance, qualité.",
      "Barres de progression par composante.",
      "Top des causes d'arrêt du shift, avec le temps cumulé par cause.",
      "Horodatage de la dernière mise à jour."],
     ["Restituer la performance à celui qui la produit : l'opérateur voit l'effet de ses déclarations sans attendre la réunion du lendemain.",
      "Le TRS est calculé à partir des seules déclarations terrain — aucune double saisie, aucun retraitement manuel.",
      "Le Top causes oriente l'action immédiate : c'est souvent 2 ou 3 causes qui expliquent l'essentiel des pertes du shift."],
     "TRS poste, disponibilité, performance, qualité."),
    ("mob-kpi2", "Indicateurs — tendance du shift", "/mobile/kpi", "Opérateur, chef d'équipe",
     ["Comparaison avec l'objectif du poste et évolution depuis le début du shift.",
      "Détail chiffré du temps perdu par cause (minutes)."],
     ["Les minutes perdues sont plus parlantes que les pourcentages pour un échange en atelier : elles se traduisent directement en pièces non produites.",
      "Ces chiffres servent de support au point de 5 minutes en début de shift suivant."],
     "Écart à l'objectif, temps perdu par cause."),
    ("mob-history", "Historique du poste", "/mobile/history", "Opérateur, chef d'équipe",
     ["Journal chronologique de toutes les saisies de la session.",
      "Filtres : Tout, Production, Arrêts.",
      "État vide explicite lorsqu'aucune saisie n'a encore été faite."],
     ["Permet à l'opérateur de vérifier et de justifier ses déclarations avant la clôture du poste.",
      "Support de passation entre équipes : l'équipe entrante voit ce qui s'est passé avant elle.",
      "Toute donnée reste consultable côté terrain, ce qui installe la confiance dans l'outil (rien n'est caché)."],
     "Journal d'événements du shift."),
    ("mob-history2", "Historique — filtres et détail", "/mobile/history", "Opérateur",
     ["Chaque ligne affiche l'heure, le type d'événement, la cause codifiée et l'auteur.",
      "Filtrage instantané par nature d'événement."],
     ["La séparation production / arrêts permet de reconstituer rapidement la chronologie d'un incident.",
      "Les corrections éventuelles restent visibles avec leur valeur d'origine (voir Administration — Corrections)."],
     "Piste d'audit terrain."),
    ("mob-map", "Plan d'atelier mobile", "/mobile/map", "Opérateur, technicien, chef d'équipe",
     ["Vue temps réel des postes regroupés par ligne (Assemblage, Injection, Contrôle).",
      "Code couleur d'état : production, arrêt technique, arrêt qualité, attente matière, changement série, non affecté.",
      "Compteur de postes et pastilles de santé par ligne."],
     ["Donne à l'opérateur la vision d'ensemble habituellement réservée au bureau : il comprend l'impact de son poste sur le flux.",
      "Sert de radar aux intervenants mobiles pour choisir leur prochaine action selon la criticité.",
      "L'état affiché découle directement des déclarations : le plan est toujours cohérent avec les indicateurs."],
     "État temps réel de tous les postes."),
    ("mob-map2", "Plan d'atelier — postes en alerte", "/mobile/map", "Technicien",
     ["Les postes en alerte portent une pastille d'alerte et l'action « PRENDRE ».",
      "Les postes non affectés apparaissent hachurés."],
     ["Prise en charge possible en un appui depuis le plan, sans passer par la file d'alertes.",
      "Les postes non affectés signalent un problème de couverture d'équipe à traiter dans les affectations."],
     "Taux de couverture, alertes par ligne."),
    ("mob-map-detail", "Plan d'atelier — fiche poste", "/mobile/map (détail)", "Technicien, chef d'équipe",
     ["Fiche contextuelle : code poste, ligne, état, opérateur affecté, ordre de fabrication.",
      "Boutons « Interventions » (voir les alertes du poste) et « Fermer »."],
     ["Accès en deux appuis à tout le contexte d'un poste, y compris pour un poste dont on n'a pas la session.",
      "Les actions proposées dépendent du rôle de l'utilisateur connecté."],
     "Contexte poste consolidé."),
    ("mob-shift-end2", "Fin de poste et passation", "/mobile/shift-end", "Opérateur",
     ["Récapitulatif complet : pièces bonnes, pièces rebutées, taux qualité, nombre d'arrêts déclarés, temps d'arrêt, durée de session, avancement de l'OF, saisies restant à synchroniser.",
      "Bouton « Rapport de poste (PDF) ».",
      "Choix du type de clôture (fin de poste normale, avec libération du poste).",
      "Options « Finir le poste et se déconnecter » ou « Continuer à travailler »."],
     ["Le récapitulatif est le moment de vérité : l'opérateur valide ses données avant qu'elles n'alimentent les indicateurs consolidés.",
      "Le compteur « saisies à synchroniser » garantit qu'aucune donnée hors-ligne n'est perdue au moment de la déconnexion.",
      "Un arrêt encore ouvert empêche la clôture : message d'alerte explicite avant de finir.",
      "La clôture libère le poste pour l'équipe suivante et clôt la session ; « Continuer à travailler » couvre les heures supplémentaires."],
     "Rapport de poste, clôture de session."),
]

WEB = [
    ("web-login", "Connexion console", "/web/login", "Superviseur, responsable production, maintenance, qualité, admin",
     ["Authentification par e-mail professionnel et mot de passe.",
      "Sélecteur de langue avant connexion.",
      "Lien « Changer d'espace » vers l'application mobile."],
     ["Le rôle porté par le compte détermine les écrans visibles et les actions autorisées dans toute la console (lecture, action, administration).",
      "Les rôles sont gérés séparément du profil utilisateur et vérifiés côté serveur : un utilisateur ne peut pas s'attribuer de droits depuis le navigateur.",
      "Les comptes sont créés et désactivés depuis l'administration, sans auto-inscription."],
     "Session console avec rôle et périmètre."),
    ("web-dashboard", "Dashboard TRS", "/web/dashboard", "Responsable production, superviseur",
     ["Bandeau d'indicateurs : TRS global, disponibilité, performance, qualité et temps moyen de réaction.",
      "Histogramme de tendance du TRS sur la période sélectionnée.",
      "Top des causes d'arrêt avec temps cumulé.",
      "Tableau comparatif par ligne (TRS, arrêts, minutes perdues)."],
     ["Point d'entrée quotidien du pilotage : répondre en 10 secondes à « où perd-on du temps aujourd'hui ? ».",
      "TRS = disponibilité × performance × qualité, calculé à partir des seules déclarations terrain, sans ressaisie ni retraitement Excel.",
      "Le croisement tendance / causes / lignes permet de distinguer un incident ponctuel d'un problème structurel.",
      "Les écarts à l'objectif sont mis en évidence pour prioriser les plans d'action."],
     "Indicateurs consolidés site / ligne / période."),
    ("web-demo-tour", "Démo guidée sonore", "/web (visite guidée)", "Commercial, formateur, nouvel utilisateur",
     ["Visite narrée qui parcourt les fonctionnalités de la suite, écran par écran.",
      "Surbrillance de l'élément d'interface commenté, avec contrôles lecture / pause / étape suivante.",
      "Disponible aussi bien côté console que côté mobile."],
     ["Sert à la démonstration client et à la formation des nouveaux utilisateurs sans mobiliser un formateur.",
      "Peut être quittée à tout moment ; l'utilisateur reprend la main sur l'application réelle, pas sur une maquette."],
     "Aucune donnée — parcours pédagogique."),
    ("web-shopfloor", "Plan atelier (console)", "/web/shopfloor", "Superviseur, chef d'équipe",
     ["Représentation temps réel de l'atelier par ligne, chaque poste affichant son état et sa cause d'arrêt.",
      "Légende des états en pied de page (production, attente matière, changement série, arrêt technique, arrêt qualité, non affecté).",
      "Postes non affectés distingués visuellement."],
     ["Lecture immédiate des zones en difficulté, à la manière d'un management visuel physique mais mis à jour en continu.",
      "Point d'entrée naturel vers la fiche poste pour agir sans changer d'écran.",
      "Utilisé pendant les tours de terrain et les points de production."],
     "Cartographie temps réel des états."),
    ("web-shopfloor-detail", "Plan atelier — fiche poste", "/web/shopfloor (détail)", "Superviseur",
     ["Panneau latéral : ligne, état, opérateur, ordre de fabrication, avancement et alertes associées.",
      "Actions contextuelles depuis le panneau (consulter, agir sur l'alerte)."],
     ["Le superviseur obtient tout le contexte d'un poste sans quitter le plan et sans appeler l'atelier.",
      "Les informations affichées sont strictement les mêmes que celles vues par l'opérateur : une seule version de la vérité."],
     "Contexte poste consolidé."),
    ("web-alerts", "File d'alertes", "/web/alerts", "Superviseur, maintenance, qualité, logistique",
     ["Tableau des événements : référence, poste, type, cause, avancement du circuit, prise en charge, SLA restant.",
      "Recherche plein texte (cause, poste, référence) et filtres par type et par étape du circuit.",
      "Frise de progression du circuit directement dans la ligne.",
      "En-tête rappelant les SLA appliqués (ex. technique 10 min, qualité 5 min)."],
     ["Centre de traitement de tous les événements de l'atelier : rien ne se perd, tout est horodaté et attribué.",
      "Les SLA sont différenciés par famille et le dépassement est signalé en rouge pour déclencher l'escalade.",
      "La vue permet de suivre le délai de réaction (temps entre déclaration et prise en charge) autant que le délai de résolution."],
     "Événements, SLA, délais de réaction et de résolution."),
    ("web-alerts-detail", "Détail d'alerte et circuit de traitement", "/web/alerts (panneau)", "Superviseur, intervenant",
     ["Panneau détaillé : poste, ligne, type, prise en charge, niveau d'escalade et durée écoulée.",
      "Circuit horodaté complet : déclaré → notifié → en route → sur place → résolu → clôturé.",
      "Actions : « Pris en charge », « Étape suivante », « Clôturer l'événement », « Vu », « Escalader ».",
      "Menu de requalification pour re-router l'événement vers un autre service."],
     ["Le circuit standardise le traitement d'un incident et rend mesurable chaque maillon de la chaîne de réaction.",
      "L'escalade hiérarchique se déclenche sur dépassement de SLA : le niveau atteint est affiché en haut du panneau.",
      "La requalification corrige une erreur de catégorie sans supprimer l'événement : l'historique initial reste tracé.",
      "La clôture n'est possible qu'une fois la résolution constatée, ce qui évite les fermetures administratives de complaisance."],
     "Cycle de vie complet de l'événement."),
    ("web-assignments", "Affectations", "/web/assignments", "Chef d'équipe, responsable production",
     ["Vue d'affectation des opérateurs aux postes pour le shift, avec taux de couverture (ex. 6/8).",
      "Compteur de conflits et de postes non couverts.",
      "Mode brouillon puis publication des affectations.",
      "Attribution poste par poste avec liste des opérateurs disponibles."],
     ["Prépare le shift avant son démarrage : chaque opérateur retrouve son poste dès sa connexion mobile.",
      "Les conflits (opérateur affecté deux fois, poste sans opérateur) sont détectés avant publication.",
      "Le brouillon permet de préparer sans impacter l'atelier ; la publication rend l'affectation effective.",
      "Le suivi de couverture met en évidence les besoins de polyvalence et de renfort."],
     "Plan d'affectation du shift, couverture, conflits."),
    ("web-reports", "Rapports", "/web/reports", "Responsable production, amélioration continue",
     ["Indicateurs de synthèse sur la période : temps moyen de réaction, temps moyen de résolution, taux de rebut, minutes perdues.",
      "Histogramme d'évolution du TRS.",
      "Pareto des causes d'arrêt en minutes.",
      "Tableaux de performance par ligne et par service (délai, respect du SLA)."],
     ["Passage du pilotage temps réel à l'analyse : comparer des périodes, des lignes, des équipes et des services.",
      "Le Pareto identifie les 20 % de causes responsables de 80 % des pertes, base des chantiers d'amélioration continue.",
      "Les indicateurs de service (réaction, résolution, respect SLA) objectivent les échanges entre production et fonctions support.",
      "Export destiné aux revues de performance hebdomadaires et mensuelles."],
     "Séries historiques, Pareto, performance par service."),
    ("web-shift-report", "Rapport de poste", "/web/shift-report", "Chef d'équipe, responsable production",
     ["Synthèse du shift : TRS, pièces produites, rebuts, temps d'arrêt total.",
      "Détail des événements du shift avec cause, durée et prise en charge.",
      "Récapitulatif par ligne (TRS, arrêts, minutes perdues).",
      "Zone de note de passation et export PDF."],
     ["Document de passation officiel entre équipes : il remplace le cahier de liaison papier.",
      "Consolidé automatiquement à partir des données déjà saisies : aucune ressaisie de fin de poste.",
      "Archivé pour servir de preuve en cas d'analyse a posteriori ou d'audit client."],
     "Rapport de shift archivé (PDF)."),
    ("web-andon", "Andon TV", "/web/andon", "Atelier (affichage collectif)",
     ["Affichage plein écran pour écran mural : alerte prioritaire en très grand format, heure courante, indicateurs clés.",
      "Bandeau des postes par ligne avec leur état et leur durée.",
      "Liste des événements ouverts avec ancienneté.",
      "Message d'équipe diffusable sur l'écran."],
     ["Mode « information radiateur » : aucune interaction, rafraîchissement automatique, lisible à plusieurs mètres.",
      "L'alerte la plus critique occupe la zone principale pour déclencher la réaction collective.",
      "Aligne tout l'atelier sur la même information au même moment, y compris les fonctions support de passage."],
     "Diffusion temps réel des alertes et indicateurs."),
    ("web-referentials", "Référentiels", "/web/referentials", "Méthodes, administrateur",
     ["Consultation et paramétrage des données de référence : lignes, postes, ordres de fabrication, références produit, causes d'arrêt, services.",
      "Édition en ligne des libellés et des rattachements.",
      "Visualisation des dépendances entre entités."],
     ["Le référentiel est le socle de cohérence : postes, causes et services y sont définis une seule fois puis réutilisés partout.",
      "Modifier une cause ici la met à jour instantanément sur toutes les tablettes de l'atelier.",
      "Le rattachement cause → service détermine le routage automatique des alertes."],
     "Données de référence du site."),
    ("web-admin-qr", "Administration — Étiquettes QR", "/web/admin (QR)", "Administrateur",
     ["Génération des QR de postes et des badges opérateurs, planche par planche.",
      "Aperçu avant impression et export imprimable.",
      "Regroupement par ligne pour préparer le déploiement atelier."],
     ["Ces étiquettes conditionnent tout le flux mobile : sans QR poste, pas de session ancrée ; sans badge, pas de connexion rapide.",
      "La génération centralisée garantit l'unicité et la validité des codes vis-à-vis du référentiel.",
      "Réimpression possible à tout moment en cas d'étiquette détériorée en atelier."],
     "Étiquettes QR postes et badges."),
    ("web-admin-users", "Administration — Utilisateurs et rôles", "/web/admin (Utilisateurs)", "Administrateur",
     ["Création de comptes (nom, matricule, e-mail, rôle, équipe).",
      "Tableau des utilisateurs avec statut et actions « Régénérer » (code PIN) et « Désactiver ».",
      "Attribution du rôle déterminant les accès console et mobile."],
     ["Les rôles sont stockés dans une table dédiée, séparée du profil : cette séparation empêche toute élévation de privilèges depuis le client.",
      "La régénération de PIN couvre l'oubli ou la compromission d'un code, sans recréer le compte.",
      "La désactivation conserve l'historique des événements de la personne : la donnée passée reste exploitable et auditable."],
     "Comptes, rôles, codes PIN, statut."),
    ("web-admin-shifts", "Administration — Équipes et shifts", "/web/admin (Équipes)", "Administrateur, RH production",
     ["Paramétrage des modèles d'équipe (Matin, Après-midi, Nuit) avec leurs plages horaires.",
      "Association des horaires aux jours de la semaine."],
     ["Le paramétrage des plages conditionne le rattachement automatique de chaque événement au bon shift.",
      "Il fixe également le temps d'ouverture théorique utilisé au dénominateur du TRS.",
      "Un mauvais paramétrage fausse tous les indicateurs : c'est l'écran à valider en premier lors d'un déploiement."],
     "Calendrier de shifts, temps d'ouverture."),
    ("web-admin-plugins", "Administration — Plugins et modules", "/web/admin (Plugins)", "Administrateur",
     ["Catalogue des modules fonctionnels classés par domaine (applications, production, planification, qualité, analyse).",
      "État d'activation par module et version.",
      "Actions d'activation / désactivation et bouton d'initialisation du catalogue."],
     ["Permet d'adapter le périmètre fonctionnel à chaque site sans redéploiement ni développement spécifique.",
      "Un déploiement peut ainsi démarrer avec le socle (scan, arrêts, TRS) puis activer progressivement Andon, SMED ou qualité.",
      "Les modules désactivés disparaissent des interfaces mobile et console : l'utilisateur ne voit que ce qui le concerne."],
     "Configuration fonctionnelle du site."),
    ("web-admin-import", "Administration — Import Excel", "/web/admin (Import)", "Administrateur, méthodes",
     ["Zone de dépôt d'un classeur Excel (glisser-déposer ou sélection de fichier).",
      "Import en masse des postes, opérateurs, ordres de fabrication et causes.",
      "Rappel du format attendu pour chaque onglet du classeur."],
     ["Accélère le démarrage d'un site : les référentiels existants sont repris tels quels au lieu d'être ressaisis.",
      "Les lignes sont validées avant application, avec rapport des rejets et de leur motif : l'import est tout ou rien sur les lignes invalides.",
      "Sert aussi aux mises à jour périodiques (nouveaux OF, nouvelles références)."],
     "Chargement de référentiels en masse."),
    ("web-admin-corrections", "Administration — Corrections", "/web/admin (Corrections)", "Superviseur habilité, administrateur",
     ["Liste des corrections de déclarations, avec heure, poste, quantité, type et actions.",
      "Correction encadrée d'une durée d'arrêt, d'une quantité ou d'une cause erronée."],
     ["Reconnaît la réalité du terrain : une erreur de saisie doit pouvoir être corrigée, mais jamais en silence.",
      "Chaque correction conserve la valeur d'origine, l'auteur et le motif : la donnée reste auditable et les indicateurs restent défendables.",
      "L'accès est réservé aux rôles habilités, ce qui protège l'intégrité des indicateurs."],
     "Historique des corrections avec valeurs d'origine."),
    ("web-admin-audit", "Administration — Journal d'audit", "/web/admin (Audit)", "Administrateur, qualité",
     ["Journal horodaté des actions sensibles : date, utilisateur, action, objet, détail.",
      "Filtres par période et par type d'action, recherche libre.",
      "Export du journal (Excel)."],
     ["Traçabilité complète : qui a modifié quoi, quand et depuis quel compte.",
      "Répond aux exigences des systèmes qualité (IATF, ISO) et des audits client sur l'intégrité des données de production.",
      "Combiné aux corrections, il garantit qu'aucune donnée ne peut être modifiée sans laisser de trace."],
     "Piste d'audit exportable."),
]


def img_flow(path, max_w, max_h):
    with Image.open(path) as im:
        w, h = im.size
    ratio = min(max_w / w, max_h / h)
    return RLImage(path, width=w * ratio, height=h * ratio)


def bullets(items):
    return ListFlowable([ListItem(Paragraph(b, LI), leftIndent=9) for b in items],
                        bulletType="bullet", bulletFontName="DJ", bulletFontSize=5.5,
                        leftIndent=9, spaceAfter=2)


def page_decor(canvas, doc):
    canvas.saveState()
    canvas.setFont("DJ", 7.5)
    canvas.setFillColor(colors.HexColor("#9CA3AF"))
    canvas.drawString(18 * mm, 12 * mm, "Suite OAS — Documentation fonctionnelle complète")
    canvas.drawRightString(A4[0] - 18 * mm, 12 * mm, str(canvas.getPageNumber()))
    canvas.restoreState()


def section(title, intro, items):
    rows = [[Paragraph(f"<font name='DJB'>{i + 1}.</font> {t}", LI), Paragraph(r, LI)]
            for i, (_, t, r, *_rest) in enumerate(items)]
    tbl = Table(rows, colWidths=[105 * mm, 69 * mm])
    tbl.setStyle(TableStyle([
        ("FONTNAME", (0, 0), (-1, -1), "DJ"),
        ("TEXTCOLOR", (1, 0), (1, -1), GREY),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 3),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
        ("LINEBELOW", (0, 0), (-1, -2), 0.25, colors.HexColor("#E5E7EB")),
    ]))
    return [Spacer(1, 45 * mm), Paragraph(title, SECT), Paragraph(intro, BODY),
            Spacer(1, 7 * mm), tbl, PageBreak()]


def pages(items, tag):
    flow, missing = [], []
    for fn, title, route, roles, feats, logic, data in items:
        path = os.path.join(SHOTS, fn + ".png")
        if not os.path.exists(path):
            missing.append(fn)
            continue
        flow.append(Paragraph(title, H2))
        flow.append(Paragraph(f"{tag} · {route} · Utilisateurs : {roles}", SUB))
        flow.append(Paragraph("Fonctionnalités de l'écran", H3))
        flow.append(bullets(feats))
        flow.append(Paragraph("Logique métier et règles de gestion", H3))
        flow.append(bullets(logic))
        flow.append(Paragraph(f"<b>Données produites :</b> {data}", NOTE))
        flow.append(Spacer(1, 3.5 * mm))
        flow.append(img_flow(path, 174 * mm, 118 * mm))
        flow.append(PageBreak())
    if missing:
        print("captures manquantes:", missing)
    return flow


doc = SimpleDocTemplate(OUT, pagesize=A4, topMargin=15 * mm, bottomMargin=18 * mm,
                        leftMargin=18 * mm, rightMargin=18 * mm,
                        title="Suite OAS — Documentation fonctionnelle complète",
                        author="OAS")

INTRO = [
    "La suite OAS couvre la conduite d'atelier de bout en bout : déclaration terrain sur mobile, traitement des "
    "alertes par les services support, pilotage et analyse en console, et administration du site.",
    "Principe directeur : <b>une seule saisie, à la source</b>. Tout ce qui est déclaré par l'opérateur (prise de poste, "
    "arrêts, quantités, changements de série) alimente automatiquement les alertes, le TRS, les rapports et l'audit — "
    "sans ressaisie ni fichier Excel intermédiaire.",
    "Trois familles d'utilisateurs : les <b>opérateurs</b> (application mobile, gros boutons, 2 appuis par action), les "
    "<b>intervenants</b> maintenance / qualité / logistique (file d'alertes mobile et console), et les "
    "<b>responsables</b> (dashboard, rapports, affectations, administration).",
    "Le présent document décrit chaque écran de la suite : fonctionnalités disponibles, logique métier et règles de "
    "gestion appliquées, données produites, illustrées par des captures réelles de l'application en français.",
]

story = [Spacer(1, 40 * mm),
         Paragraph("Suite OAS", SECT),
         Paragraph("Documentation fonctionnelle complète", H1),
         Spacer(1, 4 * mm)]
for para in INTRO:
    story += [Paragraph(para, BODY), Spacer(1, 3 * mm)]
story += [Spacer(1, 4 * mm),
          Paragraph(f"{len(MOBILE)} écrans mobiles · {len(WEB)} écrans console · "
                    "captures réelles de l'application · version de démonstration.", SUB),
          PageBreak()]

story += section("1 · Application mobile",
                 "Parcours opérateur en atelier : connexion par badge ou PIN, ancrage sur un poste par scan QR, "
                 "déclaration d'arrêts codifiés, saisie de production, changement de série guidé (SMED), "
                 "traitement des interventions, indicateurs du poste et clôture de shift. "
                 "L'application fonctionne hors ligne et rejoue les saisies au retour du réseau ; "
                 "elle est distribuée sous forme d'APK Android (Ionic / Capacitor).", MOBILE)
story += pages(MOBILE, "Mobile")
story += section("2 · Console web",
                 "Pilotage et administration : dashboard TRS, plan atelier temps réel, file d'alertes avec circuit "
                 "de traitement et SLA, affectations d'équipe, rapports d'analyse, rapport de poste, affichage Andon, "
                 "référentiels et administration complète (QR, utilisateurs et rôles, shifts, modules, import, "
                 "corrections, journal d'audit).", WEB)
story += pages(WEB, "Console web")

doc.build(story, onFirstPage=page_decor, onLaterPages=page_decor)
print("écrit", OUT)
