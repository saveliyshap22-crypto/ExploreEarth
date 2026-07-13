using Microsoft.Win32;

namespace EarthExplorer.App.Services;

public sealed class FilePickerService : IFilePickerService
{
    public string? PickImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите фотографию",
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tif;*.tiff|Все файлы|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
