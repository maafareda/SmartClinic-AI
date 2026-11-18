using System.Text.Json.Serialization;
using SmartClinic.API;
using SmartClinic.Application;
using SmartClinic.Domain;
using SmartClinic.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IClinicRepository, DemoClinicRepository>();
builder.Services.AddScoped<IClinicService, ClinicService>();
builder.Services.AddHostedService<NoShowBackgroundService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (KeyNotFoundException exception)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { error = exception.Message });
    }
    catch (ArgumentException exception)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { error = exception.Message });
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/patient", () => Results.Redirect("/patient.html"));
app.MapGet("/health", () => Results.Ok(new { status = "ok", mode = "demo" }));
app.MapGet("/api/clinics", (IClinicService service) => Results.Ok(service.GetClinics()));

var clinicApi = app.MapGroup("/api/clinics/{clinicId:guid}");
clinicApi.MapGet("/doctors", (Guid clinicId, IClinicService service) =>
    Results.Ok(service.GetDoctors(clinicId)));
clinicApi.MapGet("/dashboard", (Guid clinicId, IClinicService service) =>
    Results.Ok(service.GetDashboard(clinicId, DateTimeOffset.UtcNow)));
clinicApi.MapGet("/appointments", (Guid clinicId, IClinicService service) =>
    Results.Ok(service.GetAppointments(clinicId)));
clinicApi.MapGet("/appointments/{appointmentId:guid}", (Guid clinicId, Guid appointmentId, IClinicService service) =>
    Results.Ok(service.GetAppointments(clinicId).FirstOrDefault(item => item.Id == appointmentId)
               ?? throw new KeyNotFoundException("Rendez-vous introuvable dans ce cabinet.")));
clinicApi.MapGet("/queue/today", (Guid clinicId, IClinicService service) =>
    Results.Ok(service.GetQueue(clinicId)));
clinicApi.MapGet("/slots", (Guid clinicId, Guid doctorId, DateOnly date, IClinicService service) =>
    Results.Ok(service.GetAvailableSlots(clinicId, doctorId, date, DateTimeOffset.UtcNow)));
clinicApi.MapPost("/appointments", (
    Guid clinicId,
    CreateAppointmentCommand command,
    IClinicService service) =>
{
    var appointment = service.CreateAppointment(clinicId, command, DateTimeOffset.UtcNow);
    return Results.Created(
        $"/api/clinics/{clinicId}/appointments/{appointment.Appointment.Id}", appointment);
});
clinicApi.MapPost("/appointments/{appointmentId:guid}/check-in", (
    Guid clinicId,
    Guid appointmentId,
    CheckInCommand command,
    IClinicService service) =>
    Results.Ok(service.CheckIn(clinicId, appointmentId, command.Code, DateTimeOffset.UtcNow)));
clinicApi.MapPost("/appointments/{appointmentId:guid}/manual-check-in", (
    Guid clinicId,
    Guid appointmentId,
    ManualCheckInCommand command,
    IClinicService service) =>
    Results.Ok(service.CheckInManually(clinicId, appointmentId, command.Reason, DateTimeOffset.UtcNow)));
clinicApi.MapPost("/appointments/{appointmentId:guid}/start", (
    Guid clinicId,
    Guid appointmentId,
    IClinicService service) =>
    Results.Ok(service.StartConsultation(clinicId, appointmentId, DateTimeOffset.UtcNow)));
clinicApi.MapPost("/appointments/{appointmentId:guid}/complete", (
    Guid clinicId,
    Guid appointmentId,
    IClinicService service) =>
    Results.Ok(service.CompleteConsultation(clinicId, appointmentId, DateTimeOffset.UtcNow)));
clinicApi.MapPost("/appointments/{appointmentId:guid}/balance", (
    Guid clinicId,
    Guid appointmentId,
    IClinicService service) =>
    Results.Ok(service.PayBalance(clinicId, appointmentId, DateTimeOffset.UtcNow)));
clinicApi.MapGet("/requests", (Guid clinicId, IClinicService service) =>
    Results.Ok(service.GetRequests(clinicId)));
clinicApi.MapPost("/requests", (
    Guid clinicId,
    CreateRequestCommand command,
    IClinicService service) =>
    Results.Created($"/api/clinics/{clinicId}/requests",
        service.CreateRequest(clinicId, command, DateTimeOffset.UtcNow)));
clinicApi.MapPatch("/requests/{requestId:guid}", (
    Guid clinicId,
    Guid requestId,
    UpdateRequestCommand command,
    IClinicService service) =>
    Results.Ok(service.UpdateRequest(
        clinicId, requestId, command.Status, command.ResolutionNote, DateTimeOffset.UtcNow)));
clinicApi.MapPost("/ai/chat", (
    Guid clinicId,
    ChatCommand command,
    IClinicService service) => HandleChat(clinicId, command, service));

app.Run();

static IResult HandleChat(Guid clinicId, ChatCommand command, IClinicService service)
{
    if (string.IsNullOrWhiteSpace(command.Message))
    {
        throw new ArgumentException("Le message ne peut pas être vide.");
    }

    var text = command.Message.Trim();
    var normalized = text.ToLowerInvariant();
    var isUrgent = normalized.Contains("urgent") ||
                   normalized.Contains("urgence") ||
                   normalized.Contains("chest pain") ||
                   normalized.Contains("صعوبة التنفس") ||
                   normalized.Contains("ألم في الصدر");
    var needsReception = isUrgent ||
                         normalized.Contains("annul") ||
                         normalized.Contains("report") ||
                         normalized.Contains("décal") ||
                         normalized.Contains("decal") ||
                         normalized.Contains("humain") ||
                         normalized.Contains("reception") ||
                         normalized.Contains("موعد");

    if (needsReception)
    {
        var type = isUrgent ? "Urgent" : normalized.Contains("annul") ? "Cancel" : "Reschedule";
        var request = service.CreateRequest(clinicId, new CreateRequestCommand(
            type,
            isUrgent ? "Demande urgente reçue via l'assistant" : "Demande patient à traiter",
            text,
            "Web",
            isUrgent), DateTimeOffset.UtcNow);
        var reply = isUrgent
            ? command.Language == "ar"
                ? "إذا كانت هناك حالة طبية طارئة، اتصل فوراً بخدمات الطوارئ المحلية. أبلغنا فريق الاستقبال بطلبك."
                : "En cas d'urgence médicale, contactez immédiatement les services d'urgence locaux. J'ai prévenu l'accueil de votre demande."
            : command.Language == "ar"
                ? "تم تسجيل طلبك وسيقوم فريق الاستقبال بالرد عليك."
                : "Votre demande est enregistrée. L'équipe d'accueil va vous répondre.";
        return Results.Ok(new ChatReply(reply, request.Id, isUrgent ? "Urgent" : "HumanSupport"));
    }

    var answer = command.Language switch
    {
        "ar" => "يمكنني مساعدتك في معلومات العيادة والأسعار. لا أقدم تشخيصاً طبياً.",
        "en" => "I can help with clinic information and prices. I cannot provide a medical diagnosis.",
        _ => "Je peux vous renseigner sur le cabinet et les tarifs. Je ne fournis aucun diagnostic médical."
    };
    return Results.Ok(new ChatReply(answer, null, "GeneralInformation"));
}

public sealed record CheckInCommand(string Code);
public sealed record ManualCheckInCommand(string Reason);
public sealed record UpdateRequestCommand(RequestStatus Status, string? ResolutionNote);
public sealed record ChatReply(string Reply, Guid? RequestId, string Intent);

public partial class Program { }
