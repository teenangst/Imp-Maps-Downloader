﻿module Gameday

open Config
open System.Text
open Newtonsoft.Json
open System.Net
open System.IO
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

let processURL (url:string) = //Bypass cache if URL ends with *, i.e. "https://pastebin.com/raw/0UZZJkR0*" -> "https://pastebin.com/raw/0UZZJkR0?1590525892"
  if url.[url.Length-1] = '*' then
    sprintf "%s?%f" (url.Substring(0,url.Length-1)) (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds
  else
    url

let addGameDayToConfig name expire subscribed =
  config.gameday <- config.gameday |> Array.append [|new GameDay(name, subscribed, expire, null)|]
  saveConfig ()

let check () = 
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
            if Console.ReadKey(true).Key = ConsoleKey.Y then
              colorprintfn "\n$green[Subscribing to %s]" gameday.name
              addGameDayToConfig (sprintf "%s:%s" (index |> hashString) (gameday.name)) (gameday.expire) true
              true
            else
              colorprintfn "\n$red[Not subscribing to %s]" gameday.name
              addGameDayToConfig (sprintf "%s:%s" (index |> hashString) (gameday.name)) (gameday.expire) false
              false
          else
            false
      )
      |> Array.iter (fun gameday -> //Process lists which have changed
        DownloadMap.downloadMaps (gameday.name) (gameday.maps)
        config.gameday <- 
          config.gameday 
          |> Array.map (fun configgameday -> 
            if configgameday.name = sprintf "%s:%s" (index |> hashString) (gameday.name) then
              if (gameday.maps |> String.concat "" |> hashString) = gameday.hash then
                new GameDay(configgameday.name, configgameday.subscribed, configgameday.expire, gameday.maps |> String.concat "" |> hashString)
              else
                colorprintfn "$red[ERR06] : %A does not have the correct hash, please pass a path to the index into the GameDay Generator" (gameday.name)
                configgameday
            else
              configgameday
          )
        saveConfig ()
      )
    with
    | _ -> colorprintfn "$red[ERR05] : %A is not a valid gameday index" index
  )