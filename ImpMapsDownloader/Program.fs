module AIMD

open System
open System.Net
open BlackFox.ColoredPrintf
open System.Diagnostics

let tick () =
  FetchMaps.fetchMaps () |> DownloadMap.downloadMaps null //Check for new maps and then download
  Gameday.check 0//Check for changes to gamedays
  Environment.interval.Start ()

let imptick () =
  Imp.checkServers ()
  Environment.imp.Start ()

let () =
  Environment.title null null 0uy 0uy false
  
  Console.CancelKeyPress.AddHandler(fun _ e -> 
    e.Cancel <- true
    Environment.exitEvent.Set() |> ignore
  )

  (*Check for new version*)
  let latestVersion =
    try
      (new WebClient()).DownloadString(Environment.versionendpoint)
    with 
    | _ -> "failed"
  Config.saveConfig()
  if latestVersion = "failed" then
    colorprintfn "$red[ERR04] : Unable to check if this is the latest version"
  else if latestVersion <> (sprintf "%s" Environment.AIMDVersion) then
    colorprintfn "$yellow[There is a new version, %s, go to https://skylarkx.uk/aimd/releases and get the latest release.]" latestVersion
  Config.checkForConfigDifferences () |> ignore //Check for any Config.config recommendations
    
  (*Tick used to check for server activity*)
  if Config.config.askToJoinImp then
    Environment.imp.Interval <- 10000.
    Environment.imp.AutoReset <- false
    Environment.imp.Elapsed.AddHandler (fun _ _ -> imptick ())
    colorprintfn "Servers will be checked for imps."
    imptick ()

  (*Tick used to fetch maps and gameday maps*)
  if Config.config.interval > 0. then
    Environment.interval.Interval <- (Config.config.interval |> max 5.) |> (*) 60000. //REMOVE Fastest poll is 5 minutes
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