open System
open System.Text.RegularExpressions
open System.IO
open BlackFox.ColoredPrintf
open System.Text
open Newtonsoft.Json
open System.Net

let exitEvent = new System.Threading.ManualResetEvent(false)

let hashing = new System.Security.Cryptography.MD5CryptoServiceProvider() //Quick hash

let hashString (str:string) = //Used to version gameday lists, I am using a hash because it both versions and acts as a check to make sure the list is the latest version
  hashing.ComputeHash(Encoding.UTF8.GetBytes str) |> Array.map(fun x -> x.ToString("X2")) |> String.concat ""

module regexp =
  let relative = new Regex("(?<value>\d+)(?<modifier>[dwmy])")
  let exact = new Regex("(?:(?<year>(?:\d{2}){1,2})[\-\/\.])?(?<month>\d{1,2})[\-\/\.](?<day>\d{1,2})")
  let epoch = new Regex("^\d{0,19}$")
  let json = new Regex("\.json$")

module Time =
  let second = 1000L
  let minute = 60L * second
  let hour = 60L * minute
  let day = 24L * hour
  let week = 7L * day
  let month = 28L * day

type GameDayIndex (name:string, expire:int64, maps:string[]) =
  member val name = name
  member val expire = expire
  member val maps = maps
  member val hash = maps |> String.concat "" |> hashString

type GameDay (name:string, subscribed:bool, expire:int64, hash:string) =
  member val name = name
  member val subscribed = subscribed
  member val expire = expire
  member val hash = hash

type Config (versionendpoint:string, gamedayindexendpoint: string[], mapdirectory:string, maplistsite:string, selector:string, cdn:string, interval:float, gameday:GameDay[]) = 
  member val versionendpoint = versionendpoint
  member val gamedayindexendpoint = gamedayindexendpoint
  member val mapdirectory = mapdirectory with get, set
  member val maplistsite = maplistsite
  member val selector = selector
  member val cdn = cdn
  member val interval = interval
  member val gameday = gameday with get, set
  
let masterConfigEndpoint = "https://pastebin.com/raw/8QsViGru"

let defaultConfig = 
  try 
    (new WebClient()).DownloadString(masterConfigEndpoint) 
      |> JsonConvert.DeserializeObject<Config>
  with
  | _ -> new Config("https://pastebin.com/raw/a6mLNJu0", [|"https://pastebin.com/raw/0UZZJkR0*"|], null, "https://bot.tf2maps.net/maplist.php", "body .row.mt-3 .col-12.mb-3 .card div table tr td:first-child a", "https://redirect.tf2maps.net/maps/", 5., [||])
let ignoreConfigProperties = [|"gamedayindexendpoint";"mapdirectory";"interval";"gameday"|]

let mutable config =
  if File.Exists "config.json" then
      "config.json"
      |> File.ReadAllText
      |> JsonConvert.DeserializeObject<Config>
  else
      defaultConfig

let processTime time:int64 =
  let timenow:int64 = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds |> int64
  match time with
  | t when regexp.relative.IsMatch(t) ->
    let modifier = 
      match (regexp.relative.Match(t).Groups |> Seq.cast<Group> |> Seq.find(fun x -> x.Name = "modifier")).Value with
      | "d" -> Time.day
      | "w" -> Time.week
      | "m" -> Time.month
      | _ -> -2L
    let value : int64 = (regexp.relative.Match(time).Groups |> Seq.cast<Group> |> Seq.find(fun x -> x.Name = "value")).Value |> int64
    timenow + (value * modifier)
  | t when regexp.exact.IsMatch(t) ->
    try
      let groups = regexp.exact.Match(t).Groups |> Seq.cast<Group>
      let tempyear = (groups |> Seq.find(fun x -> x.Name = "year")).Value
      let year = if tempyear = "" then DateTime.UtcNow.Year else if (tempyear |> int) < 100 then tempyear |> int |> (+) 2000 else tempyear |> int
      let month = (groups |> Seq.find(fun x -> x.Name = "month")).Value |> int
      let day = (groups |> Seq.find(fun x -> x.Name = "day")).Value |> int
      (DateTime(year, month, day).Subtract(new DateTime(1970, 1, 1))).TotalSeconds |> int64
    with
    | _ -> -3L
  | t when regexp.epoch.IsMatch(t) ->
    try
      t |> int64
    with
    | _ -> -4L
  | _ -> -1L

