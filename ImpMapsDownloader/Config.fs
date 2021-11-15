module Config

open System
open System.Net
open Newtonsoft.Json
open System.IO

open BlackFox.ColoredPrintf

type GameDay = {name:string; subscribed:bool; expire:int64; hash:string}
type Server = {ip:string; isimp:bool; name:string}
type Config = {gamedayindexendpoint: string[]; mapdirectory:string; cdn:string; interval:float; servers:Server[]; gameday:GameDay[]; checkChanges:bool; askToJoinImp:bool}

let masterConfigEndpoint = Environment.configendpoint

let defaultConfig = 
  try 
    (new WebClient()).DownloadString(masterConfigEndpoint) 
      |> JsonConvert.DeserializeObject<Config>
  with
  | _ -> 
    colorprintfn "$yellow[Download of master config failed, using hard coded version - this version may have issues.]"
    {
      gamedayindexendpoint=[|Environment.gamedayendpoint|];
      mapdirectory=null;
      cdn="https://redirect.tf2maps.net/maps/";
      interval=1.;
      servers=[|
        {ip="us.tf2maps.net:27015"; isimp=true; name="TF2 Maps US Server"};
        {ip="eu.tf2maps.net:27015"; isimp=true; name="TF2 Maps EU Server"};
        (*{ip="us.tf2maps.net:27016"; isimp=true; name="TF2 Maps MvM US Server"};
        {ip="eu.tf2maps.net:27016"; isimp=true; name="TF2 Maps MvM EU Server"};*)
      |];
      gameday=[||];
      checkChanges=true;
      askToJoinImp=false
    }
let ignoreConfigProperties = [|"gamedayindexendpoint";"mapdirectory";"interval";"servers";"gameday";"checkChanges";"askToJoinImp"|]

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
    if config.checkChanges then
      let mutable changes = false

      masterConfig
        |> Map.filter (fun k v ->
          match currentConfig |> Map.tryFind k with
          | Some cfgV when (cfgV |> JsonConvert.SerializeObject) <> (v |> JsonConvert.SerializeObject) && (ignoreConfigProperties |> Array.exists (fun x -> x = k) |> not) -> true
          | _ -> false
        )
        |> Map.iter (fun k v ->
          colorprintf "$red[Config entry '%s' doesn't match suggested value '%A'.\nNot changing this value may impact the function of this application.\nWould you like to update it? Y/n] " k (v |> JsonConvert.SerializeObject)
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
      colorprintfn "$cyan[Not checking for differences in config to master config, change `checkChanges` in config.json to `true` to check for changes.]"
  else
    saveConfig ()