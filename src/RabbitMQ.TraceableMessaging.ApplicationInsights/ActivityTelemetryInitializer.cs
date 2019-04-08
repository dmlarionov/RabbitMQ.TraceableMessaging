using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights
{
    /// <summary>
    /// Initialize telemetry context (if not initialized) using system diagnostic activity.
    /// This is useful for binding of exceptions during RPC call to RPC call context.
    /// </summary>
    public class ActivityTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            var operation = telemetry.Context.Operation;

            // if telemetry item (e.g. exception) is not related to operation
            if (operation.Id == null && operation.ParentId == null)
            {
                // try linking it using diagnostic activity
                var activity = Activity.Current;
                if (activity?.RootId != null)
                    operation.Id = activity.RootId;

                if (activity?.ParentId != null)
                    operation.ParentId = activity.ParentId;

                // set operation name also
                if (operation.Name == null && activity?.OperationName != null)
                    operation.Name = activity.OperationName;
            }
        }
    }
}