let fetchInput () =
  colorprintf "> $cyan"
  Console.ReadLine()

let () = 
  Console.Title <- "AIMD GameDay Generator"
  
  Console.CancelKeyPress.AddHandler(fun _ e -> 
    e.Cancel <- true
    exitEvent.Set() |> ignore
  )
  let rec getAndVerifyPath ():string[] =
    colorprintfn "$yellow[Enter path to a folder of maps, or a file with map names, or a JSON file which needs the hash updating:]"
    let _path = fetchInput ()
    if (File.Exists _path || Directory.Exists _path) |> not then 
      colorprintfn "$red[Folder or file does not exist]"
      getAndVerifyPath()
    else
      let mutable failed = 0
      let mutable maps = [||]
      match _path with
      | p when File.Exists p && regexp.json.IsMatch p -> 
        try 
          File.WriteAllText(p, JsonConvert.SerializeObject( File.ReadAllText p |> JsonConvert.DeserializeObject<GameDayIndex[]>, Formatting.Indented))
          
          colorprintfn "$green[Updated hash]"
        with
        | _ ->
          colorprintfn "$red[JSON file not valid]"
          failed <- -1
        ignore |> ignore
      | p when File.Exists p -> 
        maps <- 
          File.ReadAllLines p
          |> Array.filter (fun x -> x <> "")
          |> Array.map (fun x -> x.Trim ())
        colorprintfn "$cyan[Checking %i entries]" (maps.Length)
        try 
          maps |> Array.iter(fun map ->
            let req = WebRequest.Create(Path.Combine(config.cdn, sprintf "%s.bsp.bz2" map))
            try
              req.Method = "HEAD" |> ignore
              req.GetResponse () |> ignore
              colorprintfn "$green[/] $white[%s]" map
            with 
            | _ -> 
              colorprintfn "$red[× %s]" map
              failed <- failed + 1
            req.Abort()
          )
        with
        | _ -> 
          colorprintfn "$red[Invalid map name in file]"
          failed <- -1
        
      | p when Directory.Exists p -> 
        maps <- 
          Directory.GetFiles p 
          |> Array.map (fun x -> (new Regex("[^\\\\]*(?=\.bsp)")).Match(x).Value) 
          |> Array.filter (fun x -> x <> "")
        maps |> Array.iter(fun map ->
          let req = WebRequest.Create(Path.Combine(config.cdn, sprintf "%s.bsp.bz2" map))
          try
            req.Method = "HEAD" |> ignore
            req.GetResponse () |> ignore
            colorprintfn "$green[/] $white[%s]" map
          with 
          | _ -> 
            colorprintfn "$red[× %s]" map
            failed <- failed + 1
          req.Abort()
        )
      | _ -> ignore |> ignore


      if failed > 0 then
        colorprintfn "$red[%i maps are not available]" failed
        getAndVerifyPath ()
      else if failed < 0 then
        getAndVerifyPath ()
      else
        maps


  let rec getAndVerifyDate () =
    colorprintfn "$yellow[Enter date for when the GameDay should stop being notified (see readme.md for help)]"
    let _date = fetchInput () |> processTime
    if _date < 0L then 
      colorprintfn "$red[Invalid date, see readme.md for help]"
      getAndVerifyDate()
    else _date

  let maps = getAndVerifyPath ()
  if maps.Length > 0 then
    colorprintfn "$yellow[Enter the name for the GameDay]"
    let name = fetchInput ()
    let date = getAndVerifyDate ()

    let gamedayindex = [|new GameDayIndex(name, date, maps)|]
    let output = JsonConvert.SerializeObject(gamedayindex, Formatting.Indented)
    colorprintfn "$cyan[%s]" output
    File.WriteAllText(Path.Combine(Path.GetTempPath(), "AIMDGameDayOutput.json"), output)
    System.Diagnostics.Process.Start (Path.Combine(Path.GetTempPath(), "AIMDGameDayOutput.json")) |> ignore


  exitEvent.WaitOne() |> ignore