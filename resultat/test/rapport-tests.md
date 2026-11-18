# Rapport des tests

Projet : SmartClinic AI · Environnement : Windows, SDK .NET 10 · Date : 18 novembre 2026

## Campagne de validation initiale

Campagne exécutée le 18 novembre 2026. La compilation et les tests automatisés ont été rejoués après la correction du fuseau horaire de Casablanca.

| Vérification | Résultat |
|---|---|
| `dotnet build SmartClinicAI.sln` | RÉUSSI — 0 avertissement, 0 erreur |
| `dotnet test SmartClinicAI.sln --no-build` | RÉUSSI — 8 tests, 0 échec, 0 test ignoré |
| Réservation concurrente d'un créneau | RÉUSSI — une seule réservation confirmée |
| Check-in et fenêtre horaire | RÉUSSI — bornes et code invalide testés |
| NoShow et avance conservée | RÉUSSI — seuil, contre-écriture et idempotence testés |
| Solde exact et transitions de consultation | RÉUSSI — montant incorrect refusé |
| Isolation logique entre cabinets | RÉUSSI — accès inter-cabinet refusé (HTTP 404) |
| Fuseau du cabinet de Casablanca | RÉUSSI — créneau de 09:00 exposé à 09:00 locale |
| API d'urgence | RÉUSSI — demande créée avec priorité `Urgent` |
| Interface web | RÉUSSI — page servie, réservation soumise et code affiché |
| Endpoint `/health` | RÉUSSI — réponse `ok` |
| Contrôle du diff (`git diff --check`) | RÉUSSI |

Journaux complets : `compilation.log`, `execution-finale.log` et `smoke-http.log` dans ce dossier. Le premier lancement de compilation finale a été bloqué parce que le serveur local détenait ses DLL ; après l'arrêt de ce processus, la compilation complète a réussi. Le premier contrôle du fuseau a révélé un décalage d'une heure ; la conversion s'appuie maintenant sur la base de fuseaux Windows du Maroc et le test dédié est vert.
