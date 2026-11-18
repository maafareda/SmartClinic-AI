using SmartClinic.Domain;

namespace SmartClinic.Application;

public interface IClinicRepository
{
    IReadOnlyList<Clinic> GetClinics();
    Clinic? GetClinic(Guid clinicId);
    IReadOnlyList<Doctor> GetDoctors(Guid clinicId);
    Doctor? GetDoctor(Guid clinicId, Guid doctorId);
    IReadOnlyList<Patient> GetPatients(Guid clinicId);
    Patient? GetPatientByPhone(Guid clinicId, string phone);
    IReadOnlyList<Appointment> GetAppointments(Guid clinicId);
    IReadOnlyList<PatientRequest> GetRequests(Guid clinicId);
    bool TryCreateAppointment(Patient patient, Appointment appointment);
    void AddRequest(PatientRequest request);
    bool TryUpdateAppointment(Guid clinicId, Guid appointmentId, Func<Appointment, bool> update);
    bool TryUpdateRequest(Guid clinicId, Guid requestId, Action<PatientRequest> update);
}

public sealed record AppointmentView(
    Guid Id,
    Guid ClinicId,
    Guid PatientId,
    string PatientName,
    string PatientPhone,
    Guid DoctorId,
    string DoctorName,
    string Specialty,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    decimal ConsultationPrice,
    decimal DepositAmount,
    decimal RemainingAmount,
    AppointmentStatus Status,
    DateTimeOffset? CheckedInAt,
    string CheckInCode,
    bool DepositForfeited);

public sealed record PatientRequestView(
    Guid Id,
    string Type,
    string Summary,
    string Description,
    RequestPriority Priority,
    string Channel,
    RequestStatus Status,
    DateTimeOffset CreatedAt,
    string? ResolutionNote);

public sealed record DashboardView(
    int AppointmentsToday,
    int WaitingPatients,
    int InConsultation,
    int PendingRequests,
    decimal RevenueToday,
    int NoShowsToday);

public sealed record AvailableSlot(DateTimeOffset StartAt, DateTimeOffset EndAt);

public sealed record CreateAppointmentCommand(
    string FirstName,
    string LastName,
    string Phone,
    string PreferredLanguage,
    Guid DoctorId,
    DateTimeOffset StartAt);

public sealed record CreateRequestCommand(
    string Type,
    string Summary,
    string Description,
    string Channel,
    bool IsUrgent = false);

public sealed record ChatCommand(string Message, string Language = "fr");

public interface IClinicService
{
    IReadOnlyList<Clinic> GetClinics();
    IReadOnlyList<Doctor> GetDoctors(Guid clinicId);
    DashboardView GetDashboard(Guid clinicId, DateTimeOffset now);
    IReadOnlyList<AppointmentView> GetAppointments(Guid clinicId);
    IReadOnlyList<AppointmentView> GetQueue(Guid clinicId);
    IReadOnlyList<PatientRequestView> GetRequests(Guid clinicId);
    IReadOnlyList<AvailableSlot> GetAvailableSlots(Guid clinicId, Guid doctorId, DateOnly date, DateTimeOffset now);
    AppointmentView CreateAppointment(Guid clinicId, CreateAppointmentCommand command, DateTimeOffset now);
    AppointmentView CheckIn(Guid clinicId, Guid appointmentId, string code, DateTimeOffset now);
    AppointmentView CheckInManually(Guid clinicId, Guid appointmentId, string reason, DateTimeOffset now);
    AppointmentView StartConsultation(Guid clinicId, Guid appointmentId, DateTimeOffset now);
    AppointmentView CompleteConsultation(Guid clinicId, Guid appointmentId, DateTimeOffset now);
    AppointmentView PayBalance(Guid clinicId, Guid appointmentId, DateTimeOffset now);
    PatientRequestView CreateRequest(Guid clinicId, CreateRequestCommand command, DateTimeOffset now);
    PatientRequestView UpdateRequest(Guid clinicId, Guid requestId, RequestStatus status, string? resolutionNote, DateTimeOffset now);
    int ProcessNoShows(DateTimeOffset now);
}
