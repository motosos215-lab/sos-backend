using MotoSOS.API.Common.Exceptions;

namespace MotoSOS.API.Modules.AuditLogRetention.Application;

public sealed class AuditLogRetentionRunQueryValidator
{
    public AuditLogRetentionRunQuery Validate(int? pageNumber, int? pageSize)
    {
        int page = pageNumber ?? 1;
        int size = pageSize ?? 50;
        if (page < 1) throw new ValidationAppException("pageNumber must be greater than or equal to 1.");
        if (size < 1 || size > 100) throw new ValidationAppException("pageSize must be between 1 and 100.");
        return new AuditLogRetentionRunQuery(page, size);
    }
}
