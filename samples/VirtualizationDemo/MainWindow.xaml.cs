using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using VirtualizationDemo.ViewModels;

namespace VirtualizationDemo
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.AttachDevTools();
            DataContext = new MainWindowViewModel();

            {
                //bug in combobox
                var items = new AvaloniaList<string>(Enumerable.Range(1, 5).Select(i => $"Item {i}"));
                var cbo = this.Get<ComboBox>("cbo");
                cbo.Items = items;
               // var sel = new Sel();
              //  cbo.Bind(ComboBox.SelectedItemProperty, new Binding("Selected") { Mode = BindingMode.TwoWay, Source = sel });
                int c = 1;
                // sel.Selected = items.First();
                cbo.SelectedItem = items.First();
                this.Get<Button>("btnCbo").Click += (s, e) =>
                {
                    c++;
                    //sel.Selected = null;
                    items.Clear();
                    //items.AddRange(Enumerable.Range(1, 5).Select(i => $"New {c} Item {i}"));
                    //cbo.Items = items.ToArray();
                };
            }
        }

        class Sel : ReactiveObject
        {
            private string _Selected;

            public string Selected
            {
                get => _Selected;
                set => this.RaiseAndSetIfChanged(ref _Selected, value);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
