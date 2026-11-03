using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace KfuPet_Tool.Helpers
{
    /// <summary>
    /// 让原生窗口标题栏跟随应用的深色主题。
    /// </summary>
    internal static class DarkTitleBar
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeLegacy = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public static void Apply(Window window)
        {
            if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
            {
                ApplyCore(window);
            }
            else
            {
                window.SourceInitialized += (_, _) => ApplyCore(window);
            }
        }

        private static void ApplyCore(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            int enabled = 1;
            // Windows 10 20H1 / Windows 11 使用属性 20，旧版本回退到 19。
            if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
            }
        }
    }
}
