using SmartClinic.Application;
using SmartClinic.Domain;

namespace SmartClinic.Infrastructure;

public static class DemoIds
{
    public static readonly Guid ClinicAtlas = Guid.Parse("a1000000-0000-4000-8000-000000000001");
    public static readonly Guid ClinicRiad = Guid.Parse("a1000000-0000-4000-8000-000000000002");
    public static readonly Guid DoctorAtlas = Guid.Parse("b1000000-0000-4000-8000-000000000001");
    public static readonly Guid DoctorRiad = Guid.Parse("b1000000-0000-4000-8000-000000000002");
}

public sealed class DemoClinicRepository : IClinicRepository
{
    private readonly object _sync = new();
    private readonly List<Clinic> _clinics;
    private readonly List<Doctor> _doctors;
    private readonly List<Patient> _patients = [];
    private readonly List<Appointment> _appointments = [];
    private readonly List<PatientRequest> _requests = [];

    public DemoClinicRepository()
    {
        _clinics =
        [
            new Clinic(
                DemoIds.ClinicAtlas, "Cabinet Atlas", "cabinet-atlas",
                "12 avenue Mohammed V", "Casablanca", "+212 522 00 00 01",
                "Africa/Casablanca", new ClinicSettings()),
            new Clinic(
                DemoIds.ClinicRiad, "Clinique Riad", "clinique-riad",
                "8 rue des Orangers", "Rabat", "+212 537 00 00 02",
                "Africa/Casablanca", new ClinicSettings(DepositAmount: 75m))
        ];

        _doctors =
        [
            new Doctor(
                DemoIds.DoctorAtlas, DemoIds.ClinicAtlas, "Dr. Yasmine Bennani",
                "Médecine générale", 200m, 10, CreateWeeklySchedule()),
            new Doctor(
                DemoIds.DoctorRiad, DemoIds.ClinicRiad, "Dr. Omar El Idrissi",
                "Cardiologie", 350m, 10, CreateWeeklySchedule())
        ];

        SeedClinicAppointments(_clinics[0], _doctors[0]);
        SeedClinicAppointments(_clinics[1], _doctors[1]);
        SeedRequests();
    }

    public IReadOnlyList<Clinic> GetClinics()
    {
        lock (_sync)
        {
            return _clinics.ToArray();
        }
    }

    public Clinic? GetClinic(Guid clinicId)
    {
        lock (_sync)
        {
            return _clinics.FirstOrDefault(item => item.Id == clinicId);
        }
    }

    public IReadOnlyList<Doctor> GetDoctors(Guid clinicId)
    {
        lock (_sync)
        {
            return _doctors.Where(item => item.ClinicId == clinicId).ToArray();
        }
    }

    public Doctor? GetDoctor(Guid clinicId, Guid doctorId)
    {
        lock (_sync)
        {
            return _doctors.FirstOrDefault(item => item.Id == doctorId && item.ClinicId == clinicId);
        }
    }

    public IReadOnlyList<Patient> GetPatients(Guid clinicId)
    {
        lock (_sync)
        {
            return _patients.Where(item => item.ClinicId == clinicId).ToArray();
        }
    }

