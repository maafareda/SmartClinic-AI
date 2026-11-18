# SmartClinic AI — Cahier des charges v2 (corrigé)

Nexus HealthTech Hackathon · Équipe 404: Sleep Not Found · Version 2.0

Cette version corrige la v1 pour la rendre fonctionnelle et cohérente avec vos décisions : **multi-cabinets, base partagée, IA avancée, arrivée automatique, canaux web + WhatsApp + SMS, langues : arabe, darija, français, anglais.**

## 0. Ce qui change par rapport à la v1

| Sujet | v1 | v2 (corrigé) |
|---|---|---|
| Arrivée du patient | La réceptionniste clique sur « Confirmer l'arrivée » | **Check-in automatique** (QR code du cabinet + code personnel), le patient entre directement dans la file |
| Retard | Pas de NoShow automatique | **Règle automatique** : créneau dépassé au-delà de la tolérance = NoShow, avance conservée, nouveau paiement requis pour reprendre RDV |
| Réception | Rôle central pour l'arrivée | Rôle d'**assistance** : inscrire les patients peu à l'aise avec le numérique, traiter les demandes IA, corriger les exceptions |
| Réservation temporaire | Mentionnée sans mécanisme | Table `SlotHolds` + contrainte d'exclusion PostgreSQL + expiration automatique |
| IA | Assistant simple | **IA avancée** : agent à outils, mémoire de conversation, RAG sur les infos du cabinet, score de confiance, passage à l'humain, évaluation |
| Demandes | Table PatientRequests | Chaque message utile génère une demande **stockée, classée et affichée en temps réel** à la réceptionniste |
| Sécurité | Liste de principes | Row Level Security PostgreSQL, minimisation des données envoyées au LLM, vérification des webhooks |

## 1. Vision et problème

Dans beaucoup de cabinets, la gestion des patients est manuelle : appels téléphoniques, cahiers papier, attente sans ordre clair, rendez-vous manqués qui font perdre du temps au médecin.

**SmartClinic AI** permet à un patient de saisir ses informations, réserver un créneau de 10 minutes, verser une avance de 50 DH sur le prix de la consultation, puis de **se présenter au cabinet et entrer directement dans la file sans passer par la réception**. Un assistant IA répond aux patients sur le web, WhatsApp et SMS, et transmet les demandes à la réceptionniste.

### Objectifs

- Supprimer l'attente inutile : l'ordre de passage suit l'heure du rendez-vous.
- Réduire les absences et retards grâce à l'avance et à une règle claire.
- Rendre l'inscription accessible, y compris pour les patients non connectés (aide de la réceptionniste).
- Automatiser les réponses et demandes courantes en 4 langues.
- Servir plusieurs cabinets sur la même plateforme sans mélange de données.

## 2. Acteurs et fonctionnalités

### 2.1 Patient

- Créer son dossier (nom, prénom, téléphone, e-mail facultatif, date de naissance, consentement).
- Choisir cabinet, médecin, date, créneau.
- Payer l'avance de 50 DH (configurable).
- Recevoir confirmation, rappels, code de check-in et QR personnel.
- Faire son **check-in automatique** à l'arrivée.
- Voir sa position dans la file et l'heure estimée de passage.
- Demander annulation ou report, discuter avec l'IA (web, WhatsApp, SMS).
- Payer le solde après la visite, télécharger le reçu.

### 2.2 Réceptionniste

