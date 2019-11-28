using System.Threading;

namespace RxMindstorms.Core
{
	internal class Response
	{
		public ReplyType ReplyType { get; set; }
		public ushort Sequence { get; }
		public byte[] Data { get; set; }
		public SystemOpcode SystemCommand { get; set; }
		public SystemReplyStatus SystemReplyStatus { get; set; }

		internal Response(ushort sequence)
		{
			Sequence = sequence;
		}
	}
}