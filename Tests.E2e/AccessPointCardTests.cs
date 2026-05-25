using System.Globalization;
using Microsoft.Playwright;
using Tests.E2e.Infra;
using Tests.E2e.PageObjectModels;
using Xunit.Abstractions;

namespace Tests.E2e;

public class AccessPointCardTests(
    PlaywrightFixture fixture,
    ITestOutputHelper output) : E2ETestBase(fixture, output) {
    private readonly PlaywrightFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task User_Can_Edit_Model_And_Speed_And_Save() {
        (IBrowserContext context, IPage page) = await CreatePageAsync();
        var name = $"e2e-ap-{Guid.NewGuid():N}"[..16];

        try {
            await page.GotoAsync(_fixture.BaseUrl);

            var layout = new MainLayoutPom(page);
            await layout.AssertLoadedAsync();
            await layout.GotoHardwareAsync();

            var hardwareTree = new HardwareTreePom(page);
            await hardwareTree.AssertLoadedAsync();
            await hardwareTree.GotoAccessPointsListAsync();

            var list = new AccessPointsListPom(page);
            await list.AssertLoadedAsync();

            await list.AddAccessPointAsync(name);
            await page.WaitForURLAsync($"**/resources/hardware/{name}");

            var card = new AccessPointCardPom(page);
            await card.AssertCardVisibleAsync(name);

            var newModel = "AP-Model-9000";
            var newSpeed = 2.5;

            await card.BeginEditAsync(name);
            await card.SetModelAsync(name, newModel);
            await card.SetSpeedAsync(name, newSpeed);
            await card.SaveAsync(name);

            await page.ReloadAsync();

            await Assertions.Expect(card.ModelValue(name)).ToHaveTextAsync(newModel);
            await Assertions.Expect(card.SpeedValue(name))
                .ToHaveTextAsync($"{newSpeed.ToString(CultureInfo.InvariantCulture)} Gbps");

            await context.CloseAsync();
        }
        finally {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task User_Can_Cancel_Edit_And_Changes_Are_Not_Applied() {
        (IBrowserContext context, IPage page) = await CreatePageAsync();
        var name = $"e2e-ap-{Guid.NewGuid():N}"[..16];

        try {
            await page.GotoAsync(_fixture.BaseUrl);

            var layout = new MainLayoutPom(page);
            await layout.AssertLoadedAsync();
            await layout.GotoHardwareAsync();

            var hardwareTree = new HardwareTreePom(page);
            await hardwareTree.AssertLoadedAsync();
            await hardwareTree.GotoAccessPointsListAsync();

            var list = new AccessPointsListPom(page);
            await list.AssertLoadedAsync();

            await list.AddAccessPointAsync(name);
            await page.WaitForURLAsync($"**/resources/hardware/{name}");

            var card = new AccessPointCardPom(page);
            await card.AssertCardVisibleAsync(name);

            // Capture current values (may be empty depending on seed data)
            var beforeModel = await card.ModelSection(name).TextContentAsync();
            var beforeSpeed = await card.SpeedSection(name).TextContentAsync();

            await card.BeginEditAsync(name);
            await card.SetModelAsync(name, "SHOULD-NOT-SAVE");
            await card.SetSpeedAsync(name, 9.9);
            await card.CancelEditAsync(name);

            var afterModel = await card.ModelSection(name).TextContentAsync();
            var afterSpeed = await card.SpeedSection(name).TextContentAsync();

            await Assertions.Expect(card.ModelSection(name)).ToHaveTextAsync(beforeModel ?? "");
            await Assertions.Expect(card.SpeedSection(name)).ToHaveTextAsync(beforeSpeed ?? "");

            await context.CloseAsync();
        }
        finally {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task User_Can_Rename_AccessPoint_From_Card() {
        (IBrowserContext context, IPage page) = await CreatePageAsync();
        var name = $"e2e-ap-{Guid.NewGuid():N}"[..16];
        var newName = $"e2e-ap-{Guid.NewGuid():N}"[..16];

        try {
            await page.GotoAsync(_fixture.BaseUrl);

            var layout = new MainLayoutPom(page);
            await layout.AssertLoadedAsync();
            await layout.GotoHardwareAsync();

            var hardwareTree = new HardwareTreePom(page);
            await hardwareTree.AssertLoadedAsync();
            await hardwareTree.GotoAccessPointsListAsync();

            var list = new AccessPointsListPom(page);
            await list.AssertLoadedAsync();

            await list.AddAccessPointAsync(name);
            await page.WaitForURLAsync($"**/resources/hardware/{name}");

            var card = new AccessPointCardPom(page);
            await card.AssertCardVisibleAsync(name);

            await card.RenameAsync(name, newName);

            // After rename, the card test id uses the new name
            await card.AssertCardVisibleAsync(newName);

            await context.CloseAsync();
        }
        finally {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task User_Can_Clone_AccessPoint_From_Card() {
        (IBrowserContext context, IPage page) = await CreatePageAsync();
        var name = $"e2e-ap-{Guid.NewGuid():N}"[..16];
        var cloneName = $"e2e-ap-{Guid.NewGuid():N}"[..16];

        try {
            await page.GotoAsync(_fixture.BaseUrl);

            var layout = new MainLayoutPom(page);
            await layout.AssertLoadedAsync();
            await layout.GotoHardwareAsync();

            var hardwareTree = new HardwareTreePom(page);
            await hardwareTree.AssertLoadedAsync();
            await hardwareTree.GotoAccessPointsListAsync();

            var list = new AccessPointsListPom(page);
            await list.AssertLoadedAsync();

            await list.AddAccessPointAsync(name);
            await page.WaitForURLAsync($"**/resources/hardware/{name}");

            var card = new AccessPointCardPom(page);
            await card.AssertCardVisibleAsync(name);

            await card.CloneAsync(name, cloneName);

            // Clone navigates to the clone details page
            await card.AssertCardVisibleAsync(cloneName);

            // Cleanup: delete clone then original (both from details pages)
            await card.DeleteAsync(cloneName);
            await page.WaitForURLAsync("**/hardware/tree");

            await hardwareTree.GotoAccessPointsListAsync();
            await list.AssertLoadedAsync();
            await list.OpenAccessPointAsync(name);

            await card.AssertCardVisibleAsync(name);
            await card.DeleteAsync(name);

            await context.CloseAsync();
        }
        finally {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task User_Can_Delete_AccessPoint_From_Card_And_Is_Redirected() {
        (IBrowserContext context, IPage page) = await CreatePageAsync();
        var name = $"e2e-ap-{Guid.NewGuid():N}"[..16];

        try {
            await page.GotoAsync(_fixture.BaseUrl);

            var layout = new MainLayoutPom(page);
            await layout.AssertLoadedAsync();
            await layout.GotoHardwareAsync();

            var hardwareTree = new HardwareTreePom(page);
            await hardwareTree.AssertLoadedAsync();
            await hardwareTree.GotoAccessPointsListAsync();

            var list = new AccessPointsListPom(page);
            await list.AssertLoadedAsync();

            await list.AddAccessPointAsync(name);
            await page.WaitForURLAsync($"**/resources/hardware/{name}");

            var card = new AccessPointCardPom(page);
            await card.AssertCardVisibleAsync(name);

            await card.DeleteAsync(name);
            await page.WaitForURLAsync("**/hardware/tree");

            // Verify it’s gone from the list
            await hardwareTree.GotoAccessPointsListAsync();
            await list.AssertLoadedAsync();
            await list.AssertAccessPointDoesNotExist(name);

            await context.CloseAsync();
        }
        finally {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task User_Can_Add_And_Remove_Tags_From_AccessPoint_Card() {
        (IBrowserContext context, IPage page) = await CreatePageAsync();
        var name = $"e2e-ap-{Guid.NewGuid():N}"[..16];

        try {
            await page.GotoAsync(_fixture.BaseUrl);

            var layout = new MainLayoutPom(page);
            await layout.AssertLoadedAsync();
            await layout.GotoHardwareAsync();

            var hardwareTree = new HardwareTreePom(page);
            await hardwareTree.AssertLoadedAsync();
            await hardwareTree.GotoAccessPointsListAsync();

            var list = new AccessPointsListPom(page);
            await list.AssertLoadedAsync();

            await list.AddAccessPointAsync(name);
            await page.WaitForURLAsync($"**/resources/hardware/{name}");

            var card = new AccessPointCardPom(page);
            await card.AssertCardVisibleAsync(name);

            TagsPom tags = card.Tags;

            // -------------------------------------------------
            // Add multiple tags in one modal interaction
            // -------------------------------------------------

            await tags.AddTagsAsync("accesspoint", "Foo", "Bar", "Baz");

            await tags.AssertTagVisibleAsync("accesspoint", "Foo");
            await tags.AssertTagVisibleAsync("accesspoint", "Bar");
            await tags.AssertTagVisibleAsync("accesspoint", "Baz");

            // -------------------------------------------------
            // Remove a single tag
            // -------------------------------------------------

            await tags.RemoveTagAsync("accesspoint", "Bar");

            await tags.AssertTagNotVisibleAsync("accesspoint", "Bar");
            await tags.AssertTagVisibleAsync("accesspoint", "Foo");
            await tags.AssertTagVisibleAsync("accesspoint", "Baz");

            // -------------------------------------------------
            // Reload to verify persistence
            // -------------------------------------------------

            await page.ReloadAsync();

            await tags.AssertTagVisibleAsync("accesspoint", "Foo");
            await tags.AssertTagVisibleAsync("accesspoint", "Baz");
            await tags.AssertTagNotVisibleAsync("accesspoint", "Bar");

            await context.CloseAsync();
        }
        finally {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task User_Can_Add_Ports_To_Two_AccessPoints_And_Connect_Them() {
        (IBrowserContext context, IPage page) = await CreatePageAsync();

        var ap1 = $"e2e-ap-{Guid.NewGuid():N}"[..16];
        var ap2 = $"e2e-ap-{Guid.NewGuid():N}"[..16];

        try {
            await page.GotoAsync(_fixture.BaseUrl);

            var layout = new MainLayoutPom(page);
            await layout.AssertLoadedAsync();
            await layout.GotoHardwareAsync();

            var hardwareTree = new HardwareTreePom(page);
            await hardwareTree.AssertLoadedAsync();
            await hardwareTree.GotoAccessPointsListAsync();

            var list = new AccessPointsListPom(page);
            await list.AssertLoadedAsync();

            // -------------------------------------------------
            // Create first AP
            // -------------------------------------------------

            await list.AddAccessPointAsync(ap1);
            await page.WaitForURLAsync($"**/resources/hardware/{ap1}");

            var card = new AccessPointCardPom(page);
            await card.AssertCardVisibleAsync(ap1);

            // Add port group to AP1
            await card.AddPortGroupAsync(
                "rj45",
                "1",
                2);

            // -------------------------------------------------
            // Create second AP
            // -------------------------------------------------

            await layout.GotoHardwareAsync();
            await hardwareTree.AssertLoadedAsync();
            await hardwareTree.GotoAccessPointsListAsync();
            await list.AssertLoadedAsync();

            await list.AddAccessPointAsync(ap2);
            await page.WaitForURLAsync($"**/resources/hardware/{ap2}");

            await card.AssertCardVisibleAsync(ap2);

            // Add port group to AP2
            await card.AddPortGroupAsync(
                "sfp+",
                "2.5",
                2);
            // -------------------------------------------------
            // Go back to AP1 to create connection
            // -------------------------------------------------

            await layout.GotoHardwareAsync();
            await hardwareTree.GotoAccessPointsListAsync();
            await list.AssertLoadedAsync();
            await list.OpenAccessPointAsync(ap1);

            await card.AssertCardVisibleAsync(ap1);

            // -------------------------------------------------
            // Open connection modal from port
            // -------------------------------------------------

            await card.OpenConnectionFromPortAsync(0, 0);

            // -------------------------------------------------
            // Create connection
            // -------------------------------------------------

            await card.CreateConnectionAsync(
                ap1,
                "rj45 — 1 Gbps (2)", // example label — adjust if needed
                "Port 1",
                ap2,
                "sfp+ — 2.5 Gbps (2)",
                "Port 1");

            // -------------------------------------------------
            // Verify connection indicator appears
            // -------------------------------------------------

            await card.Ports.AssertPortVisibleAsync("accesspoint-ports", 0, 0);

            await context.CloseAsync();
        }
        finally {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Port_Numbers_Are_Continuous_Across_Groups() {
        (IBrowserContext context, IPage page) = await CreatePageAsync();

        var src = $"e2e-ap-{Guid.NewGuid():N}"[..16];
        var dst = $"e2e-ap-{Guid.NewGuid():N}"[..16];

        try {
            await page.GotoAsync(_fixture.BaseUrl);

            var layout = new MainLayoutPom(page);
            await layout.AssertLoadedAsync();
            await layout.GotoHardwareAsync();

            var hardwareTree = new HardwareTreePom(page);
            await hardwareTree.AssertLoadedAsync();
            await hardwareTree.GotoAccessPointsListAsync();

            var list = new AccessPointsListPom(page);
            await list.AssertLoadedAsync();

            var card = new AccessPointCardPom(page);

            // src AP: group 0 = 3 ports, group 1 = 2 ports
            await list.AddAccessPointAsync(src);
            await page.WaitForURLAsync($"**/resources/hardware/{src}");
            await card.AssertCardVisibleAsync(src);

            await card.AddPortGroupAsync("rj45", "1", 3);
            await card.AddPortGroupAsync("sfp+", "2.5", 2);

            // Group 1 (offset 3) ports must read 4 and 5, not 1 and 2.
            await card.Ports.AssertPortLabelAsync("accesspoint-ports", 1, 0, "4");
            await card.Ports.AssertPortLabelAsync("accesspoint-ports", 1, 1, "5");

            // dst AP: single group, 2 ports
            await layout.GotoHardwareAsync();
            await hardwareTree.GotoAccessPointsListAsync();
            await list.AssertLoadedAsync();
            await list.AddAccessPointAsync(dst);
            await page.WaitForURLAsync($"**/resources/hardware/{dst}");
            await card.AssertCardVisibleAsync(dst);
            await card.AddPortGroupAsync("rj45", "1", 2);

            // Back to src, open the connection modal from group 1 port 0
            await layout.GotoHardwareAsync();
            await hardwareTree.GotoAccessPointsListAsync();
            await list.AssertLoadedAsync();
            await list.OpenAccessPointAsync(src);
            await card.AssertCardVisibleAsync(src);

            await card.OpenConnectionFromPortAsync(1, 0);

            // The side-A port dropdown labels are continuous too (Port 4 / Port 5).
            await card.Ports.AssertPortAOptionAsync("accesspoint-ports", "Port 4");
            await card.Ports.AssertPortAOptionAsync("accesspoint-ports", "Port 5");

            // Connect src "Port 4" (group 1, physical index 0) to dst "Port 1".
            await card.CreateConnectionAsync(
                src,
                "sfp+ — 2.5 Gbps (2)",
                "Port 4",
                dst,
                "rj45 — 1 Gbps (2)",
                "Port 1");

            // dst side: destination offset is computed from src's groups, so dst's
            // connected port shows "src (port 4)".
            await layout.GotoHardwareAsync();
            await hardwareTree.GotoAccessPointsListAsync();
            await list.AssertLoadedAsync();
            await list.OpenAccessPointAsync(dst);
            await card.AssertCardVisibleAsync(dst);
            await card.Ports.AssertPortTooltipAsync("accesspoint-ports", 0, 0, $"{src} (port 4)");

            // Persisted-index gate: src group 1 port 0 is keyed on the physical index 0,
            // so it shows the connection ("dst (port 1)"). A corrupted dropdown value
            // would persist a wrong index and this port would read "Available".
            await layout.GotoHardwareAsync();
            await hardwareTree.GotoAccessPointsListAsync();
            await list.AssertLoadedAsync();
            await list.OpenAccessPointAsync(src);
            await card.AssertCardVisibleAsync(src);
            await card.Ports.AssertPortTooltipAsync("accesspoint-ports", 1, 0, $"{dst} (port 1)");
        }
        finally {
            await context.CloseAsync();
        }
    }
}
