# Phase 10 — Identity & Access

## 1. Why identity is needed

Every earlier phase used a "development student ID" passed around in URLs
(`/api/marks/student/5`) and hard-coded demo teacher/admin IDs. That was fine while the
goal was learning application architecture — but StudentConnect now holds real-shaped
data: marks, attendance, homework submissions, learning materials, activity
registrations. Nothing was stopping a browser from simply changing `5` to `6` in a URL
and reading a different student's marks. The application needs to know **who is
asking**, and needs to enforce that on the server — not trust whatever the client claims.

## 2. Authentication

**Simple definition:** proving who you are. A student enters a username and password;
the system verifies them and establishes an authenticated identity.

## 3. Authorization

**Simple definition:** deciding what an authenticated identity is allowed to do. Knowing
"this is Student A" doesn't automatically mean Student A can create homework or view
Student B's marks.

## 4. Authentication vs. Authorization

```
Authentication = Who are you?
Authorization  = What can you do?
```

A school analogy: showing your school ID at the gate is authentication. The office
deciding which rooms your ID lets you into is authorization.

## 5. Users vs. Students

A **Student** is a business/domain record — grade, section, email, attendance, marks.
A **User** (`ApplicationUser`) is a *different* concept: an account that can log in,
with a username, a password hash, and a role. This phase deliberately keeps them
separate:

```
ApplicationUser
      │
      ├── StudentId (nullable) → Student
      └── TeacherId (nullable) → Teacher
```

A student-role user has `StudentId` set and `TeacherId` null; a teacher-role user is the
reverse; an admin has both null. `Student`/`Teacher` did **not** become subclasses of
`ApplicationUser` — that would tie the business domain model to the identity model for
no real benefit at this stage.

## 6. Roles

Three roles: `Student`, `Teacher`, `Admin`.

| Action | Student | Teacher | Admin |
|---|---|---|---|
| View own profile | ✓ | ✓ | ✓ |
| View own attendance/marks/homework | ✓ | — | ✓ |
| Register for an activity (self) | ✓ | — | — |
| Record attendance, enter marks | — | ✓ | ✓ |
| Create homework, manage learning materials | — | ✓ | ✓ |
| Create/manage activities, view registrations | — | ✓ | ✓ |
| Manage students (create/edit/delete) | — | — | ✓ |

## 7. Claims

A **claim** is one piece of information about the authenticated user, carried inside the
access token. This phase's tokens carry:

```
UserId (NameIdentifier)
Username (Name)
Role
StudentId   (only for Student-role users)
TeacherId   (only for Teacher-role users)
```

## 8. Tokens

An access token lets the API recognize the same authenticated identity across separate
HTTP requests, without the server needing to remember anything itself (a **JWT**, signed
by the server, is self-contained and can be verified without a database lookup). The
token is issued once at login and sent back on every subsequent request:

```
Login → Token → Request + token → API validates token → API knows the caller's identity
```

This project uses standard ASP.NET Core JWT bearer authentication
(`Microsoft.AspNetCore.Authentication.JwtBearer`) — no custom cryptography, no custom
token format.

## 9. Protected APIs

Every endpoint requires authentication by default (`FallbackPolicy` in `Program.cs`),
except `POST /api/auth/login` (`[AllowAnonymous]`). Role restrictions are then layered
on top with the framework's standard attribute:

```csharp
[Authorize]                          // must be authenticated (the default already)
[Authorize(Roles = "Teacher,Admin")] // must be authenticated AND have one of these roles
```

## 10. 401 vs. 403

- **401 Unauthorized** — "Who are you?" No valid authenticated identity was presented at
  all (missing/invalid/expired token).
- **403 Forbidden** — "I know who you are, but you're not allowed to do this." The
  caller is authenticated, but their role or ownership doesn't permit the action.

Example: `GET /api/marks/student/27` with no token → `401`. The same request with a
valid **Student** token belonging to a *different* student → `403`.

## 11. Why the client cannot be trusted

Examples fixed in this phase:

- `POST /api/homework-submissions` used to accept `{ homeworkId, studentId, comments }`
  — a client could submit homework *as any student* just by changing `studentId`. It now
  accepts only `{ homeworkId, comments }`; the server reads the student from the
  authenticated token (`ICurrentUserService.StudentId`).
- `POST /api/activity-registrations` used to accept `{ studentId, activityId }` — same
  problem. It now accepts only `{ activityId }`.
- Nothing lets a client send `{ "role": "Admin" }` and be treated as an admin — role
  comes from the signed token, set once at login by the server, not from request data.

## 12. Student data ownership

`ICurrentUserService` (in `Auth/`) reads identity from the authenticated
`HttpContext.User` claims — never from a client-supplied field:

```csharp
bool CanAccessStudent(int studentId) => IsTeacherOrAdmin || StudentId == studentId;
```

