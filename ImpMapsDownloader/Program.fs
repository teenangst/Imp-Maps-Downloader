﻿module AIMD

(*
TODO:
- Add custom thresholds
- Add ability to beep with imp notifications
- Auto-respond "no" after some time (5 minutes?)
- Some way to auto-join a server when server starts filling (maybe add ctrl+c to input a command, ctrl+c again to exit)
- When joining a map first check if it has been downloaded
*)

open System
open System.Net
open BlackFox.ColoredPrintf
open System.Diagnostics

let tick () =
  Gameday.check 0//Check for changes to gamedays
  FetchMaps.fetchMaps () |> DownloadMap.downloadMaps null //Check for new maps and then download
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
    
  //foo 1 2 ||> foo |> printfn "%A"
  (*Tick used to check for server activity*)
  if Config.config.askToJoinImp then
    Environment.imp.Interval <- 10000.
    Environment.imp.AutoReset <- false
    Environment.imp.Elapsed.AddHandler (fun _ _ -> imptick ())
    colorprintfn "Servers will be checked for map tests."
    imptick ()

  (*Tick used to fetch maps and gameday maps*)
  if Config.config.interval > 0. then
    Environment.interval.Interval <- (Config.config.interval |> max 1.) |> (*) 60000.
    Environment.interval.AutoReset <- false
    Environment.interval.Elapsed.AddHandler (fun _ _ -> tick ())

    colorprintfn "Maplist will be checked every %.1g minute%s $cyan[%s]" (Config.config.interval |> max 1.) (if (Config.config.interval |> max 1.) > 1. then "s" else "") (if Config.config.interval < 1. then "(fastest check is 1 minute)" else "")
    tick ()
    Environment.interval.Start ()
  else
    printfn "'interval' set to 0, will not refresh maplist"
    tick ()

  Environment.exitEvent.WaitOne() |> ignore
  colorprintfn "$red[Closing AIMD]"