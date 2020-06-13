module Imp

open Config

open System.Net
open System.Net.Sockets
open System.IO
open System.Text
open BlackFox.ColoredPrintf
open System

//https://developer.valvesoftware.com/wiki/Server_queries

//System.Diagnostics.Process.Start("steam://rungameid/440")
//System.Diagnostics.Process.Start("steam://connect/eu.tf2maps.net:27015")

let servers:Map<string,IPEndPoint> = 
  config.servers
  |> Array.map(fun x -> 
    try 
      if x.ip.Split(':').Length = 2 then
        (x.ip, new IPEndPoint((Dns.GetHostAddresses (x.ip.Split(':').[0])).[0], x.ip.Split(':').[1] |> int))
      else
        colorprintfn "$red[ERR07] : Server ip `%s` requires a port." x.ip
        (x.ip, null)
    with
    | _ ->
      colorprintfn "$red[ERR08] : Server `%s` dns does not resolve." x.ip
      (x.ip, null)
  )
  |> Array.filter (fun (_, x) -> x <> null)
  |> Map.ofArray

type ServerInfo (fullness:float, players:byte, maxPlayers:byte, name:string, map:string, isImp:bool, ip:string) =
  member val Fullness = fullness
  member val Players = players
  member val MaxPlayers = maxPlayers
  member val Name = name
  member val Map = map
  member val IsImp = isImp
  member val IP = ip

let mutable activeservers:Map<string,ServerInfo> = config.servers |> Array.map(fun x -> (x.ip, new ServerInfo(-1.,0uy,0uy,x.name, null, x.isimp, x.ip))) |> Map.ofArray
let mutable index = 0

type ExtraDataFlags = GameID = 0x01 | SteamID = 0x10 | Keywords = 0x20 | Spectator = 0x40 | Port = 0x80
type VACFlags = Unsecured = 0 | Secured = 1
type VisibilityFlags = Public = 0 | Private = 1
type EnvironmentFlags = Linux = 0x6C | Windows = 0x77 | Max = 0x6D | MacOsX = 0x6F
type ServerTypeFlags = Dedicated = 0x64 | Nondedicated = 0x6C | SourceTV = 0x70

type Info (header:byte, protocol:byte, name:string, map:string, folder:string, game:string, id:int16, players:byte, maxPlayers:byte, bots:byte(*, serverType:ServerTypeFlags, environment:EnvironmentFlags, visibility:VisibilityFlags, vac:VACFlags, version: string, extraDataFlag:ExtraDataFlags, gameID:uint64, steamID:uint64, keywords:string, spectator:string, spectatorPort:int16, port:int16*)) =
  member val Header = header
  member val Protocol = protocol
  member val Name = name
  member val Map = map
  member val Folder = folder
  member val Game = game
  member val ID = id
  member val Players = players
  member val MaxPlayers = maxPlayers
  member val Bots = bots
  //member val ServerType = serverType
  //member val Environment = environment
  //member val Visibility = visibility
  //member val VAC = vac
  //member val Version = version
  //member val ExtraDataFlag = extraDataFlag
  //member val GameID = gameID
  //member val SteamID = steamID
  //member val Keywords = keywords
  //member val Spectator = spectator
  //member val SpectatorPort = spectatorPort
  //member val Port = port

let ReadNullTerminatedString (br:BinaryReader) =
  let sb = new StringBuilder()
  let mutable read = br.ReadChar()
  while read <> '\x00' do
    sb.Append read |> ignore
    read <- br.ReadChar()
  sb.ToString()

let INFO_REQUEST = [|
  0xFFuy; 0xFFuy; 0xFFuy; 0xFFuy; 0x54uy; 
  0x53uy; 0x6Fuy; 0x75uy; 0x72uy; 0x63uy; 0x65uy; 0x20uy; 0x45uy; 0x6Euy; 0x67uy; 0x69uy; 0x6Euy; 0x65uy; 0x20uy; 0x51uy; 0x75uy; 0x65uy; 0x72uy; 0x79uy; //"Source Engine Query."
  0x00uy
  |]

