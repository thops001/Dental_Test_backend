using dental_backend.Dentally;

namespace dental_backend.Tests;

public class AppointmentMapperTests
{
    [Fact]
    public void Computes_duration_from_start_and_finish_when_absent()
    {
        var src = new DentallyAppointment
        {
            Id = new JsonId("1"),
            StartTime = new DateTimeOffset(2026, 6, 24, 9, 0, 0, TimeSpan.Zero),
            FinishTime = new DateTimeOffset(2026, 6, 24, 9, 30, 0, TimeSpan.Zero)
        };

        Assert.Equal(30, AppointmentMapper.ToAppointment(src).Duration);
    }

    [Fact]
    public void Prefers_explicit_duration()
    {
        var src = new DentallyAppointment
        {
            Id = new JsonId("1"),
            StartTime = new DateTimeOffset(2026, 6, 24, 9, 0, 0, TimeSpan.Zero),
            FinishTime = new DateTimeOffset(2026, 6, 24, 9, 30, 0, TimeSpan.Zero),
            Duration = 45
        };

        Assert.Equal(45, AppointmentMapper.ToAppointment(src).Duration);
    }

    [Fact]
    public void Normalises_date_to_utc()
    {
        var src = new DentallyAppointment
        {
            Id = new JsonId("1"),
            StartTime = new DateTimeOffset(2026, 6, 24, 11, 0, 0, TimeSpan.FromHours(2))
        };

        var appt = AppointmentMapper.ToAppointment(src);
        Assert.Equal(TimeSpan.Zero, appt.Date.Offset);
        Assert.Equal(new DateTime(2026, 6, 24, 9, 0, 0, DateTimeKind.Utc), appt.Date.UtcDateTime);
    }

    [Fact]
    public void Falls_back_to_id_based_names()
    {
        var src = new DentallyAppointment
        {
            Id = new JsonId("1"),
            PatientId = new JsonId("88"),
            PractitionerId = new JsonId("12")
        };

        var appt = AppointmentMapper.ToAppointment(src);
        Assert.Equal("Patient #88", appt.PatientName);
        Assert.Equal("Practitioner #12", appt.PractitionerName);
    }

    [Fact]
    public void Uses_explicit_names_when_present()
    {
        var src = new DentallyAppointment
        {
            Id = new JsonId("1"),
            PatientName = "Jane Doe",
            PractitionerName = "Dr. Smith"
        };

        var appt = AppointmentMapper.ToAppointment(src);
        Assert.Equal("Jane Doe", appt.PatientName);
        Assert.Equal("Dr. Smith", appt.PractitionerName);
    }
}
