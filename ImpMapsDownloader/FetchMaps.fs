module FetchMaps

open HtmlAgilityPack
open HtmlAgilityPack.CssSelectors.NetCore

let rec fetchMaps () =
  let doc = new HtmlDocument()
  try 
    doc.LoadHtml((new System.Net.WebClient()).DownloadString(Config.config.maplistsite))
    doc.QuerySelectorAll(Config.config.selector) |> Seq.toArray |> Array.map (fun x -> x.InnerHtml)
  with
  | _ -> 
    fetchMaps ()