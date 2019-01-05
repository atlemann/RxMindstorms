using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

namespace RxMindstorms.Core
{
	/// <summary>
	/// Communicate with EV3 brick over USB HID.
	/// </summary>
	public class UsbCommunication : ICommunication
	{
		// full-size report
		private byte[] _inputReport;
		private byte[] _outputReport;

		private readonly string _brickName;
		private HidStream _stream;

		public UsbCommunication(string brickName)
		{
			_brickName = brickName;
		}
		
		/// <summary>
		/// Connect to the EV3 brick.
		/// </summary>
		/// <returns></returns>
		public IObservable<ReportReceivedEventArgs> Connect()
		{
			Console.WriteLine("Trying to find the LEGO brick!");

			var brick =
				FindBrick();
			
			if (brick == null)
				throw new Exception($"No LEGO EV3s with name '{_brickName}' found in HID device list.");

			Console.WriteLine("Found LEGO EV3 device!");
			
			_inputReport = new byte[brick.GetMaxInputReportLength()];
			_outputReport = new byte[brick.GetMaxOutputReportLength()];

			if (!brick.TryOpen(out _stream))
				throw new Exception("Could not open stream to device.");

			var obs =
				Observable.Create<ReportReceivedEventArgs>(async observer =>
				{
					var tokenSource =
						new CancellationTokenSource();

					while (!tokenSource.IsCancellationRequested)
					{
						// if the stream is valid and ready
						if (_stream == null || !_stream.CanRead)
							continue;

						try
						{
							await
								_stream
									.ReadAsync(_inputReport, 0, _inputReport.Length, tokenSource.Token)
									.ConfigureAwait(false);
						}
						catch (TaskCanceledException)
						{
							break;
						}
						catch (Exception ex)
						{
							observer.OnError(ex);
							break;
						}

						short size = (short) (_inputReport[1] | _inputReport[2] << 8);
						if (size > 0)
						{
							byte[] report = new byte[size];
							Array.Copy(_inputReport, 3, report, 0, size);
							observer.OnNext(new ReportReceivedEventArgs {Report = report});
						}
					}

					return Disposable.Create(() =>
					{
						tokenSource.Cancel();
						_stream.Dispose();
						_stream = null;
					});
				});
			
			return obs;
		}

		/// <summary>
		/// Write data to the EV3 brick.
		/// </summary>
		/// <param name="data">Byte array to send to the EV3 brick.</param>
		/// <returns></returns>
		public async Task WriteAsync(byte[] data)
		{
			if (_stream == null)
				throw new Exception("Must connect before writing to device.");
			
			data.CopyTo(_outputReport, 1);
			
			await
				_stream
					.WriteAsync(_outputReport, 0, _outputReport.Length)
					.ConfigureAwait(false);
			
			_stream.Flush();
		}

		private HidDevice FindBrick()
		{
			Console.WriteLine("Searching for EV3 brick...");
			
			var deviceList =
				DeviceList.Local;

			var stopwatch =
				Stopwatch.StartNew();

			var hidDeviceList =
				deviceList
					.GetHidDevices()
					.ToArray();

			Console.WriteLine("Complete device list (took {0} ms to get {1} devices):",
				stopwatch.ElapsedMilliseconds, hidDeviceList.Length);

			return 
				hidDeviceList
					.FirstOrDefault(x => Equals(x.GetProductName(), _brickName));
		}
	}
}
