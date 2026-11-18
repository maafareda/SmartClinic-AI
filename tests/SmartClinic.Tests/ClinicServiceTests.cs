using SmartClinic.Application;
using SmartClinic.Domain;
using SmartClinic.Infrastructure;

namespace SmartClinic.Tests;

public sealed class ClinicServiceTests
{
    [Fact]
    public async Task ConcurrentBookingsForSameSlot_OnlyOneAppointmentIsConfirmed()
    {
        var repository = new DemoClinicRepository();
        var service = new ClinicService(repository);
        var now = DateTimeOffset.UtcNow;
        var clinicId = DemoIds.ClinicAtlas;
        var doctorId = DemoIds.DoctorAtlas;
        var date = DateOnly.FromDateTime(now.AddDays(1).Date);
        var slot = service.GetAvailableSlots(clinicId, doctorId, date, now).First();

        var attempts = Enumerable.Range(0, 2).Select(index => Task.Run(() =>
        {
            try
            {
                service.CreateAppointment(clinicId, new CreateAppointmentCommand(
                    $"Patient{index}", "Test", $"+21260000000{index}", "fr", doctorId, slot.StartAt), now);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }));

        var results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result);
        Assert.Single(repository.GetAppointments(clinicId),
            item => item.StartAt == slot.StartAt && item.Status == AppointmentStatus.Confirmed);
    }

    [Fact]
    public void DoctorAndAppointmentQueries_AreScopedToSelectedClinic()
    {
        var service = new ClinicService(new DemoClinicRepository());

        Assert.Throws<KeyNotFoundException>(() => service.GetDoctors(Guid.NewGuid()));
        Assert.Throws<KeyNotFoundException>(() =>
            service.GetAvailableSlots(DemoIds.ClinicAtlas, DemoIds.DoctorRiad,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AppointmentActions_CannotBeAppliedAcrossClinicBoundary()
    {
        var service = new ClinicService(new DemoClinicRepository());
        var appointment = service.GetAppointments(DemoIds.ClinicAtlas).First();

        Assert.Throws<KeyNotFoundException>(() =>
            service.StartConsultation(DemoIds.ClinicRiad, appointment.Id, DateTimeOffset.UtcNow));
    }
}
