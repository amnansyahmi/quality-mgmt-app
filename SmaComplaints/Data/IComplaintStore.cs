using SmaComplaints.Models;
namespace SmaComplaints.Data;

public interface IComplaintStore
{
    Task<List<Complaint>> ListAsync(AppUser user);
    Task<Complaint?> GetAsync(Guid id);
    Task SaveAsync(Complaint complaint, int expectedRevision, AuditEntry entry, StoredAttachment? attachment = null);
    Task<StoredAttachment?> AttachmentAsync(Guid id);
    Task<List<AppUser>> UsersAsync();
    Task<AppUser?> UserAsync(Guid id);
    Task<AppUser?> UserByEmailAsync(string email);
    Task SaveUserAsync(AppUser user);
}
