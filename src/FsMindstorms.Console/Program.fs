// Learn more about F# at http://fsharp.org

open System
open System
open System.Reactive.Linq
open RxMindstorms
open FsMindstorms.Domain
open FsMindstorms.Domain.Builder

[<EntryPoint>]
let main argv =

    let comm = UsbCommunication("EV3OLAV");
    let device = comm.FindBrick()
    printfn "I found this thing: %A" device
    let blah = device.GetMaxInputReportLength()
    let stream = device.Open()
    // let responseManager = ResponseManager();
    // let brick = Brick(comm, responseManager);

    // let connection = brick.Connect().Subscribe()

    // let snippet = mindstorms {
    //     // Turn (Motor OutputPort.A) With Power 50
    //     // Turn (Motors [ OutputPort.A; OutputPort.B ]) With Power 50
    //      TurnForTime 1000u (Motors [ OutputPort.A; OutputPort.B ]) With Power 50 Then Break
    //     // Step (Motors [ OutputPort.A; OutputPort.B ]) For 180u Steps With Power 50 Then Coast
    //     // StepSync (Motors [ OutputPort.A; OutputPort.B ]) For 180u Steps With Power 50 And TurnRatio 42s Then Coast
    //     // Start (Motor OutputPort.A)
    //     }

    // let cmd = createCommand brick snippet
    // brick.SendCommandAsync(cmd) |> ignore

    // // let x =
    //    brick
    //        .Connect()
    //        .Select(fun x -> x.Ports.[RxMindstorms.InputPort.One].RawValue)
    //        .Where(fun x -> x > 60)
    //        .SelectMany(fun x ->
    //            mindstorms {
    //                Stop (Motor OutputPort.A)
    //            }
    //            |> invokeCommand brick)
    //        .Subscribe()

    printfn "Press any key to continue..."
    Console.ReadKey() |> ignore   

    //connection.Dispose()

    0 // return an integer exit code
