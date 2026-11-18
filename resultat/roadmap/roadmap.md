# Roadmap SmartClinic AI

Référence : cahier des charges SmartClinic AI v2. Statut mis à jour le 18 novembre 2026.

## Phase 0 — Base de démonstration locale (livrée)

- [x] Créer une solution .NET 10 en couches : Domain, Application, Infrastructure, API et Tests.
- [x] Fournir des données synthétiques pour deux cabinets et isoler les lectures/écritures par identifiant de cabinet.
- [x] Construire une interface d'accueil responsive : tableau de bord, rendez-vous, file, demandes, équipe et paramètres.
- [x] Réserver un créneau, simuler l'avance, présenter le code de check-in une seule fois.
- [x] Appliquer les transitions check-in → file → consultation → solde, et la règle NoShow à la tolérance près.
- [x] Ajouter un assistant déterministe avec transfert humain et traitement prioritaire des demandes urgentes.
- [x] Tester les règles de rendez-vous, l'isolation logique des cabinets et la concurrence de réservation.
- [x] Enregistrer les résultats automatisés sous `resultat/test/`.

**Critères de sortie :** compilation .NET 10 réussie, suite ciblée verte, service de santé répondant, interface servie localement.

## Phase 1 — Socle sécurisé multi-cabinets

- [ ] Ajouter PostgreSQL 16, migrations EF Core, clés étrangères tenant-composite et contrainte d'exclusion des rendez-vous.
- [ ] Ajouter l'authentification ASP.NET Identity/JWT, renouvellement/révocation des sessions et rôles cabinet.
- [ ] Vérifier les autorisations à chaque route ; résoudre le tenant courant depuis une identité authentifiée et non depuis une valeur client seule.
- [ ] Appliquer les politiques RLS `ClinicId`, réduire les privilèges SQL et tester les lectures/écritures croisées entre deux cabinets.
- [ ] Ajouter journal d'audit, consentements versionnés, politique de conservation et suppression des données.
- [ ] Chiffrer les notes médicales et la sauvegarde ; exclure toute donnée clinique des journaux et traces.
- [ ] Ajouter tests d'intégration PostgreSQL (isolation, réservation concurrente, migrations) et tests de permissions.

**Critères de sortie :** aucune route métier anonyme, isolation démontrée par les tests RLS et contrainte de base anti-chevauchement.

## Phase 2 — Parcours patient et paiements

- [ ] Ajouter portail patient authentifié, OTP, consentements et récupération des rendez-vous.
- [ ] Implémenter `SlotHolds` atomiques, expiration après 10 minutes, idempotence et compensation de paiement.
- [ ] Ajouter un PSP agréé en environnement de test : validation serveur des webhooks, signatures, déduplication et clés d'idempotence.
- [ ] Générer reçus, solde, remboursements et historique transactionnel en ajout seul.
- [ ] Ajouter QR signé à durée courte, renouvellement, codes de check-in hachés/limités en débit et révocation.
- [ ] Tester créneaux aux limites d'horaire, paies échouées, reprises, annulations, remboursements et doubles webhooks.

**Critères de sortie :** aucune confirmation fondée sur un retour navigateur, aucun double débit ni double réservation.

## Phase 3 — Réception, médecin et temps réel

- [ ] Ajouter pages de gestion d'équipe, horaires, absences, exceptions, réouverture motivée d'un NoShow et historique auditable.
- [ ] Ajouter SignalR avec groupes autorisés par cabinet/médecin, reconnexion et absence de diffusion inter-tenant.
- [ ] Ajouter tâches planifiées distribuées/verrouillées pour expiration, NoShow et rappels.
- [ ] Ajouter reçus téléchargeables, tableaux de bord financiers, filtres et exports à accès limité.
- [ ] Compléter les parcours responsive, accessibilité clavier/lecteur d'écran et traduction intégrale FR/EN/AR/darija RTL.
- [ ] Tester reconnexion temps réel, tâches idempotentes, politiques d'annulation et scénarios d'absence médecin/cabinet.

**Critères de sortie :** file ordonnée par heure du rendez-vous, notifications tenant-sécurisées et actions exceptionnelles traçables.

## Phase 4 — Assistant IA et canaux

- [ ] Remplacer l'assistant de démonstration par une interface LLM isolée, tool calling côté serveur et RAG sourcé par cabinet.
- [ ] Ajouter minimisation et masquage des données, mémoire avec consentement/rétention, déduplication et journal d'outils sans données sensibles.
- [ ] Ajouter confiance calibrée, clarification, transfert humain, protection contre l'injection et politique de non-diagnostic.
- [ ] Ajouter webhooks WhatsApp/SMS vérifiés, OTP avant toute divulgation et adaptation de réponse par canal.
- [ ] Constituer un jeu de tests revu par des personnes parlant français, arabe, darija et arabizi ; mesurer exactitude, refus et transfert.
- [ ] Tester demandes urgentes, injections, indisponibilité du LLM, disponibilité non confirmée et continuité inter-canaux.

**Critères de sortie :** aucune disponibilité inventée, aucune donnée médicale envoyée au LLM, transferts urgents fiables et évaluation documentée.

## Phase 5 — Mise en production

- [ ] Ajouter Docker Compose (API, PostgreSQL, Redis), secrets externes, environnements et sauvegardes/restaurations chiffrées.
- [ ] Ajouter pipeline CI : format, build, tests unitaires/intégration, analyse de dépendances, scans de conteneur et génération des artefacts.
- [ ] Ajouter OpenAPI, supervision, alertes, limites de débit, stratégie de reprise et gestion des incidents.
- [ ] Faire revue de sécurité indépendante, tests de charge, audit de conformité et validation CNDP / loi marocaine 09-08.
- [ ] Valider juridiquement les avances, NoShow, annulations, remboursements et les mentions patient avant toute mise en service.

**Critères de sortie :** validation sécurité/conformité, restauration testée, seuils opérationnels définis et décision formelle de lancement.

## Décisions et limites actuelles

- Interface livrée en ASP.NET Core avec HTML/CSS/JavaScript sans dépendance de build Node : le SDK .NET est disponible dans l'environnement et Node n'y est pas installé.
- Le moteur web intègre une interface FR avec prise en charge directionnelle RTL arabe, sans traduction intégrale des libellés.
- Les données du MVP sont en mémoire et synthétiques ; le verrou protège uniquement un processus, pas plusieurs instances.
- Le paiement est une simulation exclusivement serveur et ne déclenche aucun paiement réel.
- L'assistant utilise des réponses déterministes et n'appelle aucun LLM.
- Le runtime Docker local était indisponible ; aucun conteneur ni service externe n'a été lancé.
