using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace RxMindstorms
{
	/// <summary>
	/// Main EV3 brick interface
	/// </summary>
	public sealed class Brick
	{
		/// <summary>
		/// Width of LCD screen
		/// </summary>
		public ushort LcdWidth => 178;

		/// <summary>
		/// Height of LCD screen
		/// </summary>
		public ushort LcdHeight => 128;

		/// <summary>
		/// Height of status bar
		/// </summary>
		public ushort TopLineHeight => 10;

		private readonly SynchronizationContext _context = SynchronizationContext.Current;
		private readonly ICommunication _comm;

		private readonly ResponseManager _responseManager;

		private readonly bool _alwaysSendEvents;
		private readonly DirectCommand _directCommand;
		private readonly SystemCommand _systemCommand;

		private readonly BehaviorSubject<Response> _subject;

		/// <summary>
		/// Input and output ports on LEGO EV3 brick
		/// </summary>
		public IDictionary<InputPort,Port> Ports { get; set; }

		/// <summary>
		/// Buttons on the face of the LEGO EV3 brick
		/// </summary>
		public BrickButtons Buttons { get; set; }

		/// <summary>
		/// Send "direct commands" to the EV3 brick.  These commands are executed instantly and are not batched.
		/// </summary>
		public DirectCommand DirectCommand => _directCommand;

		/// <summary>
		/// Send "system commands" to the EV3 brick.  These commands are executed instantly and are not batched.
		/// </summary>
		public SystemCommand SystemCommand => _systemCommand;

		/// <summary>
		/// Create a batch command of multiple direct commands at once.
		/// </summary>
		public Command CreateCommand(CommandType commandType, ushort globalSize = 0, int localSize = 0) =>
			new Command(commandType, globalSize, localSize, _responseManager.GetSequenceNumber());

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="comm">Object implementing the <see cref="ICommunication"/> interface for talking to the brick</param>
		/// <param name="responseManager">The response manager used to get sequence numbers and create reports</param>
		public Brick(ICommunication comm, ResponseManager responseManager)
			: this(comm, responseManager, false) { }

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="comm">Object implementing the <see cref="ICommunication"/> interface for talking to the brick</param>
		/// <param name="responseManager">The response manager used to get sequence numbers and create reports</param>
		/// <param name="alwaysSendEvents">Send events when data changes, or at every poll</param>
		public Brick(ICommunication comm, ResponseManager responseManager, bool alwaysSendEvents)
		{
			_subject = new BehaviorSubject<Response>(null);
			_directCommand = new DirectCommand(this);
			_systemCommand = new SystemCommand(this);

			Buttons = new BrickButtons();

			_comm = comm ?? throw new ArgumentNullException(nameof(comm));
			_responseManager = responseManager ?? throw new ArgumentNullException(nameof(comm));
			_alwaysSendEvents = alwaysSendEvents;

			Ports = new Dictionary<InputPort,Port>();

			int index = 0;
			foreach(InputPort i in Enum.GetValues(typeof(InputPort)))
			{
				Ports[i] = new Port
				{
					InputPort = i,
					Index = index++,
					Name = i.ToString(),
				};
			}
		}

		/// <summary>
		/// Connect to the EV3 brick.
		/// </summary>
		/// <returns></returns>
		public IObservable<BrickChangedEventArgs> Connect() =>
			Connect(TimeSpan.FromMilliseconds(100));

		/// <summary>
		/// Connect to the EV3 brick with a specified polling time.
		/// </summary>
		/// <param name="pollingTime">The period to poll the device status.  Set to TimeSpan.Zero to disable polling.</param>
		/// <returns>Observable with brick changed events</returns>
		public IObservable<BrickChangedEventArgs> Connect(TimeSpan pollingTime) =>
			Observable.Create<BrickChangedEventArgs>(observer =>
			{
				var compositeDisposable = new CompositeDisposable();

				_comm
					.Connect()
					.Select(e => ResponseManager.CreateResponse(e.Report))
					.Subscribe(_subject)
					.DisposeWith(compositeDisposable);

				_directCommand.StopMotorAsync(OutputPort.All, false);

				Observable
					.Interval(pollingTime)
					.SelectMany(async _ =>
						{
							var result = await PollSensorsAsync();
							await _directCommand.StopMotorAsync(OutputPort.All, false);
							return result;
						})
					.Where(e => e != null)
					.Subscribe(observer)
					.DisposeWith(compositeDisposable);

				return compositeDisposable;
			});

		internal async Task<Response> SendCommandAsyncInternal(Command command)
		{
			await _comm.WriteAsync(command.ToBytes());
			
			return command.CommandType == CommandType.DirectReply ||
			       command.CommandType == CommandType.SystemReply
				? await
					_subject
						.Where(r => r != null && r.Sequence == command.SequenceNumber)
						.FirstAsync()
				: null;
		}

		public IObservable<Unit> SendCommand(Command command)
		{
			var writeTask = _comm.WriteAsync(command.ToBytes());
			
			if (command.CommandType == CommandType.DirectReply ||
				command.CommandType == CommandType.SystemReply)
				return 
					_subject
						.AsObservable()
						.Where(r => r != null && r.Sequence == command.SequenceNumber)
						.Select(_ => Unit.Default)
						.FirstAsync();

			return
				Observable
					.FromAsync(async () =>
						{
							await writeTask;
							return Unit.Default;
						});
		}

		private async Task<BrickChangedEventArgs> PollSensorsAsync()
		{
			bool changed = false;
			const int responseSize = 11;
			int index = 0;

			Command command = CreateCommand(CommandType.DirectReply, (8 * responseSize) + 6, 0);

			foreach(InputPort i in Enum.GetValues(typeof(InputPort)))
			{
				Port p = Ports[i];
				
				index = p.Index * responseSize;

				command.GetTypeMode(p.InputPort, (byte)index, (byte)(index+1));
				command.ReadySI(p.InputPort, p.Mode, (byte)(index+2));
				command.ReadyRaw(p.InputPort, p.Mode, (byte)(index+6));
				command.ReadyPercent(p.InputPort, p.Mode, (byte)(index+10));
			}

			index += responseSize;

			command.IsBrickButtonPressed(BrickButton.Back,  (byte)(index+0));
			command.IsBrickButtonPressed(BrickButton.Left,  (byte)(index+1));
			command.IsBrickButtonPressed(BrickButton.Up,    (byte)(index+2));
			command.IsBrickButtonPressed(BrickButton.Right, (byte)(index+3));
			command.IsBrickButtonPressed(BrickButton.Down,  (byte)(index+4));
			command.IsBrickButtonPressed(BrickButton.Enter, (byte)(index+5));

			var response = await SendCommandAsyncInternal(command);

			if(response?.Data == null)
				return null;

			foreach(InputPort i in Enum.GetValues(typeof(InputPort)))
			{
				Port p = Ports[i];

				int type = response.Data[(p.Index * responseSize)+0];
				byte mode = response.Data[(p.Index * responseSize)+1];
				float siValue = BitConverter.ToSingle(response.Data, (p.Index * responseSize)+2);
				int rawValue = BitConverter.ToInt32(response.Data, (p.Index * responseSize)+6);
				byte percentValue = response.Data[(p.Index * responseSize)+10];

				if((byte)p.Type != type || Math.Abs(p.SIValue - siValue) > 0.01f || p.RawValue != rawValue || p.PercentValue != percentValue)
					changed = true;

				if(Enum.IsDefined(typeof(DeviceType), type))
					p.Type = (DeviceType)type;
				else
					p.Type = DeviceType.Unknown;

				p.SIValue = siValue;
				p.RawValue = rawValue;
				p.PercentValue = percentValue;
			}

			if(	Buttons.Back  != (response.Data[index+0] == 1) ||
				Buttons.Left  != (response.Data[index+1] == 1) ||
				Buttons.Up    != (response.Data[index+2] == 1) ||
				Buttons.Right != (response.Data[index+3] == 1) ||
				Buttons.Down  != (response.Data[index+4] == 1) ||
				Buttons.Enter != (response.Data[index+5] == 1)
			)
				changed = true;

			Buttons.Back	= (response.Data[index+0] == 1);
			Buttons.Left	= (response.Data[index+1] == 1);
			Buttons.Up		= (response.Data[index+2] == 1);
			Buttons.Right	= (response.Data[index+3] == 1);
			Buttons.Down	= (response.Data[index+4] == 1);
			Buttons.Enter	= (response.Data[index+5] == 1);

			return (changed || _alwaysSendEvents)
				? new BrickChangedEventArgs { Ports = this.Ports, Buttons = this.Buttons }
				: null;
		}
	}
}
