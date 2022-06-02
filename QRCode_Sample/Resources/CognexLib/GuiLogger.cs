using Cognex.DataMan.SDK;
using System;
using System.Collections.Generic;
using System.Text;

using System.Threading;
using System.Windows.Controls;

namespace QRCode_Sample.Resources.CognexLib
{
internal class GuiLogger : ILogger
	{
		private delegate void LogHandler(string msg);

		private static int _nextSessionId = 0;
		private TextBox _logBox;
		private bool? _isGuiClosing;
		private int _enabled = 0;
		private Thread _guiUpdaterThread;
		private AutoResetEvent _wakeupEvent;
		private Queue<string> _messages;

		public GuiLogger(TextBox logBox, bool enabled, ref bool isGuiClosing)
		{
			_logBox = logBox;
			_isGuiClosing = isGuiClosing;
			_wakeupEvent = new AutoResetEvent(false);
			_messages = new Queue<string>();

			Enabled = enabled;
		}

		public bool Enabled
		{
			get { return _enabled == 1; }
			set
			{
				if (value == true)
					Start();
				else
					Stop();
			}
		}

		private void Start()
		{
			if (Interlocked.CompareExchange(ref _enabled, 1, 0) != 0)
				return; // already started

			_wakeupEvent.Reset();

			_guiUpdaterThread = new Thread(GuiUpdaterThreadFunc);
			_guiUpdaterThread.IsBackground = true;
			_guiUpdaterThread.Name = "Gui Log Updater Thread";
			_guiUpdaterThread.Start();
		}

		private void Stop()
		{
			if (Interlocked.CompareExchange(ref _enabled, 0, 1) != 1)
				return; // already stopped

			_wakeupEvent.Set();

			_guiUpdaterThread.Join();
			_guiUpdaterThread = null;
		}

		private void EnqueueMessage(string message)
		{
			lock (_messages)
			{
				System.Diagnostics.Debug.Write(message);

				System.Diagnostics.Debug.Assert(_messages.Count < 10000);

				if (Enabled && _messages.Count < 10000)
				{
					_messages.Enqueue(message);
					_wakeupEvent.Set();
				}
			}
		}

		private void DisplayEnqueuedMessages()
		{
			string message;

			lock (_messages)
			{
				if (_messages.Count == 0)
				{
					return;
				}
				else if (_messages.Count == 1)
				{
					message = _messages.Dequeue();
				}
				else
				{
					StringBuilder sb = new StringBuilder();

					while (_messages.Count > 0)
						sb.Append(_messages.Dequeue());

					message = sb.ToString();
				}
			}

			if (message != null)
			{
				if (_logBox.Dispatcher.CheckAccess())
					_logBox.Dispatcher.BeginInvoke(new LogHandler(LogToGui_Invoke), new object[] { message });
				else
					LogToGui_Invoke(message);
			}
		}

		private void GuiUpdaterThreadFunc()
		{
			try
			{
				while (Enabled)
				{
					DisplayEnqueuedMessages();
					_wakeupEvent.WaitOne(500, false);
				}
			}
			catch
			{
			}
		}

		public int GetNextUniqueSessionId()
		{
			return Interlocked.Increment(ref _nextSessionId);
		}

		public void Log(string function, string message)
		{
			EnqueueMessage(string.Format("{0}: {1} [{2}]\r\n", function, message, DateTime.Now.ToLongTimeString()));
		}

		public void LogTraffic(int sessionId, bool isRead, byte[] buffer, int offset, int count)
		{
			EnqueueMessage(string.Format("Traffic: {0} {1} bytes at {2} [session #{3}]: {4}{5}\r\n", isRead ? "Read" : "Written", count, DateTime.Now.ToLongTimeString(), sessionId, GetBytesAsPrintable(buffer, offset, Math.Min(50, count)), count > 50 ? "..." : ""));
		}

		private void LogToGui_Invoke(string msg)
		{
			System.Diagnostics.Debug.Assert(_logBox.Dispatcher.CheckAccess() == false);   // Must be called on the GUI thread

			if (_logBox == null || _isGuiClosing == null || !_isGuiClosing.HasValue || _isGuiClosing.Value == true)
				return;

#if !WindowsCE
			_logBox.AppendText(msg);
#else
            _logBox.Text = _logBox.Text + msg;
#endif
		}

		private static string GetBytesAsPrintable(byte[] buffer, int offset, int count)
		{
			if (buffer == null || count < 1 || offset + count > buffer.Length)
				return "";

			StringBuilder SB = new StringBuilder(count * 6);
			for (int i = offset; i < buffer.Length && i < offset + count; ++i)
			{
				if (buffer[i] < (byte)' ' || buffer[i] >= 127)
				{
					SB.Append(String.Format("<0x{0:X2}>", buffer[i]));
				}
				else
				{
					SB.Append((char)buffer[i]);
				}
			}

			return SB.ToString();
		}
	}
}
