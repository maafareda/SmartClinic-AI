namespace SmartClinic.Domain;

public enum AppointmentStatus
{
    PendingPayment,
    Confirmed,
    CheckedIn,
    Waiting,
    InProgress,
    Completed,
    Settled,
    Cancelled,
    NoShow,
    Expired
}

public enum RequestStatus
{
    Pending,
    InProgress,
    WaitingForPatient,
    Resolved,
    Rejected
}

public enum RequestPriority
{
    Low,
    Normal,
    High,
    Urgent
}

public sealed record WorkingPeriod(DayOfWeek Day, TimeOnly Start, TimeOnly End);

public sealed record ClinicSettings(
    decimal DepositAmount = 50m,
    int SlotDurationMinutes = 10,
    int LateToleranceMinutes = 5,
    int CheckInOpensMinutesBefore = 15,
    int SlotHoldMinutes = 10,
    string Currency = "MAD");

public sealed record Clinic(
    Guid Id,
    string Name,
    string Slug,
    string Address,
    string City,
    string Phone,
    string Timezone,
    ClinicSettings Settings);

public sealed record Doctor(
    Guid Id,
    Guid ClinicId,
    string FullName,
    string Specialty,
    decimal ConsultationPrice,
    int SlotDurationMinutes,
    IReadOnlyList<WorkingPeriod> WorkingPeriods,
    bool IsActive = true);

public sealed record Patient(
    Guid Id,
    Guid ClinicId,
    string FirstName,
    string LastName,
    string Phone,
    string PreferredLanguage,
    DateTimeOffset CreatedAt);

public sealed record AppointmentTransition(
    AppointmentStatus From,
    AppointmentStatus To,
    DateTimeOffset At,
    string Actor,
    string? Reason = null);

public sealed record PaymentTransaction(
    Guid Id,
    Guid AppointmentId,
    decimal Amount,
    string Type,
    string Method,
    string ProviderReference,
    DateTimeOffset CreatedAt);

public sealed class Appointment
{
    private readonly List<AppointmentTransition> _history = [];
    private readonly List<PaymentTransaction> _transactions = [];

    public Guid Id { get; }
    public Guid ClinicId { get; }
    public Guid PatientId { get; }
    public Guid DoctorId { get; }
    public DateTimeOffset StartAt { get; }
    public DateTimeOffset EndAt { get; }
    public decimal ConsultationPrice { get; }
    public decimal DepositAmount { get; }
    public decimal BalancePaid { get; private set; }
    public decimal RemainingAmount => Math.Max(0, ConsultationPrice - DepositAmount - BalancePaid);
    public bool DepositForfeited { get; private set; }
    public AppointmentStatus Status { get; private set; } = AppointmentStatus.PendingPayment;
    public DateTimeOffset? CheckedInAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string CheckInCode { get; }
    public string CheckInMethod { get; private set; } = "Code";
    public IReadOnlyList<AppointmentTransition> History => _history.AsReadOnly();
    public IReadOnlyList<PaymentTransaction> Transactions => _transactions.AsReadOnly();

    public Appointment(
        Guid id,
        Guid clinicId,
        Guid patientId,
        Guid doctorId,
        DateTimeOffset startAt,
        int durationMinutes,
        decimal consultationPrice,
        decimal depositAmount,
        string checkInCode)
    {
        if (durationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        }

        if (consultationPrice < 0 || depositAmount < 0 || depositAmount > consultationPrice)
        {
            throw new ArgumentOutOfRangeException(nameof(depositAmount));
        }

        if (string.IsNullOrWhiteSpace(checkInCode))
        {
            throw new ArgumentException("Un code de check-in est obligatoire.", nameof(checkInCode));
        }

        Id = id;
        ClinicId = clinicId;
        PatientId = patientId;
        DoctorId = doctorId;
        StartAt = startAt;
        EndAt = startAt.AddMinutes(durationMinutes);
        ConsultationPrice = consultationPrice;
        DepositAmount = depositAmount;
        CheckInCode = checkInCode;
    }

    public void ConfirmDeposit(DateTimeOffset now, string providerReference)
    {
        EnsureStatus(AppointmentStatus.PendingPayment);
        _transactions.Add(new PaymentTransaction(
            Guid.NewGuid(), Id, DepositAmount, "Deposit", "Simulated", providerReference, now));
        TransitionTo(AppointmentStatus.Confirmed, now, "PaymentSimulator");
    }

