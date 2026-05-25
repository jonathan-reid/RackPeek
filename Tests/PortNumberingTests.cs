using RackPeek.Domain.Resources.SubResources;
using Shared.Rcl.Connections;

namespace Tests;

public class PortNumberingTests {
    [Theory]
    [MemberData(nameof(Cases))]
    public void Offset_SumsPrecedingGroupCounts(IReadOnlyList<Port>? ports, int groupIndex, int expected) =>
        Assert.Equal(expected, PortNumbering.Offset(ports, groupIndex));

    public static IEnumerable<object?[]> Cases() {
        yield return new object?[] { null, 1, 0 };                  // null ports → 0
        yield return new object?[] { Ports(), 1, 0 };               // empty ports → 0
        yield return new object?[] { Ports(3, 4), 0, 0 };           // first group → 0
        yield return new object?[] { Ports(12, 4), 1, 12 };         // second group offset = 12
        yield return new object?[] { Ports(3, 4, 2), 2, 7 };        // third group offset = 3 + 4
        yield return new object?[] { Ports(3, null, 2), 2, 3 };     // null Count counts as 0
        yield return new object?[] { Ports(3, 4), 5, 7 };           // out-of-range clamps to total
    }

    private static List<Port> Ports(params int?[] counts) =>
        counts.Select(c => new Port { Count = c }).ToList();
}
