namespace PasswordManager.Services;

/// <summary>Writing a decrypted file back to disk and small UI helpers shared by the file views.</summary>
public static class FileExport
{
    /// <summary>
    /// Asks for a target folder and writes the file there under its original name
    /// (a folder picker keeps dotfiles like ".env" intact). Returns the path, or null if cancelled.
    /// </summary>
    public static async Task<string?> SaveAsync(string fileName, byte[] content, Page? page)
    {
        string? folder = await PickFolderAsync();
        if (folder == null) return null;

        var target = Path.Combine(folder, fileName);
        if (File.Exists(target) && page != null)
        {
            bool overwrite = await page.DisplayAlertAsync(
                "Fayl mavjud",
                $"«{fileName}» bu papkada allaqachon bor. Ustiga yozilsinmi?",
                "Ustiga yozish", "Bekor qilish");
            if (!overwrite) return null;
        }

        await File.WriteAllBytesAsync(target, content);
        return target;
    }

    private static async Task<string?> PickFolderAsync()
    {
#if WINDOWS
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is Microsoft.UI.Xaml.Window window)
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
#else
        await Task.CompletedTask;
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#endif
    }

    /// <summary>The page hosting a view (for alerts raised from inside a ContentView).</summary>
    public static Page? PageOf(Element element)
    {
        Element? e = element;
        while (e != null && e is not Page) e = e.Parent;
        return e as Page;
    }
}
