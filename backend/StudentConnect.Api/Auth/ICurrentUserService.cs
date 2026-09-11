namespace StudentConnect.Api.Auth;

// Reads identity from the AUTHENTICATED request (JWT claims) — never from a client-supplied
// field — so controllers can answer "is the caller allowed to see this student's data?"
// without trusting anything the browser says about itself.
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    int? UserId { get; }
    string? Username { get; }
    string? Role { get; }
    int? StudentId { get; }
    int? TeacherId { get; }

    bool IsTeacherOrAdmin { get; }

    // True for Teacher/Admin (they can access any student's data), or for a Student
    // whose own StudentId claim matches the requested id.
    bool CanAccessStudent(int studentId);

    // True for Admin (can act as any teacher), or for a Teacher whose own TeacherId
    // claim matches the requested id.
    bool CanActAsTeacher(int teacherId);
}
