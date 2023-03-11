namespace RateLimiterSample.DataAccessLayer.Entities;

public class Subscription
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public int PermitLimit { get; set; }

    public int WindowLimitMinutes { get; set; }

    public virtual ICollection<Account> Accounts { get; } = new List<Account>();
}