    public Patient? GetPatientByPhone(Guid clinicId, string phone)
    {
        lock (_sync)
        {
            return _patients.FirstOrDefault(item =>
                item.ClinicId == clinicId && string.Equals(item.Phone, phone, StringComparison.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyList<Appointment> GetAppointments(Guid clinicId)
    {
        lock (_sync)
        {
            return _appointments.Where(item => item.ClinicId == clinicId).ToArray();
        }
    }

    public IReadOnlyList<PatientRequest> GetRequests(Guid clinicId)
    {
        lock (_sync)
        {
            return _requests.Where(item => item.ClinicId == clinicId).ToArray();
        }
    }

    public bool TryCreateAppointment(Patient patient, Appointment appointment)
    {
        lock (_sync)
        {
            if (appointment.ClinicId != patient.ClinicId ||
                !_clinics.Any(item => item.Id == appointment.ClinicId) ||
                !_doctors.Any(item => item.Id == appointment.DoctorId && item.ClinicId == appointment.ClinicId))
            {
                return false;
            }

            var overlaps = _appointments.Any(existing =>
                existing.ClinicId == appointment.ClinicId &&
                existing.DoctorId == appointment.DoctorId &&
                existing.Status is AppointmentStatus.PendingPayment or AppointmentStatus.Confirmed
                    or AppointmentStatus.CheckedIn or AppointmentStatus.Waiting or AppointmentStatus.InProgress &&
                existing.StartAt < appointment.EndAt && existing.EndAt > appointment.StartAt);
            if (overlaps)
            {
                return false;
            }

            var existingPatient = _patients.FirstOrDefault(item =>
                item.ClinicId == patient.ClinicId &&
                string.Equals(item.Phone, patient.Phone, StringComparison.OrdinalIgnoreCase));
            if (existingPatient is not null && existingPatient.Id != appointment.PatientId)
            {
                return false;
            }

            if (existingPatient is null)
            {
                _patients.Add(patient);
            }

            _appointments.Add(appointment);
            return true;
        }
    }

    public void AddRequest(PatientRequest request)
    {
        lock (_sync)
        {
            _requests.Add(request);
        }
    }

    public bool TryUpdateAppointment(Guid clinicId, Guid appointmentId, Func<Appointment, bool> update)
    {
        lock (_sync)
        {
            var appointment = _appointments.FirstOrDefault(item =>
                item.Id == appointmentId && item.ClinicId == clinicId);
            return appointment is not null && update(appointment);
        }
    }

    public bool TryUpdateRequest(Guid clinicId, Guid requestId, Action<PatientRequest> update)
    {
        lock (_sync)
        {
            var request = _requests.FirstOrDefault(item => item.Id == requestId && item.ClinicId == clinicId);
            if (request is null)
            {
                return false;
            }

            update(request);
            return true;
        }
    }

    private void SeedClinicAppointments(Clinic clinic, Doctor doctor)
    {
        var zoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId(clinic.Timezone, out var windowsTimeZoneId)
            ? windowsTimeZoneId
            : clinic.Timezone;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var clinicNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        var date = DateOnly.FromDateTime(clinicNow.Date);
        var localStart = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(9, 0)), DateTimeKind.Unspecified);
        var startAt = new DateTimeOffset(localStart, timeZone.GetUtcOffset(localStart));
        var patients = new[]
        {
            new Patient(Guid.NewGuid(), clinic.Id, "Salma", "Amrani", "+212 6 12 34 56 01", "fr", DateTimeOffset.UtcNow),
            new Patient(Guid.NewGuid(), clinic.Id, "Mehdi", "Tazi", "+212 6 12 34 56 02", "ar", DateTimeOffset.UtcNow),
            new Patient(Guid.NewGuid(), clinic.Id, "Nadia", "Fassi", "+212 6 12 34 56 03", "fr", DateTimeOffset.UtcNow),
            new Patient(Guid.NewGuid(), clinic.Id, "Youssef", "Alaoui", "+212 6 12 34 56 04", "fr", DateTimeOffset.UtcNow)
        };
        lock (_sync)
        {
            _patients.AddRange(patients);

            const string waitingCode = "000001";
            var waiting = CreateSeedAppointment(clinic, doctor, patients[0], startAt, waitingCode);
            waiting.ConfirmDeposit(startAt.AddMinutes(-20), $"SEED-{Guid.NewGuid():N}");
            waiting.CheckIn(startAt.AddMinutes(-10), waitingCode,
                clinic.Settings.CheckInOpensMinutesBefore, clinic.Settings.LateToleranceMinutes);
            _appointments.Add(waiting);

            const string nextCode = "000002";
            var next = CreateSeedAppointment(clinic, doctor, patients[1], startAt.AddMinutes(10), nextCode);
            next.ConfirmDeposit(startAt.AddMinutes(-10), $"SEED-{Guid.NewGuid():N}");
            next.CheckIn(startAt, nextCode,
                clinic.Settings.CheckInOpensMinutesBefore, clinic.Settings.LateToleranceMinutes);
            _appointments.Add(next);

            const string inConsultationCode = "000003";
            var inConsultation = CreateSeedAppointment(clinic, doctor, patients[2], startAt.AddMinutes(20), inConsultationCode);
            inConsultation.ConfirmDeposit(startAt, $"SEED-{Guid.NewGuid():N}");
            inConsultation.CheckIn(startAt.AddMinutes(10), inConsultationCode,
                clinic.Settings.CheckInOpensMinutesBefore, clinic.Settings.LateToleranceMinutes);
            inConsultation.StartConsultation(startAt.AddMinutes(20));
            _appointments.Add(inConsultation);

            const string noShowCode = "000004";
            var noShow = CreateSeedAppointment(clinic, doctor, patients[3], startAt.AddMinutes(30), noShowCode);
            noShow.ConfirmDeposit(startAt.AddMinutes(10), $"SEED-{Guid.NewGuid():N}");
            noShow.TryMarkNoShow(startAt.AddMinutes(36), clinic.Settings.LateToleranceMinutes);
            _appointments.Add(noShow);
        }
    }

    private Appointment CreateSeedAppointment(
        Clinic clinic,
        Doctor doctor,
        Patient patient,
        DateTimeOffset startAt,
        string code) =>
        new(Guid.NewGuid(), clinic.Id, patient.Id, doctor.Id, startAt, doctor.SlotDurationMinutes,
            doctor.ConsultationPrice, clinic.Settings.DepositAmount, code);

    private void SeedRequests()
    {
        _requests.Add(new PatientRequest
        {
            ClinicId = DemoIds.ClinicAtlas,
            Type = "Reschedule",
            Summary = "Demande de changement d'horaire",
            Description = "La patiente souhaite déplacer son rendez-vous de jeudi après-midi.",
            Priority = RequestPriority.High,
            Channel = "Web",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-12)
        });
        _requests.Add(new PatientRequest
        {
            ClinicId = DemoIds.ClinicAtlas,
            Type = "Info",
            Summary = "Question sur les tarifs",
            Description = "Le patient souhaite connaître les moyens de paiement acceptés.",
            Priority = RequestPriority.Normal,
            Channel = "WhatsApp",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-28)
        });
        _requests.Add(new PatientRequest
        {
            ClinicId = DemoIds.ClinicRiad,
            Type = "Info",
            Summary = "Demande d'informations",
            Description = "Demande de disponibilité pour une première consultation.",
            Priority = RequestPriority.Normal,
            Channel = "SMS",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-8)
        });
    }

    private static IReadOnlyList<WorkingPeriod> CreateWeeklySchedule() =>
        Enum.GetValues<DayOfWeek>()
            .Where(day => day is not DayOfWeek.Sunday)
            .SelectMany(day => new[]
            {
                new WorkingPeriod(day, new TimeOnly(9, 0), new TimeOnly(12, 0)),
                new WorkingPeriod(day, new TimeOnly(14, 0), new TimeOnly(17, 0))
            })
            .ToArray();
}
