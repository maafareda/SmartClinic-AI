using SmartClinic.Domain;

namespace SmartClinic.Tests;

public sealed class AppointmentRulesTests
{
    private static readonly Guid ClinicId = Guid.NewGuid();
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid DoctorId = Guid.NewGuid();
    private static readonly DateTimeOffset StartAt = new(2026, 11, 19, 10, 0, 0, TimeSpan.FromHours(1));

    [Fact]
    public void CheckIn_AcceptsPatientInsideWindow_AndAddsPatientToQueue()
    {
        var appointment = CreateConfirmedAppointment();

        appointment.CheckIn(StartAt.AddMinutes(-15), "123456", 15, 5);

        Assert.Equal(AppointmentStatus.Waiting, appointment.Status);
        Assert.Equal(StartAt.AddMinutes(-15), appointment.CheckedInAt);
        Assert.Collection(appointment.History,
            transition => Assert.Equal(AppointmentStatus.PendingPayment, transition.From),
            transition => Assert.Equal(AppointmentStatus.CheckedIn, transition.To),
            transition => Assert.Equal(AppointmentStatus.Waiting, transition.To));
    }

    [Fact]
    public void CheckIn_RejectsWrongCodeAndOutOfWindowArrival()
    {
        var wrongCode = CreateConfirmedAppointment();
        Assert.Throws<InvalidOperationException>(() =>
            wrongCode.CheckIn(StartAt, "000000", 15, 5));

        var tooEarly = CreateConfirmedAppointment();
        Assert.Throws<InvalidOperationException>(() =>
            tooEarly.CheckIn(StartAt.AddMinutes(-16), "123456", 15, 5));

        var tooLate = CreateConfirmedAppointment();
        Assert.Throws<InvalidOperationException>(() =>
            tooLate.CheckIn(StartAt.AddMinutes(6), "123456", 15, 5));

        Assert.Equal(AppointmentStatus.Confirmed, wrongCode.Status);
    }

    [Fact]
    public void NoShow_IsMarkedOnlyAfterTolerance_AndForfeitsDepositOnce()
    {
        var appointment = CreateConfirmedAppointment();

        Assert.False(appointment.TryMarkNoShow(StartAt.AddMinutes(5), 5));
        Assert.False(appointment.DepositForfeited);

        Assert.True(appointment.TryMarkNoShow(StartAt.AddMinutes(6), 5));
        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
        Assert.True(appointment.DepositForfeited);
        Assert.Equal(1, appointment.Transactions.Count(transaction => transaction.Type == "Forfeit"));
        Assert.False(appointment.TryMarkNoShow(StartAt.AddMinutes(7), 5));
        Assert.Equal(1, appointment.Transactions.Count(transaction => transaction.Type == "Forfeit"));
    }

    [Fact]
    public void BalancePayment_RequiresExactRemainder_AndSettlesOnce()
    {
        var appointment = CreateConfirmedAppointment();
        appointment.CheckIn(StartAt.AddMinutes(-5), "123456", 15, 5);
        appointment.StartConsultation(StartAt);
        appointment.CompleteConsultation(StartAt.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(() =>
            appointment.PayBalance(StartAt.AddMinutes(11), 149m, "Cash"));
        appointment.PayBalance(StartAt.AddMinutes(11), 150m, "Cash");

        Assert.Equal(AppointmentStatus.Settled, appointment.Status);
        Assert.Equal(0m, appointment.RemainingAmount);
        Assert.Equal(2, appointment.Transactions.Count);
        Assert.Throws<InvalidOperationException>(() =>
            appointment.PayBalance(StartAt.AddMinutes(12), 0m, "Cash"));
    }

    private static Appointment CreateConfirmedAppointment()
    {
        var appointment = new Appointment(
            Guid.NewGuid(), ClinicId, PatientId, DoctorId, StartAt, 10, 200m, 50m, "123456");
        appointment.ConfirmDeposit(StartAt.AddMinutes(-20), "DEMO-REFERENCE");
        return appointment;
    }
}
