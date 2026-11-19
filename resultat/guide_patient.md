# Guide simple — Espace patient

## À quoi sert l’application ?

SmartClinic AI permet de choisir un cabinet, un médecin et un horaire disponible pour demander un rendez-vous en ligne.

## Prendre un rendez-vous

1. Ouvrez la page patient : `http://localhost:5106/patient` si l’application est lancée sur votre ordinateur.
2. Choisissez un cabinet, un médecin et une date. Les créneaux proposés sont ceux qui sont disponibles, dans les 30 prochains jours.
3. Sélectionnez un horaire.
4. Saisissez votre prénom, votre nom et votre téléphone.
5. Cliquez sur **Confirmer mon rendez-vous**.
6. Après confirmation, notez le code de check-in affiché et gardez-le pour votre arrivée au cabinet. Communiquez-le uniquement à l’accueil.

Vous pouvez aussi appeler le numéro du cabinet affiché sur la page.

## Comment fonctionne la réservation ?

La page affiche les créneaux proposés par le système. Lorsque vous confirmez, l’application vérifie que le créneau est encore libre, enregistre le rendez-vous et affiche une confirmation avec le nom du médecin, la date et l’heure.

## À savoir

- Cette version est une démonstration : l’avance affichée est simulée et aucun paiement réel n’est effectué.
- Les données sont conservées temporairement en mémoire et sont effacées lorsque le service redémarre.
- N’entrez pas de vraies informations personnelles ou médicales.
- L’assistant de démonstration ne donne pas de diagnostic. En cas d’urgence, contactez les services d’urgence locaux.