let PLAYER_CHALLENGE_REQUEST = [|0xFFuy; 0xFFuy; 0xFFuy; 0xFFuy; 0x55uy; 0xFFuy; 0xFFuy; 0xFFuy; 0xFFuy|]

let checkServer (server:Server) =
  //colorprintfn "$blue[Checking %s [%s\]]" server.name (server.ip)
  try
    let udp = new UdpClient()
    udp.Client.SendTimeout <- 5000
    udp.Client.ReceiveTimeout <- 5000
    udp.Send(INFO_REQUEST, INFO_REQUEST.Length, servers.[server.ip]) |> ignore
    let br = udp.Receive(ref servers.[server.ip]) |> (fun x -> new MemoryStream(x)) |> (fun x -> new BinaryReader(x))
    let info =
      new Info(
        br.ReadByte(),
        br.ReadByte(),
        ReadNullTerminatedString(br),
        ReadNullTerminatedString(br),
        ReadNullTerminatedString(br),
        ReadNullTerminatedString(br),
        br.ReadInt16(),
        br.ReadByte(), //Players
        br.ReadByte(), //MaxPlayers
        br.ReadByte() //Bots
      )

    let mutable fullness = if info.Players = 0uy then 0. else float info.Players / float info.MaxPlayers
    if fullness >= Environment.rangemax && activeservers.[server.ip].Fullness = -1. then //If 30% full and hasn't been marked as active before
      colorprintf "$cyan[Players are joining %s`%s`, would you like to join?] ($yellow[%s]) [$yellow[%i]/$yellow[%i]] Y/n " (if server.isimp then "imp " else "") server.name info.Map info.Players info.MaxPlayers
      Environment.interval.Stop()
      if Console.ReadKey(true).Key = ConsoleKey.Y then
        colorprintfn "\n$green[Joining `%s`]" server.name
        System.Diagnostics.Process.Start(sprintf "steam://connect/%s" server.ip) |> ignore
        if info.Players >= info.MaxPlayers then
          Environment.TF2Init ()
      else 
        colorprintfn "\n$yellow[Not joining `%s`]" server.name
      Environment.interval.Start()
      activeservers <- activeservers |> Map.map (fun ip si -> if ip = server.ip then new ServerInfo(fullness,info.Players,info.MaxPlayers,server.name, info.Map, server.isimp, server.ip) else si)
    else if fullness <= Environment.rangemin && activeservers.[server.ip].Fullness <> -1. then
      colorprintfn "$blue[Server `%s` has emptied]" server.name
      activeservers <- activeservers |> Map.map (fun ip si -> if ip = server.ip then new ServerInfo(-1.,info.Players,info.MaxPlayers,server.name, info.Map, server.isimp, server.ip) else si)
    if fullness >= Environment.rangemax && activeservers.[server.ip].Fullness <> -1. then
      activeservers <- activeservers |> Map.map (fun ip si -> if ip = server.ip then new ServerInfo(fullness,info.Players,info.MaxPlayers,server.name, info.Map, server.isimp, server.ip) else si)
    ignore 0
  with
  | _ -> ignore 0
  

let checkServers () =
  config.servers |> Array.iter (fun x -> checkServer x)
  let servers = activeservers |> Map.filter (fun _ si -> si.Fullness > 0.)
  if servers.Count = 0 then
    Environment.title null null 0uy 0uy false
  else
    File.WriteAllText("debug.json", Newtonsoft.Json.JsonConvert.SerializeObject(servers, Newtonsoft.Json.Formatting.Indented))
    index <- (index + 1) % servers.Count
    let selected = servers.[(servers |> Map.toArray |> Array.map(fun (ip,si) -> ip)).[index]]
    Environment.title selected.Name selected.Map selected.Players selected.MaxPlayers selected.IsImp