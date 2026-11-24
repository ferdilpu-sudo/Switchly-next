using Switchly.Domain.Diagnostics;

namespace Switchly.Application.Diagnostics;

/// <summary>Reads live measured memory usage for Switchly Next and the host OS (REQ-018, TASK-034).</summary>
public interface IResourceMonitor
{
    ResourceSnapshot CaptureSnapshot(int savedProfileCount, int activeProfileCount);
}
