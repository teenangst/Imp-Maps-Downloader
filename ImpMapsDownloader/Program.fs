module AIMD

open System
open System.Net
open BlackFox.ColoredPrintf

let tick () =
  Gameday.check ()//Check for changes to gamedays
  FetchMaps.fetchMaps () |> DownloadMap.downloadMaps null //Check for new maps and then download
  Environment.interval.Start ()

let () =
  Console.Title <- sprintf "Automatic Imp Maps Downloader A%i" Environment.AIMDVersion
  
  Console.CancelKeyPress.AddHandler(fun _ e -> 
    e.Cancel <- true
    Environment.exitEvent.Set() |> ignore
  )

  (*Check for new version*)
  let latestVersion =
    try
      (new WebClient()).DownloadString(if Config.config.versionendpoint <> null then Config.config.versionendpoint else Config.defaultConfig.versionendpoint)
    with 
    | _ -> "failed"

  if latestVersion = "failed" then
    colorprintfn "$red[ERR04] : Unable to check if this is the latest version"
  else if latestVersion <> (sprintf "a%i" Environment.AIMDVersion) then
    colorprintfn "$yellow[There is a new version, %s, go to https://github.com/teenangst/Imp-Maps-Downloader and get the latest release.]" latestVersion
    Config.checkForConfigDifferences () |> ignore //Check for any Config.config recommendations

  (*Tick used to fetch maps and gameday maps*)
  if Config.config.interval > 0. then
    Environment.interval.Interval <- (Config.config.interval |> max 5.) |> (*) 600. //Fastest poll is 5 minutes
    Environment.interval.AutoReset <- false
    Environment.interval.Elapsed.AddHandler (fun _ _ -> tick ())

    colorprintfn "Maplist will be checked every %.1g minutes $cyan[%s]" (Config.config.interval |> max 5.) (if Config.config.interval < 5. then "(fastest check is 5 minutes)" else "")
    tick ()
    Environment.interval.Start ()
  else
    printfn "'interval' set to 0, will not refresh maplist"
    tick ()

  Environment.exitEvent.WaitOne() |> ignore
  colorprintfn "$red[Closing AIMD]"