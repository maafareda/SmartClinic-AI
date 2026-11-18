using System.Security.Cryptography;
using SmartClinic.Domain;

namespace SmartClinic.Application;

public sealed class ClinicService(IClinicRepository repository) : IClinicService
{
    public IReadOnlyList<Clinic> GetClinics() => repository.GetClinics();

    public IReadOnlyList<Doctor> GetDoctors(Guid clinicId)
    {
        EnsureClinic(clinicId);
        return repository.GetDoctors(clinicId);
    }

    public DashboardView GetDashboard(Guid clinicId, DateTimeOffset now)
    {
        var clinic = EnsureClinic(clinicId);
        var appointments = repository.GetAppointments(clinicId);
        var requests = repository.GetRequests(clinicId);
        var localDate = TimeZoneInfo.ConvertTime(now, ResolveTimeZone(clinic.Timezone)).Date;
        var todaysAppointments = appointments.Where(item => item.StartAt.Date == localDate).ToArray();

        return new DashboardView(
            todaysAppointments.Length,
            todaysAppointments.Count(item => item.Status == AppointmentStatus.Waiting),
            todaysAppointments.Count(item => item.Status == AppointmentStatus.InProgress),
            requests.Count(item => item.Status is RequestStatus.Pending or RequestStatus.InProgress),
            todaysAppointments.Sum(item => item.Transactions
                .Where(transaction => transaction.Type is "Deposit" or "Balance")
                .Sum(transaction => transaction.Amount)),
            todaysAppointments.Count(item => item.Status == AppointmentStatus.NoShow));
    }

    public IReadOnlyList<AppointmentView> GetAppointments(Guid clinicId)
    {
        EnsureClinic(clinicId);
        return repository.GetAppointments(clinicId)
            .OrderBy(item => item.StartAt)
            .Select(ToView)
            .ToArray();
    }

    public IReadOnlyList<AppointmentView> GetQueue(Guid clinicId)
    {
        var clinic = EnsureClinic(clinicId);
        var now = DateTimeOffset.UtcNow;
        var localDate = TimeZoneInfo.ConvertTime(now, ResolveTimeZone(clinic.Timezone)).Date;
        return repository.GetAppointments(clinicId)
            .Where(item => item.Status is AppointmentStatus.Waiting or AppointmentStatus.InProgress)
            .Where(item => item.StartAt.Date == localDate)
            .OrderBy(item => item.StartAt)
            .Select(ToView)
            .ToArray();
    }

