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
using System.Windows.Forms;
using Cognex.DataMan.SDK;
using System.Threading;
using System.Windows.Threading;

namespace QRCode_Sample.Views
{
    /// <summary>
    /// Interaction logic for ReconnectingWindow.xaml
    /// </summary>
    public partial class ReconnectingWindow : Window
    {
        private Window _parent = null;
        private DataManSystem _system = null;
        private SynchronizationContext _syncContext = null;
        private Thread _thread = null;
        private bool _cancel = false;
        public ReconnectingWindow( Window parent, DataManSystem system)
        {
            _parent = parent;
            _system = system;
            _syncContext = DispatcherSynchronizationContext.Current;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _thread = new Thread(ReconnectThread);
            _thread.Name = "frmReconnecting.ReconnectThread";
            _thread.Start();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = System.Windows.MessageBox.Show("You want cancel this process?", "Cancel", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel)
            {
                _cancel = true;
            }
            //_cancel = true;

        }
        private void ReconnectThread()
        {
            while (!_cancel)
            {
                try
                {
                    _system.Connect();
                }
                catch
                {
                    Thread.Sleep(500);
                    continue;
                }

                _syncContext.Post(
                    delegate
                    {
                        MessageBoxResult result = System.Windows.MessageBox.Show("OK?", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
                        if (result == MessageBoxResult.OK)
                        {
                            Close();
                        }
                       
                    },
                    null);

                break;
            }
        }
    }
}
