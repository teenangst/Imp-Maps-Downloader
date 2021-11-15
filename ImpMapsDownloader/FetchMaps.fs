module FetchMaps

open Newtonsoft.Json

type Map = {map:string}
type Maps = {maps:Map[]}

let rec fetchMaps () =
  try
    let maps = (new System.Net.WebClient()).DownloadString("https://bot.tf2maps.net/api/maplist") |> JsonConvert.DeserializeObject<Maps>
    maps.maps |> Array.map (fun m -> m.map)
  with
  | _ ->
    System.Threading.Thread.Sleep 1000
    fetchMaps ()