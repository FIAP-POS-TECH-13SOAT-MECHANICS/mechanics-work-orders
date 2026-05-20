using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace Mechanics.Infra.Observability;

public class DatadogTraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity == null) return;

        var traceId = activity.TraceId.ToString();
        var ddTraceId = Convert.ToUInt64(traceId[16..], 16).ToString();
        var ddSpanId = Convert.ToUInt64(activity.SpanId.ToString(), 16).ToString();

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("dd.trace_id", ddTraceId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("dd.span_id", ddSpanId));
    }
}
