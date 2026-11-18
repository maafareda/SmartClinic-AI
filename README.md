# SmartClinic AI

MVP de démonstration construit à partir du [cahier des charges v2](description/SmartClinic_AI_Cahier_des_charges_v2.md).

## Démarrage local

Prérequis : SDK .NET 10.

```powershell
dotnet test SmartClinicAI.sln
dotnet run --project src\SmartClinic.API
```

Ouvrir ensuite `http://localhost:5106` (ou l'adresse affichée par ASP.NET Core). La page d'accueil est l'espace réceptionniste ; `/patient` ouvre l'espace public de réservation en ligne. `/health` et `/api/clinics` exposent des routes de contrôle.

## Périmètre livré

- Solution .NET organisée en Domain, Application, Infrastructure et API.
- Tableau de bord réceptionniste en français, avec adaptation RTL, navigation multi-cabinets et vues accueil, médecin, demandes patients et réservations.
- Espace patient séparé pour choisir un cabinet, un médecin, un créneau disponible et réserver en ligne.
- API pour les cabinets, médecins, créneaux, rendez-vous, check-in, file d'attente, paiements simulés et demandes.
- Vérification métier de la fenêtre de check-in, transitions d'état, solde exact, conservation de l'avance en cas de NoShow et réservation concurrente d'un créneau.
- Assistant déterministe de démonstration : informations générales, transfert des demandes et priorité aux urgences, sans diagnostic ni disponibilité inventée.
- Jeu de données synthétiques pour deux cabinets ; les opérations et les réservations concurrentes sont protégées par un verrou en mémoire.

## Limites de sécurité et de persistance

**Cette version est une démonstration locale, et non un service médical prêt pour la production.** Il n'y a pas d'authentification, de persistance durable, de PostgreSQL/RLS, de chiffrement, de vraie passerelle de paiement, de modèle LLM, de WhatsApp/SMS ni de SignalR. Les données sont réinitialisées au redémarrage. N'entrez aucune donnée de patient réelle et n'exposez pas le service sur Internet. Le code de check-in n'est renvoyé qu'à la confirmation de réservation ; il est conservé sous forme hachée par l'application.

Consulter [la roadmap](resultat/roadmap/roadmap.md) avant d'étendre le périmètre et [le rapport de tests](resultat/test/rapport-tests.md) pour les dernières validations.
