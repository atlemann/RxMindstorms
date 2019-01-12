using System;
using System.Reactive.Linq;
using RxMindstorms.Core;

namespace RxMindstorms.App
{
    public static class Program
    {
        public static void Main(string[] _)
        {
            var comm =
                new UsbCommunication("EV3OLAV");
            var responseManager =
                new ResponseManager();
            var brick =
                new Brick(comm, responseManager);

            var obs =
                brick
                    .Connect()
                    .Select(x => x.Ports[InputPort.Three])
                    .Do(port => Console.WriteLine($"Sensor in port 3: ({port.Name}, {port.Type}, {port.RawValue})"));

            var command =
                brick.CreateCommand(CommandType.DirectNoReply, 0, 0);

            command.StartMotor(OutputPort.A);
            command.TurnMotorAtPowerForTime(OutputPort.A, 75, 1000, false);

            var subscription =
                brick
                    .SendCommand(command)
                    .Do(_ => Console.WriteLine("Waiting for command to finish..."))
                    .Subscribe();

            using (subscription)
            using (obs.Subscribe())
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }
    }
}
