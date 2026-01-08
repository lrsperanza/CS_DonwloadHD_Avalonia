using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DownloadHDAvalonia.Controls
{
    /// <summary>
    /// Custom control para exibir labels com conteúdo dinâmico
    /// </summary>
    public partial class LabelPerson : UserControl
    {
        private string _labelContent = string.Empty;
        
        public string LabelContent
        {
            get => _labelContent;
            set
            {
                if (_labelContent != value)
                {
                    _labelContent = value;
                    if (label != null)
                    {
                        label.Text = value;
                    }
                }
            }
        }

        public LabelPerson()
        {
            InitializeComponent();
        }
    }
}

