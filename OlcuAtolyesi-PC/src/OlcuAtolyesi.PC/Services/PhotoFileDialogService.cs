using System.IO;
using Microsoft.Win32;

namespace OlcuAtolyesi.PC.Services;

public interface IPhotoFileDialogService
{
    /// <summary>Kullanıcıdan bir fotoğraf dosyası seçmesini ister; iptal edilirse null döner.</summary>
    string? PickPhotoFile();

    /// <summary>Kullanıcıdan içe aktarılacak bir proje paketi (.zip / project.json) seçmesini ister.</summary>
    string? PickProjectPackageToImport() => null;

    /// <summary>Kullanıcıdan dışa aktarılacak .zip proje paketinin kayıt yolunu seçmesini ister.</summary>
    string? PickProjectPackageToExport(string suggestedFileName) => null;

    /// <summary>Kullanıcıdan DXF çıktı dosyası için yol seçmesini ister; iptal edilirse null döner.</summary>
    string? PickDxfExportPath(string suggestedFileName) => null;
}

public sealed class WpfPhotoFileDialogService : IPhotoFileDialogService
{
    public string? PickPhotoFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Fotoğraf Aç",
            Filter = "Görüntü dosyaları (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Tüm dosyalar (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickProjectPackageToImport()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Mobil / PC Proje Paketi İçe Aktar",
            Filter = "Ölçü Atölyesi Proje Paketleri (*.zip;*.olcupkg;project.json;*.json)|*.zip;*.olcupkg;project.json;*.json|Tüm dosyalar (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickProjectPackageToExport(string suggestedFileName)
    {
        var safeFileName = string.IsNullOrWhiteSpace(suggestedFileName)
            ? "OlcuAtolyesi_Proje.zip"
            : string.Concat(suggestedFileName.Trim().Split(Path.GetInvalidFileNameChars())) + ".zip";

        var dialog = new SaveFileDialog
        {
            Title = "Taşınabilir Proje Paketi Olarak Dışa Aktar",
            Filter = "Taşınabilir Proje Paketi (*.zip)|*.zip",
            DefaultExt = ".zip",
            AddExtension = true,
            FileName = safeFileName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickDxfExportPath(string suggestedFileName)
    {
        var safeName = string.IsNullOrWhiteSpace(suggestedFileName)
            ? "OlcuAtolyesi_Olcum.dxf"
            : string.Concat(suggestedFileName.Trim().Split(Path.GetInvalidFileNameChars()));

        if (!safeName.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
        {
            safeName += ".dxf";
        }

        var dialog = new SaveFileDialog
        {
            Title = "DXF Olarak Dışa Aktar (2B)",
            Filter = "DXF Çizimi (*.dxf)|*.dxf",
            DefaultExt = ".dxf",
            AddExtension = true,
            FileName = safeName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
