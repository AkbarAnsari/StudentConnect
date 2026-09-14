namespace StudentConnect.Domain;

// A student's real enrollment lifecycle — never hard-deleted, only transitioned so historical
// attendance/marks/homework stay intact and queryable (see StudentsController).
public enum StudentStatus
{
    Active,
    Withdrawn,
    Graduated,
    Transferred,
}
