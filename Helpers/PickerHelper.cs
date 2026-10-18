namespace MojiTodo.Helpers;

internal static class PickerHelper
{
    public static nint GetWindowHandle()
    {
        var window = App.MainWindowInstance;
        if (window is null) return 0;
        return WinRT.Interop.WindowNative.GetWindowHandle(window);
    }

    public static void InitPicker(object picker)
    {
        var hwnd = GetWindowHandle();
        if (hwnd != 0)
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }
}
