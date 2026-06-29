using System.Text.Json.Serialization;

namespace dental_backend.Dentally;

/// <summary>Raw page payload returned by GET /appointments.</summary>
public sealed class DentallyAppointmentsPage
{
    [JsonPropertyName("appointments")]
    public List<DentallyAppointment> Appointments { get; set; } = new();

    [JsonPropertyName("meta")]
    public DentallyMeta? Meta { get; set; }
}

public sealed class DentallyMeta
{
    /// <summary>Total number of records matching the query (across all pages).</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>The current page number.</summary>
    [JsonPropertyName("page")]
    public int Page { get; set; }
}

/// <summary>
/// Subset of the Dentally appointment resource we consume. Names are resolved from the
/// optional embedded patient/practitioner fields with a graceful fallback (see README).
/// </summary>
public sealed class DentallyAppointment
{
    [JsonPropertyName("id")]
    public JsonId Id { get; set; }

    [JsonPropertyName("start_time")]
    public DateTimeOffset? StartTime { get; set; }

    [JsonPropertyName("finish_time")]
    public DateTimeOffset? FinishTime { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("patient_id")]
    public JsonId PatientId { get; set; }

    [JsonPropertyName("patient_name")]
    public string? PatientName { get; set; }

    [JsonPropertyName("practitioner_id")]
    public JsonId PractitionerId { get; set; }

    [JsonPropertyName("practitioner_name")]
    public string? PractitionerName { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("cancelled_at")]
    public DateTimeOffset? CancelledAt { get; set; }
}

/// <summary>Envelope for GET /appointments/{id}.</summary>
public sealed class DentallySingleAppointment
{
    [JsonPropertyName("appointment")]
    public DentallyAppointment? Appointment { get; set; }
}

/// <summary>Envelope for GET /patients/{id}.</summary>
public sealed class DentallyPatientResponse
{
    [JsonPropertyName("patient")]
    public DentallyPatient? Patient { get; set; }
}

public sealed class DentallyPatient
{
    [JsonPropertyName("id")]
    public JsonId Id { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("email_address")]
    public string? EmailAddress { get; set; }

    [JsonPropertyName("mobile_phone")]
    public string? MobilePhone { get; set; }

    [JsonPropertyName("home_phone")]
    public string? HomePhone { get; set; }

    [JsonPropertyName("date_of_birth")]
    public string? DateOfBirth { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
