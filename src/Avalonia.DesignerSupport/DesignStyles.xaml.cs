using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Avalonia.DesignerSupport
{
    internal class DesignStyles : Styles
    {
        public DesignStyles()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public static void Apply(Window control)
        {
            control.Styles.Add(new DesignStyles());
        }

        public static readonly AttachedProperty<double> ScaleProperty =
                AvaloniaProperty.RegisterAttached<Window, double>("Scale", typeof(DesignStyles), 1);

        public static void SetScale(Window control, double value)
        {
            if (value <= 0)
            {
                value = 1;
            }

            control.SetValue(ScaleProperty, value);
        }

        public static double GetScale(Window control)
        {
            return control.GetValue(ScaleProperty);
        }
    }
}
