using RackPeek.Domain.Resources.SubResources;

namespace Shared.Rcl.Connections;

public static class PortNumbering {
    public static int Offset(IReadOnlyList<Port>? ports, int groupIndex) {
        if (ports is null || groupIndex <= 0)
            return 0;

        var offset = 0;
        var limit = Math.Min(groupIndex, ports.Count);

        for (var g = 0; g < limit; g++)
            offset += ports[g].Count ?? 0;

        return offset;
    }
}
