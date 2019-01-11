using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RxMindstorms.Core
{
	public class ResponseManager
	{
		private ushort _nextSequence = 0x0001;
		internal readonly Dictionary<int, Response> Responses = new Dictionary<int, Response>();

		public ushort GetSequenceNumber()
		{
			if(_nextSequence == UInt16.MaxValue)
				_nextSequence++;

			return _nextSequence++;
		}

		// internal Response CreateResponse()
		// {
		// 	ushort sequence = GetSequenceNumber();

		// 	Response r = new Response(sequence);
		// 	Responses[sequence] = r;
		// 	return r;
		// }

		// internal static async Task WaitForResponseAsync(Response r)
		// {
		// 	await Task.Run(() =>
		// 	{
		// 		if(r.Event.WaitOne(1000))
		// 			Responses.Remove(r.Sequence);
		// 		else
		// 			r.ReplyType = ReplyType.DirectReplyError;
		// 	});
		// }

		internal static Response CreateResponse(byte[] report)
		{
			if (report == null || report.Length < 3)
				return null;

			ushort sequence = (ushort) (report[0] | (report[1] << 8));
			int replyType = report[2];

			//System.Diagnostics.Debug.WriteLine("Size: " + report.Length + ", Sequence: " + sequence + ", Type: " + (ReplyType)replyType + ", Report: " + BitConverter.ToString(report));

			if (sequence == 0)
				return null;

			Response response = new Response(sequence);

			if (Enum.IsDefined(typeof (ReplyType), replyType))
				response.ReplyType = (ReplyType) replyType;

			if (response.ReplyType == ReplyType.DirectReply ||
			    response.ReplyType == ReplyType.DirectReplyError)
			{
				response.Data = new byte[report.Length - 3];
				Array.Copy(report, 3, response.Data, 0, report.Length - 3);
			}
			else if (response.ReplyType == ReplyType.SystemReply ||
			         response.ReplyType == ReplyType.SystemReplyError)
			{
				if (Enum.IsDefined(typeof (SystemOpcode), (int) report[3]))
					response.SystemCommand = (SystemOpcode) report[3];

				if (Enum.IsDefined(typeof (SystemReplyStatus), (int) report[4]))
					response.SystemReplyStatus = (SystemReplyStatus) report[4];

				response.Data = new byte[report.Length - 5];
				Array.Copy(report, 5, response.Data, 0, report.Length - 5);
			}

			return response;
		}
	}
}
