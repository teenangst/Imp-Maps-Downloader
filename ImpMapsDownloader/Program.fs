open System
open System.IO
open System.Net
open System.Text.RegularExpressions

open HtmlAgilityPack
open HtmlAgilityPack.CssSelectors.NetCore

open BlackFox.ColoredPrintf

open Newtonsoft.Json


type Config (mapdirectory:string, maplistsite:string, selector:string, cdn:string, interval:float) = 
  member val mapdirectory = mapdirectory with get, set
  member val maplistsite = maplistsite
  member val selector = selector
  member val cdn = cdn
  member val interval = interval

let defaultConfig = 
  (new WebClient()).DownloadString("https://raw.githubusercontent.com/teenangst/Imp-Maps-Downloader/master/config.json") 
    |> JsonConvert.DeserializeObject<Config>
let ignoreConfigProperties = ["mapdirectory";"interval"]


let temp = Path.GetTempPath()
let interval = new Timers.Timer()

//let config = if File.Exists("config.json") then JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json")) else new Config(null, "https://bot.tf2maps.net/maplist.php", "body .row.mt-3 .col-12.mb-3 .card div table tr td:first-child a", "https://redirect.tf2maps.net/maps/", 5.)
let mutable config =
  if File.Exists "config.json" then
      "config.json"
      |> File.ReadAllText
      |> JsonConvert.DeserializeObject<Config>
  else
      defaultConfig
      
let saveConfig () = File.WriteAllText("config.json", JsonConvert.SerializeObject(config))

let fetchMaps () =
  let doc = new HtmlDocument()
  try 
    doc.LoadHtml((new WebClient()).DownloadString(config.maplistsite))
    doc.QuerySelectorAll(config.selector) |> Seq.toArray |> Array.map (fun x -> x.InnerHtml)
  with
  | _ -> 
    colorprintfn "$red[ERR03] : HtmlAgilityPack error"
    [||]

//tf\download\maps directory, with fallback if that folder isn't in the normal place
let rec verifyMapDirectory () =
  if config.mapdirectory <> null then
    if Directory.Exists config.mapdirectory |> not then
      colorprintfn "$red[ERR02] : Defined tf\download\maps directory not found, please enter the full path of your maps download folder"
      colorprintf "> $cyan"
      config.mapdirectory <- Console.ReadLine()
      saveConfig()
      verifyMapDirectory()
      else config.mapdirectory
  else
    if Directory.Exists @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2\tf\download\maps\" |> not then
      colorprintfn @"$red[ERR01] : tf\download\maps directory not found, please enter the full path of your maps download folder"
      colorprintf "> $cyan"
      config.mapdirectory <- Console.ReadLine()
      saveConfig()
      verifyMapDirectory()
    else @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2\tf\download\maps\"
let mutable downloadMapDirectory = verifyMapDirectory()

let rec fetchDownloadedMaps () = 
  try
    Directory.GetFiles downloadMapDirectory |> Array.map (fun x -> (new Regex("[^\\\\]*(?=\.bsp)")).Match(x).Value)
  with
  | _ -> 
    downloadMapDirectory <- verifyMapDirectory()
    fetchDownloadedMaps()
let mutable maps:string[] = fetchDownloadedMaps ()

let downloadMap map = 
  if (maps |> Array.contains map || File.Exists (Path.Combine(downloadMapDirectory, sprintf "%s.bsp" map))) |> not then
    colorprintf "$yellow[Downloading] %s" map
    try
      (new WebClient()).DownloadFile(sprintf "%s%s.bsp.bz2" config.cdn map, sprintf "%s%s.bsp.bz2" temp map)
      Console.CursorLeft <- 0
      colorprintf "$cyan[Unzipping]   %s" map
      ICSharpCode.SharpZipLib.BZip2.BZip2.Decompress(
        new FileStream(sprintf "%s%s.bsp.bz2" temp map, FileMode.Open), 
        new FileStream(Path.Combine(downloadMapDirectory, sprintf "%s.bsp" map), FileMode.CreateNew), 
        true)
      Console.CursorLeft <- 0
      colorprintfn "$green[Complete]    %s" map
    with
    | _ -> 
      Console.CursorLeft <- 0
      colorprintfn "$red[Invalid]     %s" map
  //else
  //  colorprintfn "$red[Skipping]    %s" map
let downloadMaps maps = maps |> Array.iter (fun x -> downloadMap x)

let checkForConfigDifferences () =
  if File.Exists "config.json" then
    let masterConfig = 
      (new WebClient()).DownloadString("https://raw.githubusercontent.com/teenangst/Imp-Maps-Downloader/master/config.json") 
        |> JsonConvert.DeserializeObject<Map<string, obj>>
    let mutable currentConfig = 
      "config.json"
        |> File.ReadAllText
        |> JsonConvert.DeserializeObject<Map<string, obj>>
    let mutable changes = false

    masterConfig
      |> Map.filter (fun k v ->
        match currentConfig |> Map.tryFind k with
        | Some cfgV when cfgV <> v && (ignoreConfigProperties |> List.exists (fun x -> x = k) |> not) -> true
        | _ -> false
      )
      |> Map.iter (fun k v ->
        colorprintf "$red[Config entry '%s' doesn't match suggested value '%A'.\nNot changing this value may impact the function of this application.\nWould you like to update it? Y/n] " k v
        if Console.ReadKey(true).Key = ConsoleKey.Y then
          colorprintfn "\n$green[Changing '%s']" k
          currentConfig <- currentConfig |> Map.map (fun ik iv -> if ik = k then v else iv)
          changes <- true
        else
          colorprintfn "\n$yellow[Not changing '%s']" k
      )

    if changes then
      config <- //This is disgusting, but basically the only way to solve this issue. Spent over an hour figuring out solutions and this allow
        currentConfig
        |> JsonConvert.SerializeObject
        |> JsonConvert.DeserializeObject<Config>

let () =
  Console.Title <- "Automatic Imp Maps Downloader A3"
  let latestVersion =
    try
      (new WebClient()).DownloadString("https://raw.githubusercontent.com/teenangst/Imp-Maps-Downloader/master/version.txt")
    with 
    | _ -> "failed"

  if latestVersion = "failed" then
    colorprintfn "$red[ERR04] : Unable to check if this is the latest version, GitHub is down"
  else if latestVersion <> "a3" then
    colorprintfn "$yellow[There is a new version, %s, go to https://github.com/teenangst/Imp-Maps-Downloader and get the latest release.]" latestVersion
    checkForConfigDifferences () |> ignore

  saveConfig ()

  if config.interval > 0. then
    interval.Interval <- (config.interval |> max 5.) |> (*) 60000. //Fastest poll is 5 minutes
    interval.AutoReset <- true
    interval.Elapsed.AddHandler (fun _ _ -> fetchMaps() |> downloadMaps)
    interval.Enabled <- true

    colorprintfn "Maplist will be checked every %.1g minutes $cyan[%s]" (config.interval |> max 5.) (if config.interval < 5. then "(fastest check is 5 minutes)" else "")
  else
    printfn "'interval' set to 0, will not refresh maplist"
  
  fetchMaps() |> downloadMaps

  Console.ReadKey true |> ignore
