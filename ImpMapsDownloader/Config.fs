module Config

open System
open System.Net
open Newtonsoft.Json
open System.IO

open BlackFox.ColoredPrintf

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
  | _ -> new Config("https://raw.githubusercontent.com/teenangst/Imp-Maps-Downloader/master/version.txt", [|"https://raw.githubusercontent.com/teenangst/Imp-Maps-Downloader/master/gameday.json"|], null, "https://bot.tf2maps.net/maplist.php", "body .row.mt-3 .col-12.mb-3 .card div table tr td:first-child a", "https://redirect.tf2maps.net/maps/", 5., [||])
let ignoreConfigProperties = [|"gamedayindexendpoint";"mapdirectory";"interval";"gameday"|]

let mutable config =
  if File.Exists "config.json" then
      "config.json"
      |> File.ReadAllText
      |> JsonConvert.DeserializeObject<Config>
  else
      defaultConfig
      
let saveConfig () = File.WriteAllText("config.json", JsonConvert.SerializeObject(config, Formatting.Indented))

let checkForConfigDifferences () =
  if File.Exists "config.json" then
    let masterConfig = 
      (new WebClient()).DownloadString(masterConfigEndpoint) 
        |> JsonConvert.DeserializeObject<Map<string, obj>>
    let mutable currentConfig = 
      "config.json"
        |> File.ReadAllText
        |> JsonConvert.DeserializeObject<Map<string, obj>>

    let mutable changes = false

    masterConfig
      |> Map.filter (fun k v ->
        match currentConfig |> Map.tryFind k with
        | Some cfgV when (cfgV |> JsonConvert.SerializeObject) <> (v |> JsonConvert.SerializeObject) && (ignoreConfigProperties |> Array.exists (fun x -> x = k) |> not) -> true
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
      config <- //This is disgusting, but basically the only way to solve this issue. Spent over an hour figuring out solutions and this was the best option
        currentConfig
        |> JsonConvert.SerializeObject
        |> JsonConvert.DeserializeObject<Config>
      saveConfig ()
  else
    saveConfig ()