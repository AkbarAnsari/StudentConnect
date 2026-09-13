namespace StudentConnect.Application;

// Resolves which School (tenant) the current authenticated request belongs to, from the
// "SchoolId" JWT claim — mirrors ICurrentUserService's claims-only design (never trusts
// anything the client supplies directly).
public interface ICurrentTenantService
{
    int? SchoolId { get; }
}
