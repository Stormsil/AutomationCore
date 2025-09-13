namespace AutomationCore.Core
{
    /// <summary>
    /// Типизированная обертка для handle окна
    /// </summary>
    public readonly struct WindowHandle : IEquatable<WindowHandle>
    {
        private readonly IntPtr _handle;

        public WindowHandle(IntPtr handle) => _handle = handle;

        public static WindowHandle Invalid => new(IntPtr.Zero);
        public bool IsValid => _handle != IntPtr.Zero;

        public static implicit operator IntPtr(WindowHandle handle) => handle._handle;
        public static implicit operator WindowHandle(IntPtr handle) => new(handle);

        public bool Equals(WindowHandle other) => _handle == other._handle;
        public override bool Equals(object obj) => obj is WindowHandle h && Equals(h);
        public override int GetHashCode() => _handle.GetHashCode();
        public override string ToString() => $"Window(0x{_handle:X})";

        public static bool operator ==(WindowHandle left, WindowHandle right) => left.Equals(right);
        public static bool operator !=(WindowHandle left, WindowHandle right) => !left.Equals(right);
    }
}