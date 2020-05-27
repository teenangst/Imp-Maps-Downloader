module Environment

open System.Threading

let AIMDVersion = "4a" //This is not also the config version, config version only increases when config has changed
let interval = new System.Timers.Timer()
let exitEvent = new ManualResetEvent(false)

let haltTimer func = //Pause timer from ticking while another action is taking place and return the function value
  interval.Stop ()
  let ret = func ()
  interval.Start ()
  ret