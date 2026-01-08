using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using SharedClientSide.Helpers;
using System;
using System.IO;

namespace DownloadHDAvalonia.Models.Company
{
    public partial class OptionsModel : ObservableObject
    {
        [ObservableProperty]
        private string _defaultPathToDownloadProfessionalTaskFiles;

        [ObservableProperty]
        private string _appTheme;

        private static string AppConfigFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Separacao", "app");

        private static string SettingsFilePath =>
            Path.Combine(AppConfigFolder, "settings.json");

        public void Save()
        {
            var json = JsonConvert.SerializeObject(this);
            if (!Directory.Exists(AppConfigFolder))
                Directory.CreateDirectory(AppConfigFolder);
            File.WriteAllText(SettingsFilePath, json);
        }

        public static OptionsModel Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonConvert.DeserializeObject<OptionsModel>(json);
            }

            var om = new OptionsModel
            {
                DefaultPathToDownloadProfessionalTaskFiles = Constants.SeparationFolder.FullName
            };
            om.Save();
            return om;
        }

        public OptionsModel()
        {
        }
    }
}




