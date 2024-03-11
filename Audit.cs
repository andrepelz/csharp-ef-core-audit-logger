using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Auditing;

public class Audit
{
    public Guid UserId { get; set; } = Guid.Empty;
    public State AuditState { get; private set; }
    public object? OldValue { get; set; } = null;
    public object? NewValue { get; set; } = null;

    private Audit() { }

    public static Audit EntryAdded(PropertyEntry property)
    {
        var builder = new AuditBuilder(
            new Audit()
            {
                AuditState = State.Added
            });

        return builder
            .CreatedBy(
                null == null // CreatedBy property is not implemented
                ? Guid.NewGuid()
                : Guid.Empty)
            .WithNewValue(property.CurrentValue);
    }

    public static Audit EntryDeleted(PropertyEntry property)
    {
        var builder = new AuditBuilder(
            new Audit()
            {
                AuditState = State.Deleted
            });

        return builder
            .CreatedBy(
                null == null // CreatedBy property is not implemented
                ? Guid.NewGuid()
                : Guid.Empty)
            .WithNewValue(property.CurrentValue);
    }

    public static Audit EntryModified(PropertyEntry property)
    {
        var builder = new AuditBuilder(
            new Audit()
            {
                AuditState = State.Modified
            });

        return builder
            .LastModifiedBy(
                null == null // LastModifiedBy property is not implemented
                ? Guid.NewGuid()
                : Guid.Empty)
            .WithNewValue(property.CurrentValue)
            .WithOldValue(property.OriginalValue);
    }

    public enum State { Added, Deleted, Modified }

    public class AuditBuilder(Audit audit)
    {
        private Audit _result { get; set; } = audit;

        public AuditBuilder CreatedBy(Guid userId)
        {
            _result.UserId = userId;
            return this;
        }

        public AuditBuilder LastModifiedBy(Guid userId)
        {
            _result.UserId = userId;
            return this;
        }

        public AuditBuilder WithNewValue(object? newValue)
        {
            _result.NewValue = newValue;
            return this;
        }

        public AuditBuilder WithOldValue(object? oldValue)
        {
            _result.OldValue = oldValue;
            return this;
        }

        public static implicit operator Audit(AuditBuilder builder)
            => builder._result;
    }
}