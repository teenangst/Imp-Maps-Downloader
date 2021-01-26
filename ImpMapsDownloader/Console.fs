module Con

open BlackFox.ColoredPrintf

let mutable queue:ColorPrintFormat<obj> list = []
let mutable inUse = false
let mutable serverip = ""

let server (message:ColorPrintFormat<obj>) = 0
  //serverip <- ip

let println (message:ColorPrintFormat<obj>) =
  queue <- queue |> List.append [message]