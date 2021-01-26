module FetchMaps

open Newtonsoft.Json

type Map = {map:string}

let rec fetchMaps () =
  try
    (new System.Net.WebClient()).DownloadString("https://api.skylarkx.uk/maplist?min") |> JsonConvert.DeserializeObject<Map []> |> Array.map (fun m -> m.map)
  with
  | _ ->
    System.Threading.Thread.Sleep 1000
    fetchMaps ()