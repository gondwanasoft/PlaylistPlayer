using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PlaylistPlayer
{
    /// <summary>
    /// Interaction logic for NotificationWindow.xaml
    /// </summary>
    public partial class NotificationWindow : Window
    {
        DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();

        public NotificationWindow()
        {
            InitializeComponent();
            timer.Interval = TimeSpan.FromSeconds(1d);
            timer.Tick += new EventHandler(Timer_Tick);
        }

        public void Show(Window parent, string text) {
            if (parent.WindowState == WindowState.Maximized) {
                Top = 0;
                Left = 0;
            } else {
                Left = parent.Left;
                Top = parent.Top;
            }
            message.Content = text;
            base.Show();
            timer.Start();
        }

        void Timer_Tick(object sender, EventArgs e) {
            timer.Stop();
            this.Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) {
            this.Close();
            Console.WriteLine("NotificationWindow keyDown " + this.Parent==null?"null":"not null");
            LibVLCSharp.WPF.Sample.MainWindow mainWindow = (LibVLCSharp.WPF.Sample.MainWindow)Application.Current.MainWindow;
            mainWindow.Window_KeyDown(sender, e);
        }
    }
}
