namespace StudentConnect.Domain;

public enum ActivityRegistrationStatus
{
    Registered,
    Cancelled,

    // Added when an activity was at capacity at registration time; promoted to Registered
    // automatically when a Registered seat is freed by a cancellation (earliest-waitlisted-first).
    Waitlisted,
}
