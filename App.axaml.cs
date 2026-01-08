using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using SharedClientSide_AVALONIA.Helpers;
using DownloadHDAvalonia.Models.Company;
using CodingSeb.Localization;
using CodingSeb.Localization.Loaders;
using SharedClientSide.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DownloadHDAvalonia
{
    public partial class App : Application
    {
        public enum FluentAvaloniaThemeMode
        {
            Default,
            Light,
            Dark
        }
        private FluentAvaloniaTheme? _fluentTheme;
        
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            foreach (var style in Styles)
            {
                if (style is FluentAvaloniaTheme faTheme)
                {
                    _fluentTheme = faTheme;
                    break;
                }
            }

            // Configura os carregadores de tradução (necessário antes de qualquer coisa que use Loc)
            InitializeLocalization();

            // Configura os delegates para override de endpoint usando o helper compartilhado
            EndpointConfigHelper.ConfigureLesserFunctionClientEndpoints();

            RegisterGlobalErrorHandlers();
        }

        private void InitializeLocalization()
        {
            LocalizationLoader.Instance.FileLanguageLoaders.Add(new JsonFileLoader());
            string basePath = AppContext.BaseDirectory;

            DirectoryInfo directory = new DirectoryInfo(Path.Combine(basePath, "Resources", "Translations"));
            if (directory.Exists)
            {
                foreach (FileInfo translationFile in directory.GetFiles("*.loc.json"))
                {
                    string translationFilePath = Path.Combine(basePath, "Resources", "Translations", translationFile.Name);
                    LocalizationLoader.Instance.AddFile(translationFilePath);
                }
            }

            // Define o idioma padrão baseado no idioma do sistema
            string targetLang = LanguageHelper.GetComputerLanguage();
            if (string.IsNullOrEmpty(targetLang) || !LanguageHelper.SupportedLanguages.Contains(targetLang))
            {
                targetLang = System.Globalization.CultureInfo.CurrentCulture.Name;
                if (!LanguageHelper.SupportedLanguages.Contains(targetLang))
                {
                    targetLang = "en-US";
                }
            }
            Loc.Instance.CurrentLanguage = targetLang;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new Views.MainWindow();
                
                // Carregar tema do OptionsModel
                var options = OptionsModel.Load();
                if (!string.IsNullOrEmpty(options.AppTheme))
                {
                    if (options.AppTheme == "DarkMode")
                    {
                        ChangeThemeRequested(FluentAvaloniaThemeMode.Dark);
                    }
                    else
                    {
                        ChangeThemeRequested(FluentAvaloniaThemeMode.Light);
                    }
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        public void ChangeThemeRequested(FluentAvaloniaThemeMode mode)
        {
            Application.Current!.RequestedThemeVariant = mode switch
            {
                FluentAvaloniaThemeMode.Dark => ThemeVariant.Dark,
                FluentAvaloniaThemeMode.Light => ThemeVariant.Light,
                _ => ThemeVariant.Default
            };
        }

        private void RegisterGlobalErrorHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    if (e.ExceptionObject is Exception ex)
                    {
                        SaveLogError(ex.Message, ex.StackTrace ?? "", ex.InnerException?.ToString() ?? "Sem InnerException");
                    }
                    else
                    {
                        SaveLogError("Erro crítico desconhecido", "Sem StackTrace", e.ExceptionObject?.ToString() ?? "Sem detalhes");
                    }
                }
                catch (Exception logEx)
                {
                    Console.WriteLine("Falha ao registrar log: " + logEx.Message);
                }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try
                {
                    SaveLogError(e.Exception.Message, e.Exception.StackTrace ?? "", e.Exception.InnerException?.ToString() ?? "Sem InnerException");
                }
                catch (Exception logEx)
                {
                    Console.WriteLine("Falha ao registrar log: " + logEx.Message);
                }

                e.SetObserved(); // evita que o processo caia fora
            };
        }

        public static void SaveLogError(string message, string stacktrace, string innerException)
        {
            try
            {
                string assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name ?? "";
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string logDirectory = Path.Combine(documentsPath, "Separacao", "apps", assemblyName);
                string logFilePath = Path.Combine(logDirectory, "ERROR_LOG.txt");

                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);

                string logMessage = $"Data/Hora: {DateTime.Now}\n" +
                                    $"Mensagem de erro: {message}\n" +
                                    $"StackTrace: {stacktrace}\n" +
                                    $"InnerException: {innerException}\n" +
                                    $"-------------------------------------------\n";

                File.AppendAllText(logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao salvar log: " + ex.Message);
            }
        }
    }
}

