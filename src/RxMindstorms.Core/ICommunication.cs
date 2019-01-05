using System;
using System.Threading.Tasks;

namespace RxMindstorms.Core
{
	/// <summary>
	/// Interface for communicating with the EV3 brick
	/// </summary>
	public interface ICommunication
	{
		/// <summary>
		/// Connect to the EV3 brick.
		/// </summary>Task
		IObservable<ReportReceivedEventArgs> Connect();

		/// <summary>
		/// Write a report to the EV3 brick.
		/// </summary>
		/// <param name="data"></param>Task
		Task WriteAsync(byte[] data);
	}
}
