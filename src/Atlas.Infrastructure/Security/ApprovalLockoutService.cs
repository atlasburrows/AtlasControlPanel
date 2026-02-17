using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Security;

public interface IApprovalLockoutService
{
    bool IsLockedOut { get; }
    void RecordSuspiciousApproval();
    void ManualUnlock();
    (int suspiciousCount, DateTime? lockoutUntil) GetStatus();
}

public class ApprovalLockoutService(ILogger<ApprovalLockoutService> logger) : IApprovalLockoutService
{
    private readonly object _lock = new();
    private readonly List<DateTime> _suspiciousApprovals = [];
    private DateTime? _lockoutUntil;

    private const int MaxSuspiciousPerHour = 3;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan WindowDuration = TimeSpan.FromHours(1);

    public bool IsLockedOut
    {
        get
        {
            lock (_lock)
            {
                if (_lockoutUntil is null) return false;
                if (DateTime.UtcNow >= _lockoutUntil)
                {
                    _lockoutUntil = null;
                    return false;
                }
                return true;
            }
        }
    }

    public void RecordSuspiciousApproval()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            _suspiciousApprovals.Add(now);

            // Prune old entries
            var cutoff = now - WindowDuration;
            _suspiciousApprovals.RemoveAll(t => t < cutoff);

            if (_suspiciousApprovals.Count > MaxSuspiciousPerHour)
            {
                _lockoutUntil = now + LockoutDuration;
                logger.LogCritical("APPROVAL LOCKOUT ACTIVATED: {Count} suspicious approvals in 1 hour. Locked until {Until:u}",
                    _suspiciousApprovals.Count, _lockoutUntil);
            }
        }
    }

    public void ManualUnlock()
    {
        lock (_lock)
        {
            _lockoutUntil = null;
            _suspiciousApprovals.Clear();
            logger.LogWarning("Approval lockout manually cleared");
        }
    }

    public (int suspiciousCount, DateTime? lockoutUntil) GetStatus()
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - WindowDuration;
            _suspiciousApprovals.RemoveAll(t => t < cutoff);
            return (_suspiciousApprovals.Count, _lockoutUntil);
        }
    }
}