Endpoints like `GET /api/attendance/student/{studentId}`, `GET /api/marks/student/{id}`,
`GET /api/homework/student/{id}`, `GET /api/learning-materials/student/{id}`, and
`GET /api/activity-registrations/student/{id}` all call this before doing anything else,
returning `403 Forbidden` if a Student's own `StudentId` claim doesn't match the
requested id. Teacher/Admin bypass this check, since they legitimately need to look up
any student.

## 13. Angular route guards

`authGuard` redirects an unauthenticated visitor to `/login` before a protected
component even loads; `roleGuard(['Teacher','Admin'])` redirects a Student away from
teacher/admin-only pages. **This is a user-experience improvement, not the security
boundary.** A guard runs entirely in the browser and could be bypassed by calling the
API directly — which is exactly why every one of these rules is enforced again, and
authoritatively, in the ASP.NET Core API.

## 14. Security architecture — full request flow

```
Browser
   │
   ▼
Angular Login
   │
   ▼
ASP.NET Core Authentication (validate username + PasswordHasher.VerifyHashedPassword)
   │
   ▼
Access Token (JWT, signed, contains UserId/Role/StudentId/TeacherId claims)
   │
   ▼
Angular (stored in localStorage; HTTP interceptor attaches it to every request)
   │
   │ Authorization: Bearer <token>
   ▼
ASP.NET Core API
   │
   ▼
Authentication Middleware (validates signature, issuer, audience, expiry)
   │
   ▼
Claims / Identity (available via ICurrentUserService)
   │
   ▼
Authorization ([Authorize(Roles=...)] + ownership checks in each controller)
   │
   ├── Allowed ──→ Controller action → EF Core → SQL Server
   │
   └── Denied ───→ 401 (not authenticated) / 403 (authenticated, not permitted)
```

## 15. Future Azure connection (conceptual only — not implemented)

```
Local authentication concepts (this phase)
        ↓
ASP.NET Core authentication (already using the real framework primitives)
        ↓
Microsoft Entra ID (a centralized, managed identity provider)
        ↓
Azure-hosted application
```

Nothing Entra ID-related, or Azure-related at all, is implemented in this phase — only
the underlying authentication/authorization *concepts* that a real identity provider
would eventually plug into.

## Password handling

- Passwords are **never** stored in plaintext — only a hash (`ApplicationUser.PasswordHash`),
  produced by ASP.NET Core's own `PasswordHasher<TUser>` (PBKDF2-based), not a custom
  algorithm.
- Passwords are **never** returned by any API response (`LoginResponseDto` has no
  password field at all).
- Login failures return one generic message ("Invalid username or password.") whether
  the username doesn't exist or the password is wrong — never revealing which.
- The demo passwords documented below are for local learning/testing only, are not real
  credentials, and are not meant to represent production password policy.

## Development accounts

| Username | Password | Role | Linked to |
|---|---|---|---|
| `student1` | `Student123!` | Student | Ananya Sharma (StudentId 1) |
| `student2` | `Student123!` | Student | Rahul Verma (StudentId 2) |
| `teacher1` | `Teacher123!` | Teacher | Priya Mehta (TeacherId 1) |
| `admin1` | `Admin123!` | Admin | — |

These exist only in the local development database, seeded via EF Core `HasData`. They
are not real credentials and must never be used outside local learning/testing.

## How to run

```bash
cd backend/StudentConnect.Api
dotnet run --launch-profile http   # applies pending migrations automatically
```
```bash
cd frontend
npm start
```
Visit `http://localhost:4200/login` and sign in with one of the accounts above.

## How to test

- Open the app without logging in → redirected to `/login`.
- Login as `student1` → dashboard shows "Welcome, Ananya Sharma! Role: Student" with
  only student links.
- `GET /api/marks/student/1` as `student1` → `200`. `GET /api/marks/student/2` as
  `student1` → `403`.
- `POST /api/homework` as `student1` → `403`. Same request as `teacher1` → `201`.
- `GET /api/students` (anonymous, no token) → `401`.
- `POST /api/activity-registrations` as `student1` with `{ "activityId": 4 }` → `201`,
  registered student is always the token's own student (never a client-supplied id).
- `POST /api/students` as `teacher1` (Admin-only) → `403`.
- Logout → token cleared, subsequent protected page loads redirect to `/login`.

## Student learning summary

> Authentication tells the application who I am. Authorization determines what I am
> allowed to do. The application uses roles and claims to make authorization decisions.
> The backend must enforce these rules because the browser cannot be trusted.

```
User
 ↓
Authentication
 ↓
Identity
 ↓
Claims / Role
 ↓
Authorization
 ↓
Allowed API operation
```

## Known limitations (intentionally deferred)

- Microsoft Entra ID / any production identity provider.
- Multi-factor authentication, password reset, email verification.
- Advanced/dynamic permissions management.
- Production-grade token lifecycle management (refresh tokens, revocation lists).
- Azure deployment of any kind.

## Next phase

**Azure & Cloud Introduction** — connect the concepts learned so far to the cloud and
begin understanding what Azure provides and why organizations use it. Not implemented
yet.
