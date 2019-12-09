module FsMindstorms.Domain

open RxMindstorms

type OutputPort =
    | A
    | B
    | C
    | D
    | All

type InputPort =
    | One
    | Two
    | Three
    | Four
    | A
    | B
    | C
    | D

type DeviceType =
    | LMotor
    | MMotor
    | Touch
    | Color
    | Ultrasonic
    | Gyroscope
    | Infrared
    | Initializing
    | Empty
    | WrongPort
    | Unknown

module OutputPort =
    let toEnum =
        function
        | OutputPort.A -> RxMindstorms.OutputPort.A
        | OutputPort.B -> RxMindstorms.OutputPort.B
        | OutputPort.C -> RxMindstorms.OutputPort.C
        | OutputPort.D -> RxMindstorms.OutputPort.D
        | OutputPort.All -> RxMindstorms.OutputPort.All

module InputPort =
    let toEnum = function
        | InputPort.One -> RxMindstorms.InputPort.One
        | InputPort.Two -> RxMindstorms.InputPort.Two
        | InputPort.Three -> RxMindstorms.InputPort.Three
        | InputPort.Four -> RxMindstorms.InputPort.Four
        | InputPort.A -> RxMindstorms.InputPort.A
        | InputPort.B -> RxMindstorms.InputPort.B
        | InputPort.C -> RxMindstorms.InputPort.C
        | InputPort.D -> RxMindstorms.InputPort.D

module DeviceType =
    let toEnum = function
        | LMotor -> RxMindstorms.DeviceType.LMotor
        | MMotor -> RxMindstorms.DeviceType.MMotor
        | Touch -> RxMindstorms.DeviceType.Touch
        | Color -> RxMindstorms.DeviceType.Color
        | Ultrasonic -> RxMindstorms.DeviceType.Ultrasonic
        | Gyroscope -> RxMindstorms.DeviceType.Gyroscope
        | Infrared -> RxMindstorms.DeviceType.Infrared
        | Initializing -> RxMindstorms.DeviceType.Initializing
        | Empty -> RxMindstorms.DeviceType.Empty
        | WrongPort -> RxMindstorms.DeviceType.WrongPort
        | Unknown -> RxMindstorms.DeviceType.Unknown

type BreakMode =
    | Break
    | Coast

module BreakMode =
    let asBool = function
        | Break -> true
        | Coast -> false
    
type BrickAction =
    | MotorAction of OutputPort list * MotorAction

and MotorAction =
    | StartMotor
    | StopMotor
    | StepMotorAtPower of {| Power:int; Steps:uint32; Break:BreakMode |}
    | TurnMotorAtPower of {| Power:int |}
    | TurnMotorAtPowerForTime of {| Power:int; Time:uint32; Break:BreakMode |}
    | StepMotorSync of {| Power:int; TurnRatio:int16; Steps:uint32; Break:BreakMode |}

let updateCommand : Command -> BrickAction -> Command =
    fun command actions ->
    match actions with
    | MotorAction (port, action) ->
        let ports =
            port
            |> List.map OutputPort.toEnum
            |> List.reduce (|||)
        
        match action with
        | StartMotor -> command.StartMotor(ports)
        | StopMotor -> command.StopMotor(ports, true)
        | TurnMotorAtPower x -> command.TurnMotorAtPower(ports, x.Power)
        | TurnMotorAtPowerForTime x -> command.TurnMotorAtPowerForTime(ports, x.Power, x.Time, x.Break |> BreakMode.asBool)
        | StepMotorAtPower x -> command.StepMotorAtPower(ports, x.Power, x.Steps, x.Break |> BreakMode.asBool)
        | StepMotorSync x -> command.StepMotorSync(ports, x.Power, x.TurnRatio, x.Steps, x.Break |> BreakMode.asBool)
        
    command

module Builder =
    
    type MotorPorts =
        | Motor of OutputPort
        | Motors of OutputPort list

    module MotorPorts =
        let get = function
            | Motor m -> [ m ]
            | Motors ms -> ms

    type Power = Power
    type Steps = Steps
    type TurnRatio = TurnRatio

    type And = And
    type Then = Then
    type With = With
    type For = For

    type MindstormsBuilder() =
        member __.Yield(_) = Seq.empty

        [<CustomOperation("Start")>]
        member __.Start(currentState, motor:MotorPorts) =
            seq {
                yield! currentState
                yield MotorAction (MotorPorts.get motor, StartMotor)
            }

        [<CustomOperation("Stop")>]
        member __.Stop(currentState, motor:MotorPorts) =
            seq {
                yield! currentState
                yield MotorAction (MotorPorts.get motor, StopMotor)
            }
            
        [<CustomOperation("Turn")>]
        member __.Turn(currentState, motor:MotorPorts, _:With, _:Power, power:int) =
            seq {
                yield! currentState
                yield MotorAction (MotorPorts.get motor, TurnMotorAtPower {| Power = power |})
            }

        [<CustomOperation("TurnForTime")>]
        member __.TurnForTime(currentState, time:uint32, motor:MotorPorts, _:With, _:Power, power:int, _:Then, breakMode:BreakMode) =
            seq {
                yield! currentState
                yield MotorAction (MotorPorts.get motor, TurnMotorAtPowerForTime {| Power = power; Time = time; Break = breakMode |})
            }
            
        [<CustomOperation("Step")>]
        member __.Step(currentState, motor:MotorPorts, _:For, steps:uint32, _:Steps, _:With, _:Power, power:int, _:Then, breakMode:BreakMode) =
            seq {
                yield! currentState
                yield MotorAction (MotorPorts.get motor, StepMotorAtPower {| Power = power; Steps = steps; Break = breakMode |})
            }

        [<CustomOperation("StepSync")>]
        member __.StepSync(currentState, motor:MotorPorts, _:For, steps:uint32, _:Steps, _:With, _:Power, power:int, _:And, _:TurnRatio, turnRatio:int16, _:Then, breakMode:BreakMode) =
            seq {
                yield! currentState
                yield MotorAction (MotorPorts.get motor, StepMotorSync {| Power= power; TurnRatio = turnRatio; Steps = steps; Break = breakMode |})
            }
            
    let mindstorms = MindstormsBuilder()

open System.Threading.Tasks

let createCommand : Brick -> BrickAction seq -> Command =
    fun brick actions ->
    let cmd = brick.CreateCommand(CommandType.DirectNoReply)
    Seq.fold updateCommand cmd actions

let invokeCommand : Brick -> BrickAction seq -> Task =
    fun brick actions ->
    createCommand brick actions
    |> brick.SendCommandAsync
    
