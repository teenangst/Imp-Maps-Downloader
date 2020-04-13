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
  member val maplistsite = maplistsite with get, set
  member val selector = selector with get, set
  member val cdn = cdn with get, set
  member val interval = interval with get, set

let temp = Path.GetTempPath()

let interval = new Timers.Timer()

let config = if File.Exists("config.json") then JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json")) else new Config(null, "https://bot.tf2maps.net/maplist.php", "body .row.mt-3 .col-12.mb-3 .card div table tr td:first-child a", "https://cdn.tf2maps.net/maps/", 5.)
let saveConfig () = File.WriteAllText("config.json", JsonConvert.SerializeObject(config))

let fetchMaps () =
  try 
    let doc = new HtmlDocument()
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
let downloadMaps maps = maps |> Array.iter (fun x -> downloadMap x)

[<EntryPoint>]
let main argv =
  Console.Title <- "Automatic imp map downloader A1"
  let latestVersion = (new WebClient()).DownloadString("https://raw.githubusercontent.com/teenangst/Imp-Maps-Downloader/master/version.txt")
  if latestVersion <> "a1" then
    colorprintfn "$red[There is a new version, %s, go to https://github.com/teenangst/Imp-Maps-Downloader and get the latest release.]" latestVersion
  saveConfig ()
  if config.interval > 0. then
    interval.Interval <- (config.interval |> max 5.) |> (*) 60000. //Fastest poll is 5 minutes
    interval.AutoReset <- true
    interval.Elapsed.AddHandler (fun _ _ -> fetchMaps() |> downloadMaps)
    interval.Enabled <- true

    colorprintfn "Maplist will be checked every %.1g minutes $cyan[%s]" (config.interval |> max 5.) (if config.interval < 5. then "(fastest check is 5 minutes)" else "")
  else
    printfn "interval set to 0, will not refresh maplist"
  
  fetchMaps() |> downloadMaps

  Console.ReadKey true |> ignore
  0
