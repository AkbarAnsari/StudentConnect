using StudentConnect.Api.Data.Stores;

namespace StudentConnect.Api.Notifications;

// Resolves "who should be notified about this student" — the student's own account (if
// they have one) plus every linked parent. Centralized here because Attendance/Marks/
// Homework all need the exact same recipient logic when something happens to a student.
public class NotificationRecipientResolver(UserStore users, ParentStore parents)
{
    public async Task<List<int>> ForStudentAsync(int studentId)
    {
        var studentUserId = await users.GetUserIdForStudentAsync(studentId);
        var parentUserIds = await parents.GetParentUserIdsForStudentAsync(studentId);

        var recipientIds = new List<int>(parentUserIds);
        if (studentUserId is not null)
        {
            recipientIds.Add(studentUserId.Value);
        }

        return recipientIds.Distinct().ToList();
    }

    public async Task<List<int>> ForStudentsAsync(IReadOnlyCollection<int> studentIds)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        var studentUserIds = await users.GetStudentUserIdsAsync(studentIds);
        var parentUserIds = await parents.GetParentUserIdsForStudentsAsync(studentIds);

        return studentUserIds.Concat(parentUserIds).Distinct().ToList();
    }
}
