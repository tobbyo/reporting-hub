namespace ReportingHub.Api.Infrastructure;

public interface ICorrelationIdAccessor
{
    string CorrelationId { get; set; }
}
