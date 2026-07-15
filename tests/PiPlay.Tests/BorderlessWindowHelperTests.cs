using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class BorderlessWindowHelperTests
{
    [Fact]
    public void Native_move_is_queued_as_an_asynchronous_system_command()
    {
        var expectedHwnd = new IntPtr(42);
        IntPtr actualHwnd = IntPtr.Zero;
        var actualMessage = 0;
        IntPtr actualCommand = IntPtr.Zero;
        IntPtr actualPoint = IntPtr.Zero;

        var queued = BorderlessWindowHelper.QueueWindowMoveForTests(
            expectedHwnd,
            -1920,
            340,
            (hwnd, message, command, point) =>
            {
                actualHwnd = hwnd;
                actualMessage = message;
                actualCommand = command;
                actualPoint = point;
                return true;
            });

        Assert.True(queued);
        Assert.Equal(expectedHwnd, actualHwnd);
        Assert.Equal(0x0112, actualMessage); // WM_SYSCOMMAND
        Assert.Equal(0xF012, actualCommand.ToInt32()); // SC_MOVE | HTCAPTION
        Assert.Equal(BorderlessWindowHelper.PackScreenPointForTests(-1920, 340), actualPoint);
    }

    [Fact]
    public void Native_move_does_not_post_without_a_window_handle()
    {
        var posts = 0;

        Assert.False(BorderlessWindowHelper.QueueWindowMoveForTests(
            IntPtr.Zero, 0, 0, (_, _, _, _) => { posts++; return true; }));
        Assert.Equal(0, posts);
    }

    [Theory]
    [InlineData(120, 340)]
    [InlineData(-1920, -140)]
    [InlineData(32767, -32768)]
    public void Native_move_point_preserves_signed_multi_monitor_coordinates(int x, int y)
    {
        var packed = unchecked((int)BorderlessWindowHelper.PackScreenPointForTests(x, y).ToInt64());

        Assert.Equal(unchecked((short)x), unchecked((short)(packed & 0xFFFF)));
        Assert.Equal(unchecked((short)y), unchecked((short)((packed >> 16) & 0xFFFF)));
    }

    [Fact]
    public void Native_move_size_lifecycle_reports_only_real_state_transitions()
    {
        var transitions = new List<bool>();
        var inMoveSizeLoop = false;

        inMoveSizeLoop = BorderlessWindowHelper.ProcessMoveSizeMessageForTests(
            inMoveSizeLoop, 0x0231, transitions.Add); // WM_ENTERSIZEMOVE
        inMoveSizeLoop = BorderlessWindowHelper.ProcessMoveSizeMessageForTests(
            inMoveSizeLoop, 0x0231, transitions.Add); // duplicate enter
        inMoveSizeLoop = BorderlessWindowHelper.ProcessMoveSizeMessageForTests(
            inMoveSizeLoop, 0x0232, transitions.Add); // WM_EXITSIZEMOVE
        inMoveSizeLoop = BorderlessWindowHelper.ProcessMoveSizeMessageForTests(
            inMoveSizeLoop, 0x0082, transitions.Add); // WM_NCDESTROY after exit

        Assert.False(inMoveSizeLoop);
        Assert.Equal([true, false], transitions);
    }

    [Fact]
    public void Native_move_size_lifecycle_contains_callback_failures()
    {
        var exception = Record.Exception(() =>
            BorderlessWindowHelper.ProcessMoveSizeMessageForTests(
                inMoveSizeLoop: false,
                message: 0x0231,
                _ => throw new InvalidOperationException("test callback failure")));

        Assert.Null(exception);
    }

    [Fact]
    public void Native_destroy_ends_an_active_move_size_lifecycle()
    {
        var transitions = new List<bool>();
        var active = BorderlessWindowHelper.ProcessMoveSizeMessageForTests(
            inMoveSizeLoop: false, message: 0x0231, transitions.Add);

        active = BorderlessWindowHelper.ProcessMoveSizeMessageForTests(
            active, message: 0x0082, transitions.Add);

        Assert.False(active);
        Assert.Equal([true, false], transitions);
    }
}