- **Inscrire un patient à sa place** (patient sans smartphone ou peu à l'aise) : création du dossier, réservation, encaissement de l'avance en espèces ou carte.
- Voir en temps réel les **demandes envoyées par l'IA** (liste, priorité, statut) et les traiter.
- Consulter la file d'attente (lecture) et gérer les **exceptions** : patient en retard justifié, panne du check-in, urgence.
- Encaisser le solde après consultation.
- Rouvrir un rendez-vous marqué NoShow avec motif obligatoire (audité).

### 2.3 Médecin

- Planning du jour, file d'attente ordonnée, notification temps réel à chaque arrivée.
- Démarrer / terminer une consultation, saisir un compte rendu.
- Déclarer ses absences.

### 2.4 Administrateur du cabinet

- Informations du cabinet, médecins, réceptionnistes.
- Horaires, tarifs, durée de consultation, tolérance de retard, montant de l'avance.
- Statistiques : rendez-vous, absences, revenus.

### 2.5 Super administrateur

- Créer, activer, suspendre des cabinets ; statistiques globales.

## 3. Règles métier (version corrigée)

### 3.1 Créneaux

- Durée par défaut : **10 minutes** par médecin (configurable).
- Un créneau ne peut être confirmé qu'une fois (contrainte base de données).
- Après sélection, le créneau est **bloqué 10 minutes** (SlotHold) le temps du paiement ; sans paiement, il est libéré.

### 3.2 Paiement

- Prix exemple : 200 DH. **Avance : 50 DH** (déduite du prix). **Reste : 150 DH**, payé après la visite.
- Un rendez-vous passe à `Confirmed` uniquement après **vérification serveur** de la confirmation du prestataire (ou espèces saisies par la réceptionniste).
- Les transactions sont **immuables** (jamais modifiées, seulement complétées par une contre-écriture).

### 3.3 Check-in automatique et file d'attente

1. Le cabinet affiche un **QR code** (jeton signé, renouvelé régulièrement). Le patient le scanne avec son téléphone ; alternative : saisir son **code de check-in** (6 chiffres) sur la page de son rendez-vous.
2. Le système vérifie : rendez-vous `Confirmed`, bon cabinet, fenêtre horaire valide (de **15 minutes avant** l'heure jusqu'à l'heure + tolérance).
3. Statut → `CheckedIn` puis `Waiting`, ajout à la file.
4. Le médecin reçoit une notification temps réel.

**Ordre de passage** : selon l'heure du rendez-vous, pas selon l'ordre d'arrivée. Le patient « entre directement dans son tour », sans passer par la réception.

**Patient sans smartphone** : la réceptionniste fait le check-in manuel (cas exception), qui reste tracé.

### 3.4 Retard et perte de l'avance

Configuration par cabinet : durée de consultation (10 min), **tolérance de retard** (ex. 5 min).

Exemple : RDV 09h00, tolérance 5 min.

- Check-in avant 09h05 : normal.
- Pas de check-in à 09h05 : le système marque `NoShow` automatiquement (tâche planifiée).
- **L'avance de 50 DH n'est pas remboursée** (le médecin a réservé 10 minutes sans travailler).
- Pour être reçu, le patient doit **reprendre un rendez-vous et payer à nouveau** une avance.

Garde-fous :

- Le patient est averti avant de payer (écran + message) et reçoit un rappel.
- La réceptionniste peut **rouvrir** un NoShow justifié (urgence, panne du check-in, annulation du médecin) : l'avance est alors reportée sur un nouveau créneau, avec motif et audit.
- Si c'est le **médecin ou le cabinet** qui annule ou est absent : remboursement intégral automatique.
- Annulation par le patient **avant le délai limite** (configurable, ex. 24 h) : report ou remboursement selon la politique du cabinet.

> Hypothèse retenue : « dépasser son rendez-vous » = arriver après la tolérance. Si vous visez aussi le dépassement de durée de la consultation, ajoutez un champ `OverrunFee` (non traité dans le MVP). La politique de non-remboursement doit être validée juridiquement avant production.

### 3.5 Machine à états du rendez-vous

`PendingPayment` → `Confirmed` → `CheckedIn` → `Waiting` → `InProgress` → `Completed` (avec solde payé : `Settled`)

Sorties : `Confirmed` → `Cancelled` ; `Confirmed` → `NoShow` (automatique) ; `PendingPayment` → `Expired` ; `Confirmed` → `RescheduleRequested`.

Toute transition est écrite dans `AppointmentStatusHistory`.

### 3.6 Paiement du solde

Après `Completed`, le patient ou la réceptionniste règle le reste (150 DH dans l'exemple). Reçu généré. Le rendez-vous passe à `Settled`.

## 4. Assistant IA avancé

### 4.1 Canaux et langues

- Web : `POST /api/ai/chat` · WhatsApp : `POST /api/webhooks/whatsapp` · SMS : `POST /api/webhooks/sms`.
- Langues : arabe, **darija (arabe et arabizi)**, français, anglais, avec détection automatique et réponse dans la langue du patient.
- Les trois canaux passent par **un seul service de conversation**.

### 4.2 Capacités « advanced »

1. Détection de langue et d'intention, extraction d'entités (jour, heure, médecin, spécialité).
2. **Tool calling** : l'IA ne touche jamais la base, elle appelle des fonctions contrôlées par le backend.
3. **Mémoire de conversation** (derniers messages + résumé) et reprise entre canaux via le numéro de téléphone vérifié.
4. **RAG** sur les informations du cabinet (horaires, tarifs, adresse, FAQ, consignes) pour répondre sans inventer.
5. **Score de confiance** : en dessous d'un seuil, création d'une demande et transfert humain.
6. Détection d'**urgence médicale** : message d'orientation (urgences) + demande prioritaire à la réceptionniste, aucun diagnostic.
7. Clarification quand il manque une information (« Quel jour vous convient ? »).
8. **Journal des appels d'outils** (audit) et jeu de tests d'évaluation multilingue.

### 4.3 Intentions

`BOOK_APPOINTMENT`, `CANCEL_APPOINTMENT`, `RESCHEDULE_APPOINTMENT`, `CHECK_APPOINTMENT`, `ASK_PRICE`, `ASK_WORKING_HOURS`, `ASK_DOCTOR_AVAILABILITY`, `ASK_PAYMENT_STATUS`, `ASK_CHECKIN_HELP`, `GENERAL_INFORMATION`, `HUMAN_SUPPORT`, `MEDICAL_QUESTION`, `URGENT`.

### 4.4 Outils métier exposés au LLM

```
GetClinicInformation(clinicId)
SearchDoctors(clinicId, specialty?)
GetAvailableSlots(doctorId, date, afterTime?)
HoldSlot(doctorId, startAt, patientId)
GetPatientAppointments(patientId)
GetAppointmentStatus(appointmentId)
CreateAppointmentRequest(...)
RequestCancellation(appointmentId, reason)
RequestReschedule(appointmentId, preferredTimes)
GetPaymentLink(appointmentId)
CreatePatientRequest(type, description, priority)
TransferToReceptionist(conversationId, reason)
```

Chaque outil reçoit `ClinicId` et `PatientId` **depuis la session authentifiée, jamais depuis le texte du LLM**. Le backend revérifie droits, disponibilités et règles.

### 4.5 Gestion des demandes

À chaque message nécessitant une action humaine :

1. Le LLM répond au patient dans sa langue (accusé de réception clair).
2. Le backend crée une `PatientRequest` (type, résumé, priorité, canal, lien vers la conversation).
3. Elle est **stockée** et **poussée en temps réel** (SignalR) au tableau de bord de la réceptionniste du bon cabinet.
4. La réceptionniste la prend en charge, la traite, change le statut (`Pending`, `InProgress`, `WaitingForPatient`, `Resolved`, `Rejected`).
5. Le patient reçoit la réponse sur le même canal (et l'IA peut la reformuler).

### 4.6 Garde-fous

- Pas de diagnostic ni de prescription.
- Jamais d'invention de disponibilité : réponse uniquement basée sur `GetAvailableSlots`.
- Données envoyées au LLM minimisées (pas de notes médicales ; téléphone masqué).
- Défense contre l'injection de prompt : le contenu des messages est traité comme donnée, jamais comme instruction.
- Vérification d'identité par OTP avant de révéler des informations personnelles via WhatsApp/SMS.

## 5. Notifications

| Événement | Destinataire | Canal |
|---|---|---|
| Créneau réservé, en attente de paiement | Patient | Web |
| Rendez-vous confirmé + QR + code | Patient | WhatsApp / SMS / e-mail |
| Rappel (J-1 et 1 h avant) avec règle de retard | Patient | WhatsApp / SMS |
| Patient arrivé (check-in) | Médecin | Temps réel (SignalR) |
| Nouvelle demande IA | Réceptionniste | Temps réel |
| NoShow automatique | Patient + réceptionniste | WhatsApp / SMS + temps réel |
| Reçu / solde | Patient | WhatsApp / e-mail |

## 6. Modèle de données détaillé (PostgreSQL)

Principe : **base partagée, schéma partagé**. Toute table métier porte `ClinicId`, protégé par **Row Level Security** (`current_setting('app.clinic_id')`). Identifiants UUID. Dates en `TIMESTAMPTZ` (UTC, affichage Africa/Casablanca). Soft delete via `DeletedAt` quand nécessaire.

### 6.1 Plateforme et accès

| Table | Colonnes principales |
|---|---|
| **Clinics** | Id, Name, Slug, Address, City, Phone, Email, Timezone, Status (Active/Suspended), CreatedAt |
| **ClinicSettings** | ClinicId (PK), DepositAmount (50), SlotDurationDefault (10), LateToleranceMinutes (5), CheckInOpensMinutesBefore (15), CancellationDeadlineHours (24), SlotHoldMinutes (10), Currency (MAD), WhatsAppNumber, SmsSenderId, AiEnabled, AiHandoffThreshold, CheckInQrSecret |
| **Users** | Id, FullName, Email, Phone, PasswordHash, PreferredLanguage, IsPlatformAdmin, IsActive, CreatedAt |
| **ClinicMembers** | Id, ClinicId, UserId, Role (ClinicAdmin/Doctor/Receptionist), IsActive, CreatedAt · UNIQUE(ClinicId, UserId) |
| **RefreshTokens** | Id, UserId, TokenHash, ExpiresAt, RevokedAt |

### 6.2 Patients

| Table | Colonnes principales |
|---|---|
| **Patients** | Id, ClinicId, UserId (nullable), FirstName, LastName, Phone, PhoneVerifiedAt, Email, DateOfBirth, Address, PreferredLanguage, RegisteredBy (Self/Receptionist), CreatedBy, CheckInCode (hash), NoShowCount, CreatedAt, DeletedAt · UNIQUE(ClinicId, Phone) |
| **Consents** | Id, ClinicId, PatientId, Type (DataProcessing/Whatsapp/Sms), GivenAt, WithdrawnAt, CollectedBy (Self/Receptionist), Version |

Les doublons sont évités par l'unicité (ClinicId, Phone) et la recherche approchée (nom + date de naissance) à la création par la réceptionniste.

### 6.3 Médecins et planning

| Table | Colonnes principales |
|---|---|
| **Doctors** | Id, ClinicId, UserId, Specialty, SlotDurationMinutes, ConsultationPrice, DepositAmount (nullable, sinon réglage du cabinet), IsActive |
| **Schedules** | Id, ClinicId, DoctorId, DayOfWeek (0-6), StartTime, EndTime, IsActive (plusieurs lignes par jour pour les pauses) |
| **DoctorAbsences** | Id, ClinicId, DoctorId, StartAt, EndAt, Reason |

### 6.4 Rendez-vous, créneaux, file

| Table | Colonnes principales |
|---|---|
| **Appointments** | Id, ClinicId, PatientId, DoctorId, StartAt, EndAt, ConsultationPrice, DepositAmount, Status, BookingSource (Web/WhatsApp/Sms/Reception), CheckInMethod (Qr/Code/Manual), CheckedInAt, StartedAt, CompletedAt, NoShowAt, NoShowReason, RescheduledFromId, CreatedBy, CreatedAt |
| **SlotHolds** | Id, ClinicId, DoctorId, StartAt, EndAt, PatientId, ExpiresAt, Status (Active/Converted/Expired) |
| **AppointmentStatusHistory** | Id, ClinicId, AppointmentId, FromStatus, ToStatus, Actor (User/System/AI), ActorId, Reason, CreatedAt |
| **Consultations** | Id, ClinicId, AppointmentId, DoctorId, StartAt, EndAt, Notes (chiffré) |

La file d'attente est une **vue** : rendez-vous du jour avec Status = Waiting, triés par StartAt.

Anti-chevauchement :

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE "Appointments" ADD CONSTRAINT no_overlap
  EXCLUDE USING gist (
    "DoctorId" WITH =,
    tstzrange("StartAt", "EndAt") WITH &&
  ) WHERE ("Status" IN ('PendingPayment','Confirmed','CheckedIn','Waiting','InProgress'));
```

Le `SlotHold` crée directement un rendez-vous `PendingPayment` avec `ExpiresAt` ; si le paiement n'arrive pas, une tâche planifiée passe le rendez-vous à `Expired` et libère le créneau.

### 6.5 Paiements

| Table | Colonnes principales |
|---|---|
| **Payments** | Id, ClinicId, AppointmentId (UNIQUE), TotalAmount, DepositPaid, BalancePaid, RemainingAmount, DepositForfeited (bool), Status (Pending/DepositPaid/Settled/Forfeited/Refunded), CreatedAt |
| **PaymentTransactions** | Id, ClinicId, PaymentId, Amount, Type (Deposit/Balance/Refund/Forfeit), Method (Card/Cash/Other), ProviderReference (UNIQUE), IdempotencyKey (UNIQUE), Status (Pending/Succeeded/Failed), RecordedBy, CreatedAt |
| **Receipts** | Id, ClinicId, PaymentId, Number, IssuedAt, FileUrl |
| **WebhookEvents** | Id, Provider, ExternalEventId (UNIQUE), EventType, Status, ReceivedAt, ProcessedAt |

Transactions en ajout seul : jamais de UPDATE sur les montants (droits SQL restreints).

### 6.6 IA et demandes

| Table | Colonnes principales |
|---|---|
| **Conversations** | Id, ClinicId, PatientId (nullable), ExternalContact (téléphone/identifiant), Channel (Web/WhatsApp/Sms), Language, Status (Open/HandedOff/Closed), Summary, LastMessageAt, CreatedAt |
| **Messages** | Id, ClinicId, ConversationId, SenderType (Patient/Ai/Staff), Content, Language, Intent, Confidence, ProviderMessageId (UNIQUE), CreatedAt |
| **AiToolCalls** | Id, ClinicId, MessageId, ToolName, Arguments (JSONB), Result (JSONB), Success, DurationMs, CreatedAt |
| **PatientRequests** | Id, ClinicId, PatientId (nullable), ConversationId, Type (Booking/Cancel/Reschedule/Payment/Info/Urgent/Other), Summary, Description, Priority (Low/Normal/High/Urgent), Status, AssignedTo, ResolutionNote, CreatedAt, ResolvedAt |
| **ClinicKnowledge** | Id, ClinicId, Title, Content, Language, Embedding (pgvector, vector(1536)), UpdatedAt |

### 6.7 Transverse

| Table | Colonnes principales |
|---|---|
| **Notifications** | Id, ClinicId, RecipientType, RecipientId, Channel (Web/WhatsApp/Sms/Email/Realtime), TemplateKey, Content, Status, SentAt, Error |
| **AuditLogs** | Id, ClinicId, UserId, Action, EntityType, EntityId, Before (JSONB), After (JSONB), Ip, CreatedAt |

### 6.8 Index et contraintes clés

- Index `(ClinicId, ...)` sur toutes les tables métier ; `(DoctorId, StartAt)` sur Appointments ; `(ClinicId, Phone)` sur Patients ; `(ClinicId, Status, CreatedAt)` sur PatientRequests.
- Clés étrangères composites `(ClinicId, Id)` pour empêcher les liens entre cabinets.
- Index partiel sur Appointments pour la tâche NoShow : `WHERE Status = 'Confirmed'`.

## 7. Architecture technique

### 7.1 Stack

| Couche | Technologie |
|---|---|
| Frontend | React + TypeScript + Tailwind CSS, i18n (FR/EN/AR), **RTL** pour l'arabe et la darija |
| Backend | ASP.NET Core Web API (C#), Clean Architecture en monolithe modulaire |
| Accès aux données | Entity Framework Core + Npgsql, RLS PostgreSQL |
| Base | PostgreSQL 16 (+ btree_gist, pgvector) |
| Temps réel | SignalR (groupes par `clinic-{id}` et `doctor-{id}`) |
| Cache / verrous | Redis (sessions de chat, limitation de débit, verrous courts) |
| Tâches planifiées | Hangfire ou BackgroundService (expiration des créneaux, NoShow, rappels) |
| IA | LLM avec tool calling, embeddings pour le RAG |
| Canaux | WhatsApp Cloud API, fournisseur SMS (webhooks signés) |
| Paiement | Simulé (MVP), prestataire marocain agréé ensuite (CMI / PSP) |
| Auth | ASP.NET Core Identity + JWT + refresh, OTP téléphone |
| DevOps | Docker Compose, GitHub Actions, Swagger/OpenAPI |
| Tests | xUnit (concurrence créneaux, règle NoShow, isolation tenant) |

### 7.2 Vue d'ensemble

Patient Portal, Reception Dashboard, Doctor Dashboard, Admin Dashboards (React) → API ASP.NET → modules (Auth, Clinics, Patients, Scheduling, Appointments, CheckIn, Payments, AI, Notifications, Reporting) → PostgreSQL / Redis / LLM / WhatsApp / SMS / PSP.

### 7.3 Isolation multi-cabinets

1. Le JWT contient `clinic_ids` et le rôle par cabinet.
2. Un middleware résout le cabinet courant (en-tête `X-Clinic-Id` validé contre le JWT).
3. À l'ouverture de connexion : `SET app.clinic_id = '...'`.
4. Policies RLS : `USING ("ClinicId" = current_setting('app.clinic_id')::uuid)`.
5. Filtre global EF Core en seconde protection.
6. Test d'intégration : « le cabinet A ne voit jamais les données du cabinet B ».

### 7.4 Moteur de créneaux

Entrées : Schedules, DoctorAbsences, Appointments actifs, durée. Calcul : découper les plages horaires en créneaux de N minutes, retirer les créneaux occupés ou passés, retourner les libres. La contrainte d'exclusion est le **dernier rempart** contre la double réservation (la concurrence renvoie `409 Conflict`).

### 7.5 Check-in automatique

- `GET /api/clinics/{id}/checkin-qr` (affichage sur écran du cabinet, jeton JWT court renouvelé toutes les 60 s).
- `POST /api/appointments/{id}/check-in` avec jeton QR ou code : vérifie fenêtre horaire, statut, cabinet, puis passe à `Waiting` et émet l'événement SignalR vers le médecin.
- Un contrôle de proximité (géolocalisation) est optionnel, non bloquant.

### 7.6 Tâches planifiées (chaque minute)

- Expirer les `PendingPayment` dépassés.
- Marquer `NoShow` les `Confirmed` dont `StartAt + tolérance` est dépassé, écrire la transaction `Forfeit`, notifier.
- Envoyer les rappels.

Chaque tâche est **idempotente** et verrouillée (Redis ou Hangfire).

### 7.7 Pipeline IA

1. Webhook/entrée reçue → vérification de signature → déduplication par `ProviderMessageId`.
2. Identification du cabinet (numéro WhatsApp/SMS ou site) et du patient (téléphone).
3. Chargement du contexte autorisé (historique, RDV, infos cabinet via RAG).
4. Appel du LLM avec outils ; boucle outil → résultat → réponse.
5. Validation et exécution par le backend ; enregistrement Message, AiToolCalls, PatientRequest.
6. Envoi de la réponse sur le canal d'origine ; notification SignalR à la réception.

Le module IA est une bibliothèque indépendante (`SmartClinic.Infrastructure/AI`) qui n'appelle que les services applicatifs.

### 7.8 Structure du code

```
SmartClinicAI/
├── src/
│   ├── SmartClinic.Domain/          (entités, enums, règles : NoShowPolicy, DepositPolicy)
│   ├── SmartClinic.Application/     (use cases, DTOs, validators, interfaces)
│   ├── SmartClinic.Infrastructure/  (EF Core, AI, Payments, WhatsApp, SMS, Jobs)
│   └── SmartClinic.API/             (controllers, middleware, SignalR hubs, webhooks)
├── tests/ (UnitTests, IntegrationTests)
└── frontend/
    └── src/ (pages/patient|receptionist|doctor|clinic-admin|super-admin, services, hooks, i18n)
```

### 7.9 API REST (principale)

| Domaine | Endpoints |
|---|---|
| Auth | `POST /api/auth/register`, `login`, `refresh`, `logout`, `otp/send`, `otp/verify` |
| Cabinets | `GET/POST /api/clinics`, `GET/PUT /api/clinics/{id}`, `GET/PUT /api/clinics/{id}/settings` |
| Patients | `GET/POST /api/patients`, `GET/PUT /api/patients/{id}`, `GET /api/patients/search?phone=` |
| Créneaux | `GET /api/doctors`, `GET /api/appointments/available-slots` |
| Rendez-vous | `POST /api/appointments`, `GET /api/appointments/{id}`, `POST /{id}/check-in`, `PATCH /{id}/start`, `PATCH /{id}/complete`, `POST /{id}/cancel`, `POST /{id}/reopen` (réception) |
| File | `GET /api/queue/today` |
| Paiements | `POST /api/payments/deposit`, `POST /api/payments/webhook`, `POST /api/payments/balance`, `GET /api/payments/{id}` |
| IA | `POST /api/ai/chat`, `GET /api/ai/conversations`, `GET /api/ai/requests`, `PATCH /api/ai/requests/{id}/assign`, `PATCH /api/ai/requests/{id}/resolve` |
| Webhooks | `POST /api/webhooks/whatsapp`, `POST /api/webhooks/sms` |
| Dashboard | `GET /api/dashboard/overview`, `appointments`, `revenue`, `patient-requests` |

## 8. Sécurité et conformité

- HTTPS obligatoire, mots de passe hachés (Identity), JWT courts + refresh révocables.
- Autorisation par rôle **et** par cabinet ; RLS PostgreSQL.
- Signature des webhooks (WhatsApp, SMS, paiement) vérifiée ; événements idempotents.
- Chiffrement des notes de consultation et des champs sensibles ; sauvegardes chiffrées.
- Limitation de débit sur le chat et l'OTP.
- Consentements tracés ; politique de conservation et de suppression.
- Le LLM ne reçoit aucune note médicale et le minimum de données personnelles.
- Loi marocaine 09-08 et CNDP : déclarations et autorisations à vérifier avant toute mise en production (données de santé).

## 9. Périmètre MVP réaliste pour 2 jours

**À livrer (chaîne de démo) :**

1. Auth + rôles, 2 cabinets seedés (isolation RLS visible).
2. Inscription patient (par lui-même **et** par la réceptionniste).
3. Médecin + horaires + créneaux de 10 min.
4. Réservation avec blocage de créneau, avance de 50 DH **simulée**, confirmation.
5. **Check-in automatique** par QR/code, file d'attente, notification temps réel au médecin.
6. **Règle NoShow** automatique (tolérance réduite à 1 min en mode démo) avec avance conservée.
7. Chat IA (web) en darija/français : consultation de créneaux, création de `PatientRequest`.
8. Dashboard réceptionniste : demandes IA en temps réel, gestion des statuts.
9. Solde après consultation + reçu.

**Simulé ou reporté :** WhatsApp et SMS (adaptateurs prêts, démo via simulateur de webhook), paiement réel, dashboards Super Admin et statistiques avancées, RAG complet (FAQ statique en MVP).

### Répartition suggérée (équipe de 4)

| Rôle | Jour 1 | Jour 2 |
|---|---|---|
| Backend A | Auth, multi-tenant, RLS, patients | Check-in, file, NoShow, paiements |
| Backend B | Médecins, créneaux, rendez-vous, anti-chevauchement | Tâches planifiées, SignalR, tests |
| IA | Pipeline, outils, prompt darija/FR, jeu de tests | Demandes, handoff, adaptateurs WhatsApp/SMS |
| Frontend | Portail patient, réservation | Dashboards réception + médecin, i18n/RTL |

## 10. Scénario de démonstration

1. Le cabinet A et le cabinet B existent ; connexion au A, puis au B : données différentes.
2. La réceptionniste inscrit un patient peu à l'aise avec le numérique (aide à l'inscription).
3. Un autre patient réserve à 10 min, paie l'avance simulée de 50 DH, reçoit son QR.
4. Il écrit en darija : « bghit n9delm rdv dyali l 3 » ; l'IA répond et crée la demande.
5. La demande apparaît en direct chez la réceptionniste, qui la traite.
6. Le patient scanne le QR : check-in automatique, il entre dans la file ; le médecin est notifié instantanément.
7. Un second patient n'arrive pas : NoShow automatique, avance conservée, nouvelle avance exigée pour reprendre RDV.
8. Après la consultation, paiement du solde de 150 DH et reçu.

## 11. Critères de réussite

- Isolation des cabinets prouvée par un test.
- Deux patients ne peuvent jamais obtenir le même créneau (test de concurrence).
- Check-in automatique fonctionnel sans intervention de la réceptionniste.
- NoShow et conservation de l'avance appliqués automatiquement, et réversibles par la réception avec audit.
- Chaque message IA utile génère une demande stockée et visible en temps réel.
- L'IA n'invente aucune disponibilité et ne donne aucun diagnostic.
- Aucune confirmation de paiement sans vérification côté serveur.

## 12. Suite de conception proposée

1. **Maquettes** (3 écrans clés) : réservation patient, file du médecin, dashboard réceptionniste.
2. **Diagramme de séquence** : réservation → paiement → check-in → consultation → solde.
3. **Script SQL initial** : tables, extension `btree_gist`, contrainte d'exclusion, policies RLS, données de seed.
4. **Prompt système de l'IA** et 30 phrases de test (darija, arabizi, arabe, français, anglais).
5. **Backlog** priorisé par jour avec critères d'acceptation.
6. **Pitch** : 5 slides (problème, démo, IA en darija, sécurité des données, roadmap).

Dites-moi par quoi commencer (script SQL, prompt IA, diagramme de séquence ou maquettes) et je le produis directement.
