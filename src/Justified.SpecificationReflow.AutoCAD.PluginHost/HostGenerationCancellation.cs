using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Justified.SpecificationReflow.AutoCAD.PluginHost;

// 计时器只读取前台窗口和 Escape 状态；后台线程不访问 AutoCAD 数据库或编辑器。
// 不泵送嵌套消息循环，避免生成期间执行另一条修改图纸的命令。
internal sealed class HostGenerationCancellation : IDisposable
{
    private readonly CancellationTokenSource _source = new CancellationTokenSource();
    private readonly Timer _timer;
    private readonly object _gate = new object();
    private bool _disposed;
    private readonly uint _processId = (uint)Process.GetCurrentProcess().Id;

    public HostGenerationCancellation()
    {
        _timer = new Timer(Poll, null, 0, 25);
    }

    public CancellationToken Token => _source.Token;

    private void Poll(object? state)
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundProcess);
        if (foregroundProcess != _processId || (GetAsyncKeyState(0x1B) & 0x8000) == 0) return;
        lock (_gate)
        {
            if (!_disposed) _source.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Dispose();
            _source.Dispose();
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
