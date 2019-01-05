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
            var brick =
                new Brick(comm);

            var obs =
                brick
                    .Connect()
                    .Select(x => x.Ports[InputPort.Three])
                    .Do(port => Console.WriteLine($"Sensor in port 3: ({port.Name}, {port.Type}, {port.RawValue})"));

            using (obs.Subscribe())
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }
    }
}
