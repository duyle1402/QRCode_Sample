using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;
using Cognex.DataMan.SDK;
using Cognex.DataMan.SDK.Discovery;
using Cognex.DataMan.SDK.Utils;
using QRCode_Sample.Resources.CognexLib;
using Image = System.Drawing.Image;

namespace QRCode_Sample.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>


    
    public partial class MainWindow : Window
    {

        private ResultCollector _results;

        private SynchronizationContext _syncContext = null;
        private EthSystemDiscoverer _ethSystemDiscoverer = null;
        private SerSystemDiscoverer _serSystemDiscoverer = null;
        private ISystemConnector _connector = null;
        private DataManSystem _system = null;
        private object _currentResultInfoSyncLock = new object();
        private bool _closing = false;
        private bool _autoconnect = false;
        private object _listAddItemLock = new object();
        private GuiLogger _logger;
        public MainWindow()
        {
            InitializeComponent();
            _syncContext = DispatcherSynchronizationContext.Current;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _logger = new GuiLogger(tbLog, (bool)cbLoggingEnabled.IsChecked, ref _closing);

            // Create discoverers to discover ethernet and serial port systems.
            _ethSystemDiscoverer = new EthSystemDiscoverer();
            _serSystemDiscoverer = new SerSystemDiscoverer();

            // Subscribe to the system discoved event.
            _ethSystemDiscoverer.SystemDiscovered += new EthSystemDiscoverer.SystemDiscoveredHandler(OnEthSystemDiscovered);
            _serSystemDiscoverer.SystemDiscovered += new SerSystemDiscoverer.SystemDiscoveredHandler(OnSerSystemDiscovered);

            // Ask the discoverers to start discovering systems.
            _ethSystemDiscoverer.Discover();
            _serSystemDiscoverer.Discover();

            RefreshGui();
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            _closing = true;
            _autoconnect = false;

            if (null != _system && _system.State == ConnectionState.Connected)
                _system.Disconnect();

            _ethSystemDiscoverer.Dispose();
            _ethSystemDiscoverer = null;

            _serSystemDiscoverer.Dispose();
            _serSystemDiscoverer = null;
        }
        private void Results_ComplexResultCompleted(object sender, ComplexResult e)
        {
            _syncContext.Post(
                delegate
                {
                    ShowResult(e);
                },
                null);
        }

        private void Results_SimpleResultDropped(object sender, SimpleResult e)
        {
            _syncContext.Post(
                delegate
                {
                    ReportDroppedResult(e);
                },
                null);
        }

        private void ReportDroppedResult(SimpleResult result)
        {
            AddListItem(string.Format("Partial result dropped: {0}, id={1}", result.Id.Type.ToString(), result.Id.Id));
        }

        private void RefreshGui()
        {
            bool system_connected = _system != null && _system.State == ConnectionState.Connected;
            bool system_ready_to_connect = _system == null || _system.State == ConnectionState.Disconnected;
            bool gui_ready_to_connect = lbDetectedSystem.SelectedIndex != -1 && lbDetectedSystem.Items.Count > lbDetectedSystem.SelectedIndex;

            btnConnect.IsEnabled = system_ready_to_connect && gui_ready_to_connect;
            btnDisconnect.IsEnabled = system_connected;
            btnTrigger.IsEnabled = system_connected;
            cbLiveDisplay.IsEnabled = system_connected;
        }
		private void btnTrigger_MouseDown(object sender, MouseButtonEventArgs e)
		{
			try
			{
				_system.SendCommand("TRIGGER ON");
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to send TRIGGER ON command: " + ex.ToString());
			}
		}

		private void btnTrigger_MouseUp(object sender, MouseButtonEventArgs e)
		{
			try
			{
				_system.SendCommand("TRIGGER OFF");
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to send TRIGGER OFF command: " + ex.ToString());
			}
		}

		private void Log(string function, string message)
		{
			if (_logger != null)
				_logger.Log(function, message);
		}
		#region Device Discovery Events

		private void OnEthSystemDiscovered(EthSystemDiscoverer.SystemInfo systemInfo)
        {
            _syncContext.Post(
                new SendOrPostCallback(
                    delegate
                    {
                        lbDetectedSystem.Items.Add(systemInfo);
                    }),
                    null);
        }

        private void OnSerSystemDiscovered(SerSystemDiscoverer.SystemInfo systemInfo)
        {
            _syncContext.Post(
                new SendOrPostCallback(
                    delegate
                    {
                        lbDetectedSystem.Items.Add(systemInfo);
                    }),
                    null);
        }

		#endregion

		#region Device Events

		private void OnSystemConnected(object sender, EventArgs args)
		{
			_syncContext.Post(
				delegate
				{
					AddListItem("System connected");
					RefreshGui();
				},
				null);
		}

		private void OnSystemDisconnected(object sender, EventArgs args)
		{
			_syncContext.Post(
				delegate
				{
					AddListItem("System disconnected");
					bool reset_gui = false;

					if (!_closing && _autoconnect && (bool)cbAutoReconnect.IsChecked)
					{
						ReconnectingWindow wd = new ReconnectingWindow(this, _system);

						if (wd.ShowDialog() == DialogResult.Cancel)
							reset_gui = true;
					}
					else
					{
						reset_gui = true;
					}

					if (reset_gui)
					{
						btnConnect.IsEnabled = true;
						btnDisconnect.IsEnabled = false;
						btnTrigger.IsEnabled = false;
						cbLiveDisplay.IsEnabled = false;

						picResultImage.Image = null;
						lbReadString.Content = "";
					}
				},
				null);
		}

		private void OnKeepAliveResponseMissed(object sender, EventArgs args)
		{
			_syncContext.Post(
				delegate
				{
					AddListItem("Keep-alive response missed");
				},
				null);
		}

		private void OnSystemWentOnline(object sender, EventArgs args)
		{
			_syncContext.Post(
				delegate
				{
					AddListItem("System went online");
				},
				null);
		}

		private void OnSystemWentOffline(object sender, EventArgs args)
		{
			_syncContext.Post(
				delegate
				{
					AddListItem("System went offline");
				},
				null);
		}

		private void OnBinaryDataTransferProgress(object sender, BinaryDataTransferProgressEventArgs args)
		{
			Log("OnBinaryDataTransferProgress", string.Format("{0}: {1}% of {2} bytes (Type={3}, Id={4})", args.Direction == TransferDirection.Incoming ? "Receiving" : "Sending", args.TotalDataSize > 0 ? (int)(100 * (args.BytesTransferred / (double)args.TotalDataSize)) : -1, args.TotalDataSize, args.ResultType.ToString(), args.ResponseId));
		}

		private void OffProtocolByteReceived(object sender, OffProtocolByteReceivedEventArgs args)
		{
			Log("OffProtocolByteReceived", string.Format("{0}", (char)args.Byte));
		}

		private void AutomaticResponseArrived(object sender, AutomaticResponseArrivedEventArgs args)
		{
			Log("AutomaticResponseArrived", string.Format("Type={0}, Id={1}, Data={2} bytes", args.DataType.ToString(), args.ResponseId, args.Data != null ? args.Data.Length : 0));
		}

		#endregion


		#region Auxiliary Methods

		private void CleanupConnection()
		{
			if (null != _system)
			{
				_system.SystemConnected -= OnSystemConnected;
				_system.SystemDisconnected -= OnSystemDisconnected;
				_system.SystemWentOnline -= OnSystemWentOnline;
				_system.SystemWentOffline -= OnSystemWentOffline;
				_system.KeepAliveResponseMissed -= OnKeepAliveResponseMissed;
				_system.BinaryDataTransferProgress -= OnBinaryDataTransferProgress;
				_system.OffProtocolByteReceived -= OffProtocolByteReceived;
				_system.AutomaticResponseArrived -= AutomaticResponseArrived;
			}

			_connector = null;
			_system = null;
		}

		private void OnLiveImageArrived(IAsyncResult result)
		{
			try
			{
				Image image = _system.EndGetLiveImage(result);

				_syncContext.Post(
					delegate
					{
                        System.Drawing.Size image_size = Gui.FitImageInControl(image.Size, imgResultPicture.Size);
                        System.Drawing.Image fitted_image = Gui.ResizeImageToBitmap(image, image_size);
						picResultImage.Image = fitted_image;
						picResultImage.Invalidate();

						if ((bool)cbLiveDisplay.IsChecked)
						{
							_system.BeginGetLiveImage(
								ImageFormat.jpeg,
								ImageSize.Sixteenth,
								ImageQuality.Medium,
								OnLiveImageArrived,
								null);
						}
					},
				null);
			}
			catch
			{
			}
		}

		private string GetReadStringFromResultXml(string resultXml)
		{
			try
			{
				XmlDocument doc = new XmlDocument();

				doc.LoadXml(resultXml);

				XmlNode full_string_node = doc.SelectSingleNode("result/general/full_string");

				if (full_string_node != null && _system != null && _system.State == ConnectionState.Connected)
				{
					XmlAttribute encoding = full_string_node.Attributes["encoding"];
					if (encoding != null && encoding.InnerText == "base64")
					{
						if (!string.IsNullOrEmpty(full_string_node.InnerText))
						{
							byte[] code = Convert.FromBase64String(full_string_node.InnerText);
							return _system.Encoding.GetString(code, 0, code.Length);
						}
						else
						{
							return "";
						}
					}

					return full_string_node.InnerText;
				}
			}
			catch
			{
			}

			return "";
		}

		private void ShowResult(ComplexResult complexResult)
		{
			List<System.Drawing.Image> images = new List<Image>();
			List<string> image_graphics = new List<string>();
			string read_result = null;
			int result_id = -1;
			ResultTypes collected_results = ResultTypes.None;

			// Take a reference or copy values from the locked result info object. This is done
			// so that the lock is used only for a short period of time.
			lock (_currentResultInfoSyncLock)
			{
				foreach (var simple_result in complexResult.SimpleResults)
				{
					collected_results |= simple_result.Id.Type;

					switch (simple_result.Id.Type)
					{
						case ResultTypes.Image:
							Image image = ImageArrivedEventArgs.GetImageFromImageBytes(simple_result.Data);
							if (image != null)
								images.Add(image);
							break;

						case ResultTypes.ImageGraphics:
							image_graphics.Add(simple_result.GetDataAsString());
							break;

						case ResultTypes.ReadXml:
							read_result = GetReadStringFromResultXml(simple_result.GetDataAsString());
							result_id = simple_result.Id.Id;
							break;

						case ResultTypes.ReadString:
							read_result = simple_result.GetDataAsString();
							result_id = simple_result.Id.Id;
							break;
					}
				}
			}

			AddListItem(string.Format("Complex result arrived: resultId = {0}, read result = {1}", result_id, read_result));
			Log("Complex result contains", string.Format("{0}", collected_results.ToString()));

			if (images.Count > 0)
			{
				Image first_image = images[0];

                System.Drawing.Size image_size = Gui.FitImageInControl(first_image.Size, picResultImage.Size);
				Image fitted_image = Gui.ResizeImageToBitmap(first_image, image_size);

				if (image_graphics.Count > 0)
				{
					using (Graphics g = Graphics.FromImage(fitted_image))
					{
						foreach (var graphics in image_graphics)
						{
							ResultGraphics rg = GraphicsResultParser.Parse(graphics, new System.Drawing.Rectangle(0, 0, image_size.Width, image_size.Height));
							ResultGraphicsRenderer.PaintResults(g, rg);
						}
					}
				}

				if (imgResultPicture.Image != null)
				{
					var image = picResultImage.Image;
					picResultImage.Image = null;
					image.Dispose();
				}

				picResultImage.Image = fitted_image;
				picResultImage.Invalidate();
			}

			if (read_result != null)
				lbReadString.Content = read_result;
		}

		private void AddListItem(object item)
		{
			lock (_listAddItemLock)
			{
				lbStateConnect.Items.Add(item);

				if (lbStateConnect.Items.Count > 500)
					lbStateConnect.Items.RemoveAt(0);

				if (lbStateConnect.Items.Count > 0)
					lbStateConnect.SelectedIndex = lbStateConnect.Items.Count - 1;
			}
		}


        #endregion

        
    }
}
