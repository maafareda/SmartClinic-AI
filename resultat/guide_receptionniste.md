# Guide simple — Espace réceptionniste

## À quoi sert l’application ?

L’espace réceptionniste sert à suivre les rendez-vous et la file d’attente, à inscrire un patient et à traiter les demandes qui arrivent à l’accueil.

## Ouvrir l’espace

Ouvrez `http://localhost:5106` si l’application est lancée sur votre ordinateur. La page d’accueil affiche le tableau de bord du cabinet. Le lien **Espace patient** ouvre la réservation en ligne.

## Inscrire un patient et réserver

1. Cliquez sur **Nouveau rendez-vous**.
2. Saisissez le prénom, le nom et le téléphone du patient.
3. Choisissez un médecin, une date et un créneau disponible.
4. Confirmez le rendez-vous.
5. Le code de check-in s’affiche après la confirmation : communiquez-le au patient.

L’avance indiquée dans cette version est simulée : aucun argent n’est encaissé.

## Suivre les rendez-vous et les arrivées

- **Vue d’ensemble** : résumé de l’activité du jour et demandes à traiter.
- **Rendez-vous** : liste des rendez-vous, avec leur statut et les montants simulés.
- **File d’attente** : patients présents, classés selon l’heure de leur rendez-vous, et non selon l’ordre d’arrivée. La page se met à jour périodiquement.
- Une action apparaît selon le statut : démarrer ou terminer une consultation, ou enregistrer le solde simulé.
- Pour un check-in manuel (par exemple si le patient ne peut pas utiliser son code), utilisez **Check-in / Arrivée manuelle** et indiquez le motif demandé.

## Traiter les demandes et utiliser l’assistant

Dans **Demandes patients**, consultez les messages transmis à l’accueil et modifiez leur statut. Pour marquer une demande comme résolue, une note de résolution est obligatoire.

Dans **Assistant IA**, vous pouvez tester l’assistant de démonstration. Il répond à quelques questions générales et transmet certaines demandes, comme une annulation, un report ou une urgence, à l’accueil. Les urgences sont signalées comme prioritaires. L’assistant n’effectue pas de diagnostic médical.

## Limites importantes

- C’est un prototype local, pas un service prêt pour une utilisation réelle.
- Il n’y a pas de connexion par compte ni de droits d’accès sécurisés.
- Les données et rendez-vous de démonstration sont conservés en mémoire et réinitialisés au redémarrage du service.
- Les paiements et montants affichés sont simulés.
- N’entrez aucune donnée réelle de patient et n’exposez pas l’application sur Internet.