    public IReadOnlyList<PatientRequestView> GetRequests(Guid clinicId)
    {
        EnsureClinic(clinicId);
        return repository.GetRequests(clinicId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(ToView)
            .ToArray();
    }

    public IReadOnlyList<AvailableSlot> GetAvailableSlots(
        Guid clinicId,
        Guid doctorId,
        DateOnly date,
        DateTimeOffset now)
    {
        var clinic = EnsureClinic(clinicId);
        var doctor = repository.GetDoctor(clinicId, doctorId)
            ?? throw new KeyNotFoundException("Médecin introuvable dans ce cabinet.");
        var timeZone = ResolveTimeZone(clinic.Timezone);
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        if (!doctor.IsActive ||
            date < DateOnly.FromDateTime(localNow.Date) ||
            date > DateOnly.FromDateTime(localNow.AddDays(30).Date))
        {
            return [];
        }

        var occupied = repository.GetAppointments(clinicId)
            .Where(item => item.DoctorId == doctorId &&
                           item.Status is AppointmentStatus.PendingPayment or AppointmentStatus.Confirmed
                               or AppointmentStatus.CheckedIn or AppointmentStatus.Waiting
                               or AppointmentStatus.InProgress)
            .ToArray();
        var slots = new List<AvailableSlot>();
        var slotDuration = doctor.SlotDurationMinutes > 0
            ? doctor.SlotDurationMinutes
            : clinic.Settings.SlotDurationMinutes;

        foreach (var period in doctor.WorkingPeriods.Where(item => item.Day == date.DayOfWeek))
        {
            var start = date.ToDateTime(period.Start);
            var end = date.ToDateTime(period.End);
            for (var candidate = start; candidate.AddMinutes(slotDuration) <= end; candidate = candidate.AddMinutes(slotDuration))
            {
                var localCandidate = DateTime.SpecifyKind(candidate, DateTimeKind.Unspecified);
                if (timeZone.IsInvalidTime(localCandidate))
                {
                    continue;
                }

                var startAt = new DateTimeOffset(localCandidate, timeZone.GetUtcOffset(localCandidate));
                var endAt = startAt.AddMinutes(slotDuration);
                if (startAt <= now || occupied.Any(item => item.StartAt < endAt && item.EndAt > startAt))
                {
                    continue;
                }

                slots.Add(new AvailableSlot(startAt, endAt));
            }
        }

        return slots;
    }

    public CreateAppointmentResult CreateAppointment(Guid clinicId, CreateAppointmentCommand command, DateTimeOffset now)
    {
        var clinic = EnsureClinic(clinicId);
        var doctor = repository.GetDoctor(clinicId, command.DoctorId)
            ?? throw new KeyNotFoundException("Médecin introuvable dans ce cabinet.");

        if (string.IsNullOrWhiteSpace(command.FirstName) ||
            string.IsNullOrWhiteSpace(command.LastName) ||
            string.IsNullOrWhiteSpace(command.Phone))
        {
            throw new ArgumentException("Le prénom, le nom et le téléphone sont obligatoires.");
        }

        var date = DateOnly.FromDateTime(command.StartAt.Date);
        var available = GetAvailableSlots(clinicId, doctor.Id, date, now)
            .Any(slot => slot.StartAt == command.StartAt);
        if (!available)
        {
            throw new InvalidOperationException("Ce créneau n'est plus disponible.");
        }

        var patient = repository.GetPatientByPhone(clinicId, command.Phone.Trim())
            ?? new Patient(
                Guid.NewGuid(),
                clinicId,
                command.FirstName.Trim(),
                command.LastName.Trim(),
                command.Phone.Trim(),
                string.IsNullOrWhiteSpace(command.PreferredLanguage) ? "fr" : command.PreferredLanguage,
                now);
        var checkInCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var appointment = new Appointment(
            Guid.NewGuid(),
            clinicId,
            patient.Id,
            doctor.Id,
            command.StartAt,
            doctor.SlotDurationMinutes,
            doctor.ConsultationPrice,
            clinic.Settings.DepositAmount,
            checkInCode);
        appointment.ConfirmDeposit(now, $"DEMO-{Guid.NewGuid():N}");

        if (!repository.TryCreateAppointment(patient, appointment))
        {
            throw new InvalidOperationException("Ce créneau vient d'être réservé par un autre patient.");
        }

        return new CreateAppointmentResult(ToView(appointment), checkInCode);
    }

    public AppointmentView CheckIn(Guid clinicId, Guid appointmentId, string code, DateTimeOffset now)
    {
        var clinic = EnsureClinic(clinicId);
        var appointment = UpdateAppointment(clinicId, appointmentId, item =>
        {
            item.CheckIn(now, code, clinic.Settings.CheckInOpensMinutesBefore, clinic.Settings.LateToleranceMinutes);
            return true;
        });
        return ToView(appointment);
    }

    public AppointmentView CheckInManually(Guid clinicId, Guid appointmentId, string reason, DateTimeOffset now)
    {
        EnsureClinic(clinicId);
        var appointment = UpdateAppointment(clinicId, appointmentId, item =>
        {
            item.CheckInManually(now, reason);
            return true;
        });
        return ToView(appointment);
    }

    public AppointmentView StartConsultation(Guid clinicId, Guid appointmentId, DateTimeOffset now)
    {
        EnsureClinic(clinicId);
        return ToView(UpdateAppointment(clinicId, appointmentId, item =>
        {
            item.StartConsultation(now);
            return true;
        }));
    }

    public AppointmentView CompleteConsultation(Guid clinicId, Guid appointmentId, DateTimeOffset now)
    {
        EnsureClinic(clinicId);
        return ToView(UpdateAppointment(clinicId, appointmentId, item =>
        {
            item.CompleteConsultation(now);
            return true;
        }));
    }

    public AppointmentView PayBalance(Guid clinicId, Guid appointmentId, DateTimeOffset now)
    {
        EnsureClinic(clinicId);
        return ToView(UpdateAppointment(clinicId, appointmentId, item =>
        {
            item.PayBalance(now, item.RemainingAmount, "Cash");
            return true;
        }));
    }

    public PatientRequestView CreateRequest(Guid clinicId, CreateRequestCommand command, DateTimeOffset now)
    {
        EnsureClinic(clinicId);
        if (string.IsNullOrWhiteSpace(command.Summary) || string.IsNullOrWhiteSpace(command.Description))
        {
            throw new ArgumentException("Le résumé et la description sont obligatoires.");
        }

        var request = new PatientRequest
        {
            ClinicId = clinicId,
            Type = string.IsNullOrWhiteSpace(command.Type) ? "Other" : command.Type,
            Summary = command.Summary.Trim(),
            Description = command.Description.Trim(),
            Priority = command.IsUrgent ? RequestPriority.Urgent : RequestPriority.Normal,
            Channel = command.Channel,
            CreatedAt = now
        };
        repository.AddRequest(request);
        return ToView(request);
    }

    public PatientRequestView UpdateRequest(
        Guid clinicId,
        Guid requestId,
        RequestStatus status,
        string? resolutionNote,
        DateTimeOffset now)
    {
        EnsureClinic(clinicId);
        PatientRequest? updated = null;
        var success = repository.TryUpdateRequest(clinicId, requestId, request =>
        {
            request.UpdateStatus(status, resolutionNote, now);
            updated = request;
        });

        return success && updated is not null
            ? ToView(updated)
            : throw new KeyNotFoundException("Demande introuvable dans ce cabinet.");
    }

    public int ProcessNoShows(DateTimeOffset now)
    {
        var marked = 0;
        foreach (var clinic in repository.GetClinics())
        {
            foreach (var appointment in repository.GetAppointments(clinic.Id))
            {
                if (repository.TryUpdateAppointment(clinic.Id, appointment.Id,
                        item => item.TryMarkNoShow(now, clinic.Settings.LateToleranceMinutes)))
                {
                    marked++;
                }
            }
        }

        return marked;
    }

    private Clinic EnsureClinic(Guid clinicId) =>
        repository.GetClinic(clinicId) ?? throw new KeyNotFoundException("Cabinet introuvable.");

    private static TimeZoneInfo ResolveTimeZone(string ianaTimeZone)
    {
        var timeZoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaTimeZone, out var windowsTimeZoneId)
            ? windowsTimeZoneId
            : ianaTimeZone;
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    private Appointment UpdateAppointment(Guid clinicId, Guid appointmentId, Func<Appointment, bool> update)
    {
        Appointment? updated = null;
        var success = repository.TryUpdateAppointment(clinicId, appointmentId, appointment =>
        {
            var changed = update(appointment);
            if (changed)
            {
                updated = appointment;
            }

            return changed;
        });

        return success && updated is not null
            ? updated
            : throw new KeyNotFoundException("Rendez-vous introuvable dans ce cabinet.");
    }

    private AppointmentView ToView(Appointment appointment)
    {
        var patient = repository.GetPatients(appointment.ClinicId)
            .FirstOrDefault(item => item.Id == appointment.PatientId)
            ?? throw new InvalidOperationException("Le patient du rendez-vous est introuvable.");
        var doctor = repository.GetDoctor(appointment.ClinicId, appointment.DoctorId)
            ?? throw new InvalidOperationException("Le médecin du rendez-vous est introuvable.");

        return new AppointmentView(
            appointment.Id,
            appointment.ClinicId,
            appointment.PatientId,
            $"{patient.FirstName} {patient.LastName}",
            patient.Phone,
            doctor.Id,
            doctor.FullName,
            doctor.Specialty,
            appointment.StartAt,
            appointment.EndAt,
            appointment.ConsultationPrice,
            appointment.DepositAmount,
            appointment.RemainingAmount,
            appointment.Status,
            appointment.CheckedInAt,
            appointment.DepositForfeited);
    }

    private static PatientRequestView ToView(PatientRequest request) =>
        new(request.Id, request.Type, request.Summary, request.Description, request.Priority,
            request.Channel, request.Status, request.CreatedAt, request.ResolutionNote);
}
