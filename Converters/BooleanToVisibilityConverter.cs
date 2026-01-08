using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace DownloadHDAvalonia.Converters
{
    /// <summary>
    /// Converte um valor booleano para visibilidade (IsVisible).
    /// Em Avalonia, IsVisible já é bool, então este converter pode ser usado para inversão ou casos especiais.
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converte um bool para bool (IsVisible).
        /// Se o parâmetro for "Inverse", inverte o valor.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                // Se o parâmetro for "Inverse", inverte o valor
                if (parameter is string param && param == "Inverse")
                {
                    return !booleanValue;
                }
                return booleanValue;
            }
            return false;
        }

        /// <summary>
        /// Converte de volta de bool (IsVisible) para bool.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Se o parâmetro for "Inverse", inverte o valor
                if (parameter is string param && param == "Inverse")
                {
                    return !boolValue;
                }
                return boolValue;
            }
            return false;
        }
    }
}











