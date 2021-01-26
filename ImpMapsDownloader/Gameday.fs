module Gameday

open Config
open System.Text
open Newtonsoft.Json
open System.Net
open System
open BlackFox.ColoredPrintf

let hashing = new System.Security.Cryptography.MD5CryptoServiceProvider() //Quick hash

let hashString (str:string) = //Used to version gameday lists, I am using a hash because it both versions and acts as a check to make sure the list is the latest version
  hashing.ComputeHash(Encoding.UTF8.GetBytes str) |> Array.map(fun x -> x.ToString("X2")) |> String.concat ""
  
type GameDayIndex (name:string, expire:int64, maps:string[], hash:string) =
  member val name = name
  member val expire = expire
  member val maps = maps
  member val hash = hash //maps |> String.concat "" |> hashString

let processURL (url:string) =
  if url.[url.Length-1] = '*' then
    sprintf "%s?%f" (url.Substring(0,url.Length-1)) (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds
  else
    url

let addGameDayToConfig name expire subscribed =
  //config.gameday <- config.gameday |> Array.append [|new GameDay(name, subscribed, expire, null)|]
  config <- {config with gameday=config.gameday |> Array.append [|{name=name;subscribed=subscribed;expire=expire;hash=null}|]}
  saveConfig ()

let rec check i = 
  let time:int64 = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds |> int64
  config.gamedayindexendpoint |> Array.iter (fun index -> //Iterate through indexes
    try
      let gdindex = (new WebClient()).DownloadString(index |> processURL) |> JsonConvert.DeserializeObject<GameDayIndex[]>
      gdindex |> Array.filter(fun gameday -> //Filter down lists that have changed
        try 
          let gd = (config.gameday |> Array.find(fun x -> (x.name = sprintf "%s:%s" (index |> hashString) (gameday.name))))
          gd.hash <> gameday.hash && gd.subscribed
        with
        | _ ->
          if gameday.expire = -1L || gameday.expire > time then
            colorprintf "$cyan[A gameday '%s' is available, would you like to subscribe to this list? Y/n]" (gameday.name)
            Environment.imp.Stop()
            if Console.ReadKey(true).Key = ConsoleKey.Y then
              Environment.imp.Start()
              colorprintfn "\n$green[Subscribing to %s]" gameday.name
              addGameDayToConfig (sprintf "%s:%s" (index |> hashString) (gameday.name)) (gameday.expire) true
              true
            else
              Environment.imp.Start()
              colorprintfn "\n$red[Not subscribing to %s]" gameday.name
              addGameDayToConfig (sprintf "%s:%s" (index |> hashString) (gameday.name)) (gameday.expire) false
              false
          else
            addGameDayToConfig (sprintf "%s:%s" (index |> hashString) (gameday.name)) (gameday.expire) false
            false
      )
      |> Array.iter (fun gameday -> //Process lists which have changed
        DownloadMap.downloadMaps (gameday.name) (gameday.maps)
        let newgd =
          config.gameday 
          |> Array.map (fun configgameday -> 
            if configgameday.name = sprintf "%s:%s" (index |> hashString) (gameday.name) then
              if (gameday.maps |> String.concat "" |> hashString) = gameday.hash then
                {name=configgameday.name;subscribed=configgameday.subscribed;expire=configgameday.expire;hash=gameday.maps |> String.concat "" |> hashString}
              else
                colorprintfn "$red[ERR06] : %A does not have the correct hash, please pass a path to the index into the GameDay Generator" (gameday.name)
                configgameday
            else
              configgameday
          )
        config <- {config with gameday=newgd}
        saveConfig ()
      )
    with
    | _ -> 
      if i = 2 then colorprintfn "$red[ERR05] : %A is not a valid gameday index" index
      else 
        System.Threading.Thread.Sleep 1000
        check (i+1)
  )