    public void CheckIn(DateTimeOffset now, string code, int opensMinutesBefore, int lateToleranceMinutes)
    {
        EnsureStatus(AppointmentStatus.Confirmed);

        if (!string.Equals(CheckInCode, code, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Le code de check-in est invalide.");
        }

        if (now < StartAt.AddMinutes(-opensMinutesBefore) ||
            now > StartAt.AddMinutes(lateToleranceMinutes))
        {
            throw new InvalidOperationException("Le check-in est en dehors de la fenêtre autorisée.");
        }

        CheckedInAt = now;
        TransitionTo(AppointmentStatus.CheckedIn, now, "Patient");
        TransitionTo(AppointmentStatus.Waiting, now, "System");
    }

    public void CheckInManually(DateTimeOffset now, string reason)
    {
        EnsureStatus(AppointmentStatus.Confirmed);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Un motif est obligatoire pour un check-in manuel.", nameof(reason));
        }

        CheckedInAt = now;
        CheckInMethod = "Manual";
        TransitionTo(AppointmentStatus.CheckedIn, now, "Receptionist", reason);
        TransitionTo(AppointmentStatus.Waiting, now, "System");
    }

    public void StartConsultation(DateTimeOffset now)
    {
        EnsureStatus(AppointmentStatus.Waiting);
        StartedAt = now;
        TransitionTo(AppointmentStatus.InProgress, now, "Doctor");
    }

    public void CompleteConsultation(DateTimeOffset now)
    {
        EnsureStatus(AppointmentStatus.InProgress);
        CompletedAt = now;
        TransitionTo(AppointmentStatus.Completed, now, "Doctor");
    }

    public void PayBalance(DateTimeOffset now, decimal amount, string method)
    {
        EnsureStatus(AppointmentStatus.Completed);
        if (amount != RemainingAmount)
        {
            throw new InvalidOperationException("Le montant doit correspondre exactement au solde restant.");
        }

        if (amount > 0)
        {
            _transactions.Add(new PaymentTransaction(
                Guid.NewGuid(), Id, amount, "Balance", method, $"DEMO-{Guid.NewGuid():N}", now));
        }

        BalancePaid += amount;
        TransitionTo(AppointmentStatus.Settled, now, "Receptionist");
    }

    public bool TryMarkNoShow(DateTimeOffset now, int lateToleranceMinutes)
    {
        if (Status != AppointmentStatus.Confirmed || now <= StartAt.AddMinutes(lateToleranceMinutes))
        {
            return false;
        }

        DepositForfeited = DepositAmount > 0;
        TransitionTo(AppointmentStatus.NoShow, now, "System", "Délai de tolérance dépassé");
        if (DepositForfeited)
        {
            _transactions.Add(new PaymentTransaction(
                Guid.NewGuid(), Id, DepositAmount, "Forfeit", "Simulated", $"DEMO-{Guid.NewGuid():N}", now));
        }

        return true;
    }

    private void EnsureStatus(AppointmentStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Transition impossible depuis le statut {Status}.");
        }
    }

    private void TransitionTo(
        AppointmentStatus next,
        DateTimeOffset now,
        string actor,
        string? reason = null)
    {
        var previous = Status;
        Status = next;
        _history.Add(new AppointmentTransition(previous, next, now, actor, reason));
    }
}

public sealed class PatientRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ClinicId { get; init; }
    public Guid? PatientId { get; init; }
    public string Type { get; init; } = "Info";
    public string Summary { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public RequestPriority Priority { get; init; } = RequestPriority.Normal;
    public string Channel { get; init; } = "Web";
    public RequestStatus Status { get; private set; } = RequestStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolutionNote { get; private set; }

    public void UpdateStatus(RequestStatus status, string? resolutionNote, DateTimeOffset now)
    {
        if (status == RequestStatus.Resolved && string.IsNullOrWhiteSpace(resolutionNote))
        {
            throw new ArgumentException("Une note de résolution est obligatoire.", nameof(resolutionNote));
        }

        Status = status;
        ResolutionNote = resolutionNote;
        ResolvedAt = status == RequestStatus.Resolved ? now : null;
    }
}
