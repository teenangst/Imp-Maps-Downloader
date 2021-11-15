module Environment

open System
open System.Diagnostics
open System.Threading

let versionendpoint = "https://skylarkx.uk/aimd/version"
let configendpoint = "https://skylarkx.uk/aimd/config"
let gamedayendpoint = "https://skylarkx.uk/aimd/gameday"

let rangemax = 3./24.
let rangemin = 1./24.

let AIMDVersion = "A7" //This is not also the config version, config version only increases when config has changed
let interval = new System.Timers.Timer()
let imp = new System.Timers.Timer()
let exitEvent = new ManualResetEvent(false)

let haltTimer func = //Pause timer from ticking while another action is taking place and return the function value
  interval.Stop ()
  imp.Stop()
  let ret = func ()
  interval.Start ()
  imp.Start()
  ret

let title name map (players:byte) (max:byte) imp =
  if name = null then
    Console.Title <- sprintf "Automatic Imp Maps Downloader %s" AIMDVersion
  else
    Console.Title <- sprintf "AIMD %s : %s%s (%s) [%i/%i]" AIMDVersion (if imp then "[imp] " else "") name map players max

let TF2Init () =
  if Process.GetProcessesByName("hl2").Length = 0 then
    System.Diagnostics.Process.Start("steam://rungameid/440") |> ignore