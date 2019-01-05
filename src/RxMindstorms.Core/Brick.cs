using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RxMindstorms.Core
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
		private readonly bool _alwaysSendEvents;
		private readonly DirectCommand _directCommand;
		private readonly SystemCommand _systemCommand;
		private readonly Command _batchCommand;

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
		/// Send a batch command of multiple direct commands at once.  Call the <see cref="Command.Initialize"/> method with the proper <see cref="CommandType"/> to set the type of command the batch should be executed as.
		/// </summary>
		public Command BatchCommand => _batchCommand;

		/// <summary>
		/// Event that is fired when a port is changed
		/// </summary>
		public event EventHandler<BrickChangedEventArgs> BrickChanged;
		
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="comm">Object implementing the <see cref="ICommunication"/> interface for talking to the brick</param>
		public Brick(ICommunication comm) : this(comm, false) { }

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="comm">Object implementing the <see cref="ICommunication"/> interface for talking to the brick</param>
		/// <param name="alwaysSendEvents">Send events when data changes, or at every poll</param>
		public Brick(ICommunication comm, bool alwaysSendEvents)
		{
			_directCommand = new DirectCommand(this);
			_systemCommand = new SystemCommand(this);
			_batchCommand = new Command(this);

			Buttons = new BrickButtons();

			_alwaysSendEvents = alwaysSendEvents;

			int index = 0;

			_comm = comm ?? throw new ArgumentNullException(nameof(comm));

			Ports = new Dictionary<InputPort,Port>();

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
		/// <returns></returns>
		public IObservable<BrickChangedEventArgs> Connect(TimeSpan pollingTime) =>
			Observable.Create<BrickChangedEventArgs>(observer =>
			{
				var compositeDisposable =
					new CompositeDisposable();

				_comm
					.Connect()
					.Do(e => ResponseManager.HandleResponse(e.Report))
					.Subscribe()
					.DisposeWith(compositeDisposable);

				_directCommand.StopMotorAsync(OutputPort.All, false);

				Observable
					.Interval(pollingTime)
					.SelectMany(async _ =>
						{
							var result =
								await PollSensorsAsync();
							await _directCommand
								.StopMotorAsync(OutputPort.All, false);
							return result;
						})
					.Where(e => e != null)
					.Subscribe(observer)
					.DisposeWith(compositeDisposable);

				return compositeDisposable;
			});

		internal async Task SendCommandAsyncInternal(Command c)
		{
			await _comm.WriteAsync(c.ToBytes());
			if(c.CommandType == CommandType.DirectReply || c.CommandType == CommandType.SystemReply)
				await ResponseManager.WaitForResponseAsync(c.Response);
		}

		private async Task<BrickChangedEventArgs> PollSensorsAsync()
		{
			bool changed = false;
			const int responseSize = 11;
			int index = 0;

			Command c = new Command(CommandType.DirectReply, (8 * responseSize) + 6, 0);

			foreach(InputPort i in Enum.GetValues(typeof(InputPort)))
			{
				Port p = Ports[i];
				
				index = p.Index * responseSize;

				c.GetTypeMode(p.InputPort, (byte)index, (byte)(index+1));
				c.ReadySI(p.InputPort, p.Mode, (byte)(index+2));
				c.ReadyRaw(p.InputPort, p.Mode, (byte)(index+6));
				c.ReadyPercent(p.InputPort, p.Mode, (byte)(index+10));
			}

			index += responseSize;

			c.IsBrickButtonPressed(BrickButton.Back,  (byte)(index+0));
			c.IsBrickButtonPressed(BrickButton.Left,  (byte)(index+1));
			c.IsBrickButtonPressed(BrickButton.Up,    (byte)(index+2));
			c.IsBrickButtonPressed(BrickButton.Right, (byte)(index+3));
			c.IsBrickButtonPressed(BrickButton.Down,  (byte)(index+4));
			c.IsBrickButtonPressed(BrickButton.Enter, (byte)(index+5));

			await SendCommandAsyncInternal(c);
			if(c.Response.Data == null)
				return null;

			foreach(InputPort i in Enum.GetValues(typeof(InputPort)))
			{
				Port p = Ports[i];

				int type = c.Response.Data[(p.Index * responseSize)+0];
				byte mode = c.Response.Data[(p.Index * responseSize)+1];
				float siValue = BitConverter.ToSingle(c.Response.Data, (p.Index * responseSize)+2);
				int rawValue = BitConverter.ToInt32(c.Response.Data, (p.Index * responseSize)+6);
				byte percentValue = c.Response.Data[(p.Index * responseSize)+10];

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

			if(	Buttons.Back  != (c.Response.Data[index+0] == 1) ||
				Buttons.Left  != (c.Response.Data[index+1] == 1) ||
				Buttons.Up    != (c.Response.Data[index+2] == 1) ||
				Buttons.Right != (c.Response.Data[index+3] == 1) ||
				Buttons.Down  != (c.Response.Data[index+4] == 1) ||
				Buttons.Enter != (c.Response.Data[index+5] == 1)
			)
				changed = true;

			Buttons.Back	= (c.Response.Data[index+0] == 1);
			Buttons.Left	= (c.Response.Data[index+1] == 1);
			Buttons.Up		= (c.Response.Data[index+2] == 1);
			Buttons.Right	= (c.Response.Data[index+3] == 1);
			Buttons.Down	= (c.Response.Data[index+4] == 1);
			Buttons.Enter	= (c.Response.Data[index+5] == 1);

			return (changed || _alwaysSendEvents)
				? new BrickChangedEventArgs { Ports = this.Ports, Buttons = this.Buttons }
				: null;
		}
	}
}
