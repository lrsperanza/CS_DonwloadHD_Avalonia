using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DownloadHDAvalonia.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json;
using SharedClientSide.Helpers;
using SharedClientSide.ServerInteraction;
using CodingSeb.Localization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DownloadHDAvalonia.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        #region Propriedades Observáveis

        [ObservableProperty]
        private string _classCode;

        partial void OnClassCodeChanged(string value)
        {
            _ = ClassCodeChangedAsync();
        }

        [ObservableProperty]
        private string _folderPath;

        partial void OnFolderPathChanged(string value)
        {
            // Remove aspas se houver
            if (!string.IsNullOrEmpty(value))
            {
                FolderPath = value.Replace("\"", "");
            }
            OnPropertyChanged(nameof(IsFolderPathValid));
            CheckStartDownloadAvailability();
        }

        [ObservableProperty]
        private string _pathFilter = "";

        partial void OnPathFilterChanged(string value)
        {
            CheckStartDownloadAvailability();
        }

        [ObservableProperty]
        private string _progressText = "";

        [ObservableProperty]
        private double _progressPercentage = 0;

        [ObservableProperty]
        private bool _isDownloading = false;

        [ObservableProperty]
        private bool _existsAutoTreatment = false;

        [ObservableProperty]
        private bool _isClassCodeValid = false;

        [ObservableProperty]
        private bool _isCheckingClassCode = false;

        [ObservableProperty]
        private string _statusClassCodeText = "";

        [ObservableProperty]
        private bool _isStatusClassCodeVisible = false;

        [ObservableProperty]
        private bool _isFolderPathEnabled = true;

        [ObservableProperty]
        private bool _isPathFilterEnabled = true;

        [ObservableProperty]
        private bool _isClassCodeEnabled = true;

        [ObservableProperty]
        private bool _isStartDownloadEnabled = false;

        [ObservableProperty]
        private ProfessionalTask? _professionalTask;

        [ObservableProperty]
        private DownloadHDPhotosInBatchService.DownloadAllPhotosResult? _downloadAllPhotosResult;

        partial void OnDownloadAllPhotosResultChanged(DownloadHDPhotosInBatchService.DownloadAllPhotosResult? value)
        {
            PopulateContainerErrors();
        }

        [ObservableProperty]
        private bool _isProgressContainerVisible = false;

        [ObservableProperty]
        private bool _isStatusContainerVisible = false;

        [ObservableProperty]
        private string _statusDownloadMessage = "";

        [ObservableProperty]
        private List<string> _errorPhotos = new List<string>();

        [ObservableProperty]
        private bool _preferAutoTreatment = false;

        [ObservableProperty]
        private bool _smartCompress = true;

        [ObservableProperty]
        private bool _closeAppAfterFinish = true;

        [ObservableProperty]
        private bool _isFinishAllProcess = false;

        [ObservableProperty]
        private string _labelFinishAllProcess = "";

        [ObservableProperty]
        private bool _isPreferAutoTreatmentEnabled = false;

        #endregion

        #region Propriedades Calculadas

        public bool IsFolderPathValid
        {
            get
            {
                if (string.IsNullOrEmpty(FolderPath))
                    return false;
                return Directory.Exists(FolderPath);
            }
        }

        public bool HasErrorPhotos
        {
            get => ErrorPhotos != null && ErrorPhotos.Count > 0;
        }

        #endregion

        #region Construtor

        public MainWindowViewModel()
        {
            // Inicializar autenticação
            LesserFunctionClient.DefaultClient.InitFromFile((string msg) =>
            {
                if (!string.IsNullOrEmpty(msg))
                {
                    _ = ShowLoginRequiredMessageAndOpenDashboard();
                    return;
                }
            });

            // Carregar ProfessionalTask do arquivo se existir
            if (File.Exists(AppInstaller.ClassToDownloadHDTxtFilePath))
            {
                try
                {
                    var json = File.ReadAllText(AppInstaller.ClassToDownloadHDTxtFilePath);
                    var pt = JsonConvert.DeserializeObject<ProfessionalTask>(json);
                    if (pt != null)
                    {
                        ProfessionalTask = pt;
                        ClassCode = pt.classCode;
                        ExistsAutoTreatment = pt.AutoTreatment ?? false;
                        IsPreferAutoTreatmentEnabled = ExistsAutoTreatment;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao carregar ProfessionalTask: {ex.Message}");
                }
            }

            // Iniciar verificação do ProfessionalTask
            _ = StartUpProfessionalTaskAsync();
        }

        #endregion

        #region Métodos de Inicialização

        private async Task StartUpProfessionalTaskAsync()
        {
            if (string.IsNullOrEmpty(ClassCode))
                return;

            try
            {
                ProfessionalTask = await LesserFunctionClient.DefaultClient.GetProfessionalTask(ClassCode);
                IsClassCodeValid = ProfessionalTask != null;
            }
            catch
            {
                IsClassCodeValid = false;
            }
        }

        #endregion

        #region Métodos de Validação e Verificação

        private async Task ClassCodeChangedAsync()
        {
            IsCheckingClassCode = true;
            IsClassCodeValid = true;

            // Atualizar UI para estado de verificação
            IsStatusClassCodeVisible = true;
            IsFolderPathEnabled = false;
            IsPathFilterEnabled = false;
            StatusClassCodeText = Loc.Tr("Analisando, aguarde...");

            try
            {
                ProfessionalTask = await LesserFunctionClient.DefaultClient.GetProfessionalTask(ClassCode);
                IsClassCodeValid = ProfessionalTask != null;

                if (IsClassCodeValid && ProfessionalTask != null)
                {
                    ExistsAutoTreatment = ProfessionalTask.AutoTreatment ?? false;
                    IsPreferAutoTreatmentEnabled = ExistsAutoTreatment;
                }
            }
            catch
            {
                IsClassCodeValid = false;
            }

            IsCheckingClassCode = false;

            // Atualizar UI com resultado
            if (IsClassCodeValid)
            {
                IsStatusClassCodeVisible = false;
                StatusClassCodeText = "";
            }
            else
            {
                IsStatusClassCodeVisible = true;
                StatusClassCodeText = Loc.Tr("Este código de coleção não existe");
            }

            IsFolderPathEnabled = true;
            IsPathFilterEnabled = true;

            CheckStartDownloadAvailability();
        }

        private void CheckStartDownloadAvailability()
        {
            bool canStart = IsFolderPathValid && 
                           IsClassCodeValid && 
                           !IsDownloading && 
                           ProfessionalTask != null &&
                           !string.IsNullOrEmpty(ClassCode);

            IsStartDownloadEnabled = canStart;
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task StartDownloadAsync()
        {
            if (ProfessionalTask == null || string.IsNullOrEmpty(FolderPath))
            {
                await ShowMessageAsync(Loc.Tr("Erro"), Loc.Tr("Dados inválidos para iniciar o download."));
                return;
            }

            try
            {
                IsDownloading = true;
                IsStartDownloadEnabled = false;
                IsClassCodeEnabled = false;
                IsFolderPathEnabled = false;
                IsPathFilterEnabled = false;
                IsProgressContainerVisible = true;

                var service = new DownloadHDPhotosInBatchService();
                var outputFolder = new DirectoryInfo(FolderPath);
                const int maxRetryAttempts = 3;
                int retryCount = 0;
                DownloadHDPhotosInBatchService.DownloadAllPhotosResult? result = null;

                // Loop de retry automático (até 3 tentativas)
                while (true)
                {
                    // Executar download em background
                    result = await Task.Run(async () =>
                    {
                        return await service.DownloadAllPhotosInClass(
                            ProfessionalTask,
                            outputFolder,
                            PathFilter,
                            UpdateProgress,
                            PreferAutoTreatment,
                            SmartCompress
                        );
                    });

                    // Se não houve erros, sair do loop
                    if (result.HasValue && (result.Value.success || result.Value.missingPhotos == null || result.Value.missingPhotos.Count == 0))
                    {
                        break;
                    }

                    // Se ainda há erros e não atingiu o máximo de tentativas
                    if (retryCount < maxRetryAttempts - 1)
                    {
                        retryCount++;
                        
                        // Atualizar mensagem de progresso na UI thread
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ProgressText = string.Format(Loc.Tr("Algumas fotos não foram baixadas. Tentando novamente ({0} de {1})..."), retryCount + 1, maxRetryAttempts);
                        });

                        // Aguardar 2 segundos antes de tentar novamente (igual ao ExporterAvalonia)
                        await Task.Delay(2000);
                    }
                    else
                    {
                        // Atingiu o máximo de tentativas, sair do loop
                        break;
                    }
                }

                // Atualizar resultado na UI thread
                if (result.HasValue)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        DownloadAllPhotosResult = result;
                        IsFinishAllProcess = true;
                        
                        if (result.Value.success)
                        {
                            LabelFinishAllProcess = Loc.Tr("Todas as fotos foram baixadas com sucesso.");
                        }
                        else
                        {
                            LabelFinishAllProcess = Loc.Tr("Download concluído com alguns erros após múltiplas tentativas.");
                        }
                        
                        // Verificar se deve fechar o app automaticamente
                        if (CloseAppAfterFinish && result.Value.success)
                        {
                            _ = CloseThisAppAsync(LabelFinishAllProcess);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync(Loc.Tr("Erro"), string.Format(Loc.Tr("Erro ao realizar download: {0}"), ex.Message) + $"\n\n{ex.StackTrace}");
            }
            finally
            {
                IsDownloading = false;
                IsStartDownloadEnabled = true;
                IsClassCodeEnabled = true;
                IsFolderPathEnabled = true;
                IsPathFilterEnabled = true;
                CheckStartDownloadAvailability();
            }
        }

        #endregion

        #region Métodos de Atualização de Progresso

        private void UpdateProgress(DownloadHDPhotosInBatchService.ProgressCallbackContent progress)
        {
            // Otimização de performance: verificar se já está na UI thread
            // Se estiver, atualiza diretamente (mais rápido)
            // Se não estiver, usa InvokeAsync para thread-safety
            if (Dispatcher.UIThread.CheckAccess())
            {
                // Já está na UI thread, atualiza diretamente
                UpdateProgressInternal(progress);
            }
            else
            {
                // Não está na UI thread, usa InvokeAsync para thread-safety
                // Usa Post com Background priority para não bloquear operações críticas
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateProgressInternal(progress);
                }, DispatcherPriority.Background);
            }
        }

        private void UpdateProgressInternal(DownloadHDPhotosInBatchService.ProgressCallbackContent progress)
        {
            if (progress.total > 0)
            {
                ProgressPercentage = (int)(progress.currentIndex * 100.0 / progress.total);
            }
            ProgressText = string.Format(Loc.Tr("Foto {0} de {1} | {2}% - {3}"), progress.currentIndex, progress.total, ProgressPercentage, progress.message);
        }

        #endregion

        #region Métodos de UI e Mensagens

        private async Task ShowMessageAsync(string caption, string message)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var box = MessageBoxManager.GetMessageBoxStandard(caption, message, ButtonEnum.Ok);
                await box.ShowAsync();
            });
        }

        private async Task ShowMessageAndCloseApp(string message, string caption = "")
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var box = MessageBoxManager.GetMessageBoxStandard(caption, message, ButtonEnum.Ok);
                await box.ShowAsync();

                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            });
        }

        private async Task ShowLoginRequiredMessageAndOpenDashboard()
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var box = MessageBoxManager.GetMessageBoxStandard(
                        Loc.Tr("Erro"),
                        Loc.Tr("Falha em autenticação, faça login em nosso sistema."),
                        ButtonEnum.Ok
                    );

                    await box.ShowAsync();

                    // Após clicar em OK, abrir o dashboard
                    await StartLatestDashboard();

                    // Fechar o app após abrir o dashboard
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.Shutdown();
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao mostrar mensagem e abrir dashboard: {ex.Message}");
                // Tentar abrir o dashboard mesmo em caso de erro
                try
                {
                    await StartLatestDashboard();
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"Erro ao abrir dashboard: {ex2.Message}");
                }

                // Fechar o app mesmo em caso de erro
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
        }

        /// <summary>
        /// Abre a versão mais recente do dashboard disponível na máquina do usuário
        /// </summary>
        private async Task StartLatestDashboard()
        {
            try
            {
                // Caminho para as versões do LesserDashboard conforme informado pelo usuário
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string appVersionsPath = Path.Combine(documentsPath, "Separacao", "apps", "LesserDashboard", "v");
                
                if (Directory.Exists(appVersionsPath))
                {
                    // Buscar todas as versões disponíveis
                    var versionDirs = Directory.GetDirectories(appVersionsPath)
                        .OrderByDescending(d => d) // Ordenar por nome (versão mais recente primeiro)
                        .ToArray();
                    
                    if (versionDirs.Length > 0)
                    {
                        // Pegar a versão mais recente disponível
                        string latestVersionPath = Path.Combine(versionDirs[0], "LesserDashboard.exe");
                        
                        if (File.Exists(latestVersionPath))
                        {
                            // Abrir diretamente com Process.Start usando o caminho encontrado
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = latestVersionPath,
                                UseShellExecute = true
                            });
                            return;
                        }
                    }
                }

                // Fallback: tentar usar AppInstaller se não encontrar versão específica
                SharedClientSide.Helpers.AppInstaller installer = new SharedClientSide.Helpers.AppInstaller("LesserDashboard", _ => { });
                await installer.startApp();
            }
            catch (Exception ex)
            {
                // Em caso de erro, não fazer nada para não interferir no fechamento do app
                Console.WriteLine($"Erro ao tentar abrir o dashboard: {ex.Message}");
            }
        }

        private void PopulateContainerErrors()
        {
            if (DownloadAllPhotosResult == null)
            {
                IsStatusContainerVisible = false;
                return;
            }

            IsStatusContainerVisible = true;

            if (!DownloadAllPhotosResult.Value.success)
            {
                StatusDownloadMessage = Loc.Tr("Houve erro ao baixar as seguintes fotos:");
                ErrorPhotos = DownloadAllPhotosResult.Value.missingPhotos ?? new List<string>();
            }
            else
            {
                StatusDownloadMessage = Loc.Tr("Todas as fotos foram baixadas com sucesso.");
                ErrorPhotos = new List<string>();
            }
            
            OnPropertyChanged(nameof(HasErrorPhotos));
        }


        public async Task CloseThisAppAsync(string baseText)
        {
            if (CloseAppAfterFinish == true)
            {
                int seconds = 10;

                for (int i = seconds; i > 0; i--)
                {
                    int remaining = i;
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        LabelFinishAllProcess = $"{baseText} {Loc.Tr("Esta janela será fechada em")} {remaining} {Loc.Tr("segundos")}";
                    });

                    await Task.Delay(1000);
                }

                // Após a contagem regressiva, fecha o app
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.Shutdown();
                    }
                });
            }
        }

        #endregion
    }
}